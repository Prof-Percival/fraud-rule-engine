using FraudRuleEngine.Application.Abstractions;
using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Application.Tests.TestSupport;

// Hand written rather than a mocking framework. Each of these ports has one member, so a fake is
// shorter than the setup call would be, and a test asserting on RecordedAssessment reads better than
// one verifying an invocation.

internal sealed class FakeCustomerContextSource : ICustomerContextSource
{
    private readonly Func<TransactionEvent, FraudEvaluationContext> _build;

    public FakeCustomerContextSource(Func<TransactionEvent, FraudEvaluationContext>? build = null) =>
        _build = build ?? (transaction =>
            FraudEvaluationContext.WithoutHistory(transaction, TimeSpan.FromHours(24)));

    public int Calls { get; private set; }

    public Task<FraudEvaluationContext> LoadAsync(
        TransactionEvent transaction,
        CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(_build(transaction));
    }
}

internal sealed class FakeAssessmentStore : IFraudAssessmentStore
{
    public TransactionEvent? SavedTransaction { get; private set; }

    public FraudAssessment? SavedAssessment { get; private set; }

    public int Calls { get; private set; }

    public Task SaveAsync(
        TransactionEvent transaction,
        FraudAssessment assessment,
        CancellationToken cancellationToken)
    {
        Calls++;
        SavedTransaction = transaction;
        SavedAssessment = assessment;
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingAssessmentStore : IFraudAssessmentStore
{
    public Task SaveAsync(
        TransactionEvent transaction,
        FraudAssessment assessment,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Database unavailable.");
}

internal sealed class FixedRuleSetVersion : IRuleSetVersionProvider
{
    public FixedRuleSetVersion(string version = "test-1") => Current = RuleSetVersion.From(version);

    public RuleSetVersion Current { get; }
}

/// <summary>A clock frozen at a known instant, so assessment timestamps are assertable.</summary>
internal sealed class FrozenClock : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FrozenClock(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
