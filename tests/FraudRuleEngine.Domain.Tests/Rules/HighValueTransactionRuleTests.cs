using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class HighValueTransactionRuleTests
{
    private readonly HighValueTransactionRule _rule = Defaults.HighValue();

    [Fact]
    public void Flags_an_amount_above_the_threshold()
    {
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithAmount(48_500m, Currency.Zar)
            .Build();

        var outcome = _rule.Evaluate(ContextBuilder.For(transaction));

        outcome.IsTriggered.ShouldBeTrue();
        outcome.Severity.ShouldBe(RuleSeverity.High);
        outcome.RuleId.ShouldBe(_rule.Id);
    }

    [Fact]
    public void Flags_an_amount_exactly_at_the_threshold()
    {
        // The boundary, stated explicitly. "At or above" has to mean something definite, because a
        // threshold that excludes its own value is really a different threshold and the analyst tuning
        // it would have no way of knowing.
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithAmount(25_000m, Currency.Zar)
            .Build();

        _rule.Evaluate(ContextBuilder.For(transaction)).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Does_not_flag_an_amount_one_cent_below_the_threshold()
    {
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithAmount(24_999.99m, Currency.Zar)
            .Build();

        _rule.Evaluate(ContextBuilder.For(transaction)).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_flag_an_ordinary_amount()
    {
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithAmount(250m, Currency.Zar)
            .Build();

        var outcome = _rule.Evaluate(ContextBuilder.For(transaction));

        outcome.IsTriggered.ShouldBeFalse();
        outcome.Severity.ShouldBe(RuleSeverity.None);
    }

    [Theory]
    [InlineData(Currency.Zar, 25_000)]
    [InlineData(Currency.Usd, 1_500)]
    [InlineData(Currency.Eur, 1_400)]
    [InlineData(Currency.Gbp, 1_200)]
    public void Applies_the_threshold_for_the_transaction_currency(Currency currency, int threshold)
    {
        var atThreshold = TransactionEventBuilder.AValidEvent()
            .WithAmount(threshold, currency)
            .Build();

        var justUnder = TransactionEventBuilder.AValidEvent()
            .WithAmount(threshold - 1, currency)
            .Build();

        _rule.Evaluate(ContextBuilder.For(atThreshold)).IsTriggered.ShouldBeTrue();
        _rule.Evaluate(ContextBuilder.For(justUnder)).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Uses_the_threshold_for_the_currency_rather_than_comparing_bare_numbers()
    {
        // The case that justifies holding thresholds per currency. Two thousand dollars is over the
        // dollar threshold of 1,500 and far below the rand figure of 25,000. A rule that compared the
        // raw number against one threshold would miss this altogether, which is a fraudulent
        // transaction going through rather than a false positive.
        var dollars = TransactionEventBuilder.AValidEvent()
            .WithAmount(2_000m, Currency.Usd)
            .Build();

        var outcome = _rule.Evaluate(ContextBuilder.For(dollars));

        outcome.IsTriggered.ShouldBeTrue();
        outcome.Reason.ShouldContain("USD");
        outcome.Reason.ShouldNotContain("ZAR");
    }

    [Fact]
    public void Explains_the_flag_with_the_amount_and_the_threshold_it_crossed()
    {
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithAmount(48_500m, Currency.Zar)
            .Build();

        var reason = _rule.Evaluate(ContextBuilder.For(transaction)).Reason;

        reason.ShouldContain("48500");
        reason.ShouldContain("25000");
        reason.ShouldContain("ZAR");
    }

    [Fact]
    public void Explains_why_it_did_not_fire()
    {
        // Clear outcomes carry a reason too, because a stored assessment has to be reviewable when
        // the transaction turns out to have been fraud that nothing caught.
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithAmount(250m, Currency.Zar)
            .Build();

        _rule.Evaluate(ContextBuilder.For(transaction)).Reason.ShouldContain("below");
    }

    [Fact]
    public void Rejects_a_null_transaction()
    {
        Should.Throw<ArgumentNullException>(() => _rule.Evaluate(null!));
    }
}
