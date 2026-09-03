using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class FirstTimeMerchantHighValueRuleTests
{
    private static readonly MerchantId Familiar = MerchantId.From("MERCH-FAMILIAR");
    private static readonly MerchantId Unfamiliar = MerchantId.From("MERCH-NEW");

    private readonly FirstTimeMerchantHighValueRule _rule = Defaults.FirstTimeMerchantHighValue();

    [Fact]
    public void Flags_a_large_amount_at_a_merchant_never_used_before()
    {
        var outcome = Evaluate(Unfamiliar, 12_000m, priorTransactions: 50);

        outcome.IsTriggered.ShouldBeTrue();
        outcome.Severity.ShouldBe(RuleSeverity.Medium);
    }

    [Fact]
    public void Does_not_flag_a_large_amount_at_a_familiar_merchant()
    {
        // Half of the combination on its own. Spending a lot somewhere the customer shops regularly is
        // routine.
        Evaluate(Familiar, 50_000m, priorTransactions: 50).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_flag_a_small_amount_at_a_new_merchant()
    {
        // The other half. Customers shop somewhere new constantly, and buying lunch there is not a
        // signal.
        Evaluate(Unfamiliar, 85m, priorTransactions: 50).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Flags_at_exactly_the_threshold()
    {
        Evaluate(Unfamiliar, 5_000m, priorTransactions: 50).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Does_not_flag_one_cent_below_the_threshold()
    {
        Evaluate(Unfamiliar, 4_999.99m, priorTransactions: 50).IsTriggered.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    public void Does_not_draw_conclusions_from_too_little_history(int priorTransactions)
    {
        // Every merchant looks new to a customer with three transactions. Flagging their fourth
        // purchase for being somewhere they have not been says nothing about the transaction and
        // everything about how little is known.
        var outcome = Evaluate(Unfamiliar, 50_000m, priorTransactions);

        outcome.IsTriggered.ShouldBeFalse();
        outcome.Reason.ShouldContain("too few");
    }

    [Fact]
    public void Starts_drawing_conclusions_at_the_minimum_history()
    {
        Evaluate(Unfamiliar, 12_000m, priorTransactions: 10).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Does_not_flag_a_currency_it_has_no_threshold_for()
    {
        var baseline = Baseline(priorTransactions: 50);

        var transaction = TransactionEventBuilder.AValidEvent()
            .WithMerchant(new Merchant(Unfamiliar, "Some New Shop"))
            .WithAmount(new Money(9_999m, Currency.Eur))
            .Build();

        // Euro does have a threshold, so this proves the configured ones are used per currency rather
        // than the rand figure being applied to everything.
        _rule.Evaluate(ContextBuilder.WithBaseline(transaction, baseline)).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Applies_the_threshold_for_the_transaction_currency()
    {
        // 400 dollars is over the 300 dollar threshold and far under the 5,000 rand one, so a rule
        // comparing bare numbers against a single figure would miss it.
        Evaluate(Unfamiliar, 400m, priorTransactions: 50, Currency.Usd).IsTriggered.ShouldBeTrue();
        Evaluate(Unfamiliar, 250m, priorTransactions: 50, Currency.Usd).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Names_the_merchant_and_the_threshold_in_the_reason()
    {
        var reason = Evaluate(Unfamiliar, 12_000m, priorTransactions: 50).Reason;

        reason.ShouldContain("MERCH-NEW");
        reason.ShouldContain("Some New Shop");
        reason.ShouldContain("5000");
    }

    [Fact]
    public void Rejects_a_null_context()
    {
        Should.Throw<ArgumentNullException>(() => _rule.Evaluate(null!));
    }

    private RuleOutcome Evaluate(
        MerchantId merchant,
        decimal amount,
        int priorTransactions,
        Currency currency = Currency.Zar)
    {
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithMerchant(new Merchant(merchant, "Some New Shop"))
            .WithAmount(new Money(amount, currency))
            .Build();

        return _rule.Evaluate(ContextBuilder.WithBaseline(transaction, Baseline(priorTransactions)));
    }

    private static CustomerBaseline Baseline(int priorTransactions) =>
        priorTransactions == 0
            ? CustomerBaseline.None
            : CustomerBaseline.Over(
                TimeSpan.FromDays(90),
                priorTransactions,
                new Money(400m, Currency.Zar),
                [Familiar]);
}
