using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class AmountEscalationRuleTests
{
    private readonly AmountEscalationRule _rule = new();

    [Fact]
    public void Flags_an_amount_far_above_the_customers_average()
    {
        // An account whose ordinary traffic is a few hundred rand suddenly paying twenty thousand.
        var outcome = Evaluate(amount: 20_000m, average: 400m, priorTransactions: 60);

        outcome.IsTriggered.ShouldBeTrue();
        outcome.Severity.ShouldBe(RuleSeverity.Medium);
    }

    [Fact]
    public void Flags_at_exactly_the_multiple()
    {
        Evaluate(amount: 2_000m, average: 400m, priorTransactions: 60).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Does_not_flag_one_cent_below_the_multiple()
    {
        Evaluate(amount: 1_999.99m, average: 400m, priorTransactions: 60).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_flag_ordinary_variation()
    {
        Evaluate(amount: 900m, average: 400m, priorTransactions: 60).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Judges_each_customer_against_their_own_behaviour()
    {
        // The point of the rule. The same amount is unremarkable for one customer and a five fold jump
        // for another, and a fixed threshold cannot express that.
        Evaluate(amount: 10_000m, average: 4_000m, priorTransactions: 60).IsTriggered.ShouldBeFalse();
        Evaluate(amount: 10_000m, average: 300m, priorTransactions: 60).IsTriggered.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    public void Does_not_draw_conclusions_from_too_little_history(int priorTransactions)
    {
        // An average over two transactions is not a baseline, and flagging a third purchase for being
        // unlike the first two says nothing.
        var outcome = Evaluate(amount: 50_000m, average: 100m, priorTransactions);

        outcome.IsTriggered.ShouldBeFalse();
        outcome.Reason.ShouldContain("fewer than");
    }

    [Fact]
    public void Starts_drawing_conclusions_at_the_minimum_history()
    {
        Evaluate(amount: 20_000m, average: 400m, priorTransactions: 10).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Declines_when_the_baseline_is_in_a_different_currency()
    {
        // A customer transacting in rand and dollars has no single meaningful average, and Money would
        // rightly refuse the comparison. Converting at some ambient rate would produce a number nobody
        // can reconcile.
        var baseline = CustomerBaseline.Over(
            TimeSpan.FromDays(90),
            transactionCount: 60,
            new Money(400m, Currency.Zar),
            []);

        var transaction = TransactionEventBuilder.AValidEvent()
            .WithAmount(new Money(5_000m, Currency.Usd))
            .Build();

        var outcome = _rule.Evaluate(ContextBuilder.WithBaseline(transaction, baseline));

        outcome.IsTriggered.ShouldBeFalse();
        outcome.Reason.ShouldContain("cannot be compared");
    }

    [Fact]
    public void Declines_when_there_is_no_average_at_all()
    {
        var baseline = CustomerBaseline.Over(
            TimeSpan.FromDays(90),
            transactionCount: 60,
            averageAmount: null,
            []);

        var transaction = TransactionEventBuilder.AValidEvent().WithAmount(50_000m).Build();

        _rule.Evaluate(ContextBuilder.WithBaseline(transaction, baseline)).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Declines_when_the_average_is_zero()
    {
        // Any multiple of zero is zero, so every transaction would clear the bar and the rule would fire
        // on all of them. An average of zero means something is wrong upstream, not that the customer is
        // behaving unusually.
        var baseline = CustomerBaseline.Over(
            TimeSpan.FromDays(90),
            transactionCount: 60,
            Money.Zero(Currency.Zar),
            []);

        var transaction = TransactionEventBuilder.AValidEvent().WithAmount(1m).Build();

        var outcome = _rule.Evaluate(ContextBuilder.WithBaseline(transaction, baseline));

        outcome.IsTriggered.ShouldBeFalse();
        outcome.Reason.ShouldContain("zero");
    }

    [Fact]
    public void Quotes_the_amount_the_average_and_the_trigger_point()
    {
        var reason = Evaluate(amount: 20_000m, average: 400m, priorTransactions: 60).Reason;

        reason.ShouldContain("20000");
        reason.ShouldContain("400");
        reason.ShouldContain("2000");
        reason.ShouldContain("60");
    }

    [Fact]
    public void Rejects_a_null_context()
    {
        Should.Throw<ArgumentNullException>(() => _rule.Evaluate(null!));
    }

    private RuleOutcome Evaluate(decimal amount, decimal average, int priorTransactions)
    {
        var baseline = priorTransactions == 0
            ? CustomerBaseline.None
            : CustomerBaseline.Over(
                TimeSpan.FromDays(90),
                priorTransactions,
                new Money(average, Currency.Zar),
                []);

        var transaction = TransactionEventBuilder.AValidEvent().WithAmount(amount).Build();

        return _rule.Evaluate(ContextBuilder.WithBaseline(transaction, baseline));
    }
}
