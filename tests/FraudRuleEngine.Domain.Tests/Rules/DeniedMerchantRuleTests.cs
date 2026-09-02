using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class DeniedMerchantRuleTests
{
    private readonly DeniedMerchantRule _rule = new();

    [Theory]
    [InlineData("MERCH-DENY-0001")]
    [InlineData("MERCH-DENY-0002")]
    [InlineData("MERCH-DENY-0003")]
    public void Flags_a_merchant_on_the_deny_list(string merchantId)
    {
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithMerchant(new Merchant(MerchantId.From(merchantId), "Some Trading Name"))
            .Build();

        var outcome = _rule.Evaluate(ContextBuilder.For(transaction));

        outcome.IsTriggered.ShouldBeTrue();
        outcome.Severity.ShouldBe(RuleSeverity.High);
    }

    [Fact]
    public void Does_not_flag_a_merchant_off_the_deny_list()
    {
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithMerchant(new Merchant(MerchantId.From("MERCH-0001"), "Checkers Hyper Constantia"))
            .Build();

        _rule.Evaluate(ContextBuilder.For(transaction)).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Matches_on_the_identifier_and_not_the_name()
    {
        // A merchant name is free text the acquirer controls and it varies in spelling and casing
        // between transactions, so denying by name would be trivial to evade and would also catch
        // unrelated merchants that happen to share one.
        var deniedIdWithInnocentName = TransactionEventBuilder.AValidEvent()
            .WithMerchant(new Merchant(MerchantId.From("MERCH-DENY-0001"), "Woolworths Sandton"))
            .Build();

        var innocentIdWithDeniedLookingName = TransactionEventBuilder.AValidEvent()
            .WithMerchant(new Merchant(MerchantId.From("MERCH-9999"), "MERCH-DENY-0001"))
            .Build();

        _rule.Evaluate(ContextBuilder.For(deniedIdWithInnocentName)).IsTriggered.ShouldBeTrue();
        _rule.Evaluate(ContextBuilder.For(innocentIdWithDeniedLookingName)).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Flags_regardless_of_amount_or_category()
    {
        // A deny list hit is a policy violation rather than a probabilistic signal, so nothing else
        // about the transaction makes it less of one.
        var trivialAndOrdinary = TransactionEventBuilder.AValidEvent()
            .WithMerchant(new Merchant(MerchantId.From("MERCH-DENY-0002"), "Corner Cafe"))
            .WithAmount(1m, Currency.Zar)
            .WithCategory(TransactionCategory.Groceries)
            .Build();

        _rule.Evaluate(ContextBuilder.For(trivialAndOrdinary)).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Names_the_merchant_in_the_reason()
    {
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithMerchant(new Merchant(MerchantId.From("MERCH-DENY-0001"), "Dodgy Electronics"))
            .Build();

        var reason = _rule.Evaluate(ContextBuilder.For(transaction)).Reason;

        reason.ShouldContain("MERCH-DENY-0001");
        reason.ShouldContain("Dodgy Electronics");
    }

    [Fact]
    public void Is_case_sensitive_on_the_identifier()
    {
        // Consistent with MerchantId itself, which does not fold case. An upstream identifier is an
        // opaque token and this rule is not entitled to decide two spellings mean the same merchant.
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithMerchant(new Merchant(MerchantId.From("merch-deny-0001"), "Dodgy Electronics"))
            .Build();

        _rule.Evaluate(ContextBuilder.For(transaction)).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_null_transaction()
    {
        Should.Throw<ArgumentNullException>(() => _rule.Evaluate(null!));
    }
}
