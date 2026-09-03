using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class HighRiskCategoryRuleTests
{
    private readonly HighRiskCategoryRule _rule = Defaults.HighRiskCategory();

    [Theory]
    [InlineData(TransactionCategory.Gambling)]
    [InlineData(TransactionCategory.Cryptocurrency)]
    [InlineData(TransactionCategory.InternationalTransfer)]
    public void Flags_a_category_on_the_watchlist(TransactionCategory category)
    {
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithCategory(category)
            .Build();

        _rule.Evaluate(ContextBuilder.For(transaction)).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Reports_a_watchlist_hit_as_weak_signal()
    {
        // Low rather than High, and deliberately so. Gambling is common legitimate behaviour, so this
        // rule should not on its own push a transaction towards a decline.
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithCategory(TransactionCategory.Gambling)
            .Build();

        _rule.Evaluate(ContextBuilder.For(transaction)).Severity.ShouldBe(RuleSeverity.Low);
    }

    [Theory]
    [InlineData(TransactionCategory.Groceries)]
    [InlineData(TransactionCategory.Dining)]
    [InlineData(TransactionCategory.Transport)]
    [InlineData(TransactionCategory.Travel)]
    [InlineData(TransactionCategory.Utilities)]
    [InlineData(TransactionCategory.Retail)]
    [InlineData(TransactionCategory.Healthcare)]
    [InlineData(TransactionCategory.Subscriptions)]
    [InlineData(TransactionCategory.CashWithdrawal)]
    [InlineData(TransactionCategory.Transfer)]
    public void Does_not_flag_a_category_off_the_watchlist(TransactionCategory category)
    {
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithCategory(category)
            .Build();

        _rule.Evaluate(ContextBuilder.For(transaction)).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_flag_an_unrecognised_category()
    {
        // Unknown means the upstream service sent a category this build has never heard of, which says
        // nothing about the customer. If unknown were treated as risky then every upstream release
        // that added a category would arrive as a wave of false positives.
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithCategory(TransactionCategory.Unknown)
            .Build();

        _rule.Evaluate(ContextBuilder.For(transaction)).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Ignores_the_amount_entirely()
    {
        // This rule is about category and nothing else. A large grocery shop is not its business, and
        // a small crypto purchase still is. Combining the two signals is the scoring step's job.
        var largeGroceries = TransactionEventBuilder.AValidEvent()
            .WithCategory(TransactionCategory.Groceries)
            .WithAmount(500_000m, Currency.Zar)
            .Build();

        var tinyCrypto = TransactionEventBuilder.AValidEvent()
            .WithCategory(TransactionCategory.Cryptocurrency)
            .WithAmount(0.01m, Currency.Zar)
            .Build();

        _rule.Evaluate(ContextBuilder.For(largeGroceries)).IsTriggered.ShouldBeFalse();
        _rule.Evaluate(ContextBuilder.For(tinyCrypto)).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Explains_the_flag_with_the_category_that_caused_it()
    {
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithCategory(TransactionCategory.Gambling)
            .Build();

        _rule.Evaluate(ContextBuilder.For(transaction)).Reason.ShouldContain("Gambling");
    }

    [Fact]
    public void Rejects_a_null_transaction()
    {
        Should.Throw<ArgumentNullException>(() => _rule.Evaluate(null!));
    }
}
