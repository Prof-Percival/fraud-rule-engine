using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class TransactionVelocityRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TransactionVelocityRule _rule = new();

    [Fact]
    public void Does_not_flag_a_customer_with_no_history()
    {
        // A customer's first ever transaction, which has to be handled rather than treated as an error.
        _rule.Evaluate(ContextBuilder.For(At(Now))).IsTriggered.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Does_not_flag_fewer_transactions_than_the_threshold(int preceding)
    {
        var outcome = Evaluate(preceding, TimeSpan.FromMinutes(1));

        outcome.IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Flags_at_exactly_the_threshold()
    {
        // Four preceding plus the one being judged makes five. The threshold counts the transaction
        // under evaluation, so five means five in total rather than five before this one.
        var outcome = Evaluate(preceding: 4, spacing: TimeSpan.FromMinutes(1));

        outcome.IsTriggered.ShouldBeTrue();
        outcome.Severity.ShouldBe(RuleSeverity.Medium);
    }

    [Fact]
    public void Flags_above_the_threshold()
    {
        Evaluate(preceding: 10, spacing: TimeSpan.FromSeconds(20)).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Ignores_transactions_outside_the_window()
    {
        // Ten transactions, but spaced four minutes apart so only three fall inside the fifteen minute
        // window. Spread out, the same volume is ordinary behaviour.
        Evaluate(preceding: 10, spacing: TimeSpan.FromMinutes(4)).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Counts_a_transaction_exactly_on_the_window_edge()
    {
        var history = new[]
        {
            At(Now.AddMinutes(-15)),
            At(Now.AddMinutes(-10)),
            At(Now.AddMinutes(-5)),
            At(Now.AddMinutes(-1)),
        };

        _rule.Evaluate(ContextBuilder.WithHistory(At(Now), history)).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Measures_the_window_from_the_transaction_rather_than_from_now()
    {
        // An event that took hours to arrive must score the same as one handled live, otherwise
        // replaying history produces different assessments from the originals and nothing is
        // reproducible.
        var lastYear = new DateTimeOffset(2025, 3, 4, 9, 0, 0, TimeSpan.Zero);

        var history = Enumerable.Range(1, 4)
            .Select(minutes => At(lastYear.AddMinutes(-minutes)))
            .ToArray();

        _rule.Evaluate(ContextBuilder.WithHistory(At(lastYear), history)).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Refuses_to_answer_when_less_history_was_loaded_than_it_needs()
    {
        // Under reporting would be worse than failing. With five minutes loaded the rule would see part
        // of the traffic, clear transactions it should flag, and raise no error at all.
        var context = ContextBuilder.WithHistory(
            At(Now),
            [At(Now.AddMinutes(-1))],
            lookback: TimeSpan.FromMinutes(5));

        Should.Throw<ArgumentOutOfRangeException>(() => _rule.Evaluate(context));
    }

    [Fact]
    public void Reports_the_count_and_the_threshold()
    {
        var reason = Evaluate(preceding: 4, spacing: TimeSpan.FromMinutes(1)).Reason;

        reason.ShouldContain("5");
        reason.ShouldContain("15 minutes");
    }

    [Fact]
    public void Rejects_a_null_context()
    {
        Should.Throw<ArgumentNullException>(() => _rule.Evaluate(null!));
    }

    private RuleOutcome Evaluate(int preceding, TimeSpan spacing)
    {
        var history = Enumerable.Range(1, preceding)
            .Select(step => At(Now - (spacing * step)))
            .ToArray();

        return _rule.Evaluate(ContextBuilder.WithHistory(At(Now), history));
    }

    private static TransactionEvent At(DateTimeOffset occurredAt) =>
        TransactionEventBuilder.AValidEvent()
            .WithEventId(EventId.From($"evt-{occurredAt.Ticks}"))
            .OccurringAt(occurredAt)
            .Build();
}
