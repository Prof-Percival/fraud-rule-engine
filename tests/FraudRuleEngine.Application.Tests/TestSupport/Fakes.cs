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
    private readonly Dictionary<string, FraudAssessment> _byEventId = new(StringComparer.Ordinal);

    /// <summary>Makes the next save report the event as already assessed.</summary>
    public FraudAssessment? ExistingForNextSave { get; set; }

    public TransactionEvent? SavedTransaction { get; private set; }

    public FraudAssessment? SavedAssessment { get; private set; }

    public int Calls { get; private set; }

    public int LookupCalls { get; private set; }

    public Task<SaveResult> SaveAsync(
        TransactionEvent transaction,
        FraudAssessment assessment,
        CancellationToken cancellationToken)
    {
        Calls++;

        if (ExistingForNextSave is { } existing)
        {
            _byEventId[transaction.EventId.Value] = existing;
            return Task.FromResult(SaveResult.AlreadyAssessed);
        }

        SavedTransaction = transaction;
        SavedAssessment = assessment;
        _byEventId[transaction.EventId.Value] = assessment;

        return Task.FromResult(SaveResult.Saved);
    }

    public Task<FraudAssessment?> FindByEventAsync(EventId eventId, CancellationToken cancellationToken)
    {
        LookupCalls++;

        return Task.FromResult(_byEventId.GetValueOrDefault(eventId.Value));
    }
}

/// <summary>Reports a clash but has nothing stored, which should not happen and must not be silent.</summary>
internal sealed class InconsistentAssessmentStore : IFraudAssessmentStore
{
    public Task<SaveResult> SaveAsync(
        TransactionEvent transaction,
        FraudAssessment assessment,
        CancellationToken cancellationToken) =>
        Task.FromResult(SaveResult.AlreadyAssessed);

    public Task<FraudAssessment?> FindByEventAsync(EventId eventId, CancellationToken cancellationToken) =>
        Task.FromResult<FraudAssessment?>(null);
}

internal sealed class ThrowingAssessmentStore : IFraudAssessmentStore
{
    public Task<SaveResult> SaveAsync(
        TransactionEvent transaction,
        FraudAssessment assessment,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Database unavailable.");

    public Task<FraudAssessment?> FindByEventAsync(EventId eventId, CancellationToken cancellationToken) =>
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
