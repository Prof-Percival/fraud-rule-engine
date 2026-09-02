using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class CustomerBaselineTests
{
    [Fact]
    public void Carries_the_aggregate_it_was_built_from()
    {
        var baseline = CustomerBaseline.Over(
            TimeSpan.FromDays(90),
            transactionCount: 42,
            new Money(320m, Currency.Zar),
            [MerchantId.From("MERCH-1"), MerchantId.From("MERCH-2")]);

        baseline.Period.ShouldBe(TimeSpan.FromDays(90));
        baseline.TransactionCount.ShouldBe(42);
        baseline.AverageAmount.ShouldBe(new Money(320m, Currency.Zar));
        baseline.KnownMerchants.Count.ShouldBe(2);
        baseline.HasHistory.ShouldBeTrue();
    }

    [Fact]
    public void None_represents_a_customer_nobody_has_seen_before()
    {
        // A real state rather than a placeholder. Rules have to tell "unlike this customer's normal
        // behaviour" apart from "this customer has no normal behaviour yet", because the second is not
        // evidence of anything.
        CustomerBaseline.None.HasHistory.ShouldBeFalse();
        CustomerBaseline.None.TransactionCount.ShouldBe(0);
        CustomerBaseline.None.AverageAmount.ShouldBeNull();
        CustomerBaseline.None.KnownMerchants.ShouldBeEmpty();
    }

    [Fact]
    public void Answers_whether_a_merchant_has_been_used()
    {
        var baseline = CustomerBaseline.Over(
            TimeSpan.FromDays(90),
            transactionCount: 10,
            new Money(100m, Currency.Zar),
            [MerchantId.From("MERCH-1")]);

        baseline.HasUsed(MerchantId.From("MERCH-1")).ShouldBeTrue();
        baseline.HasUsed(MerchantId.From("MERCH-2")).ShouldBeFalse();
    }

    [Fact]
    public void Deduplicates_repeated_merchants()
    {
        var baseline = CustomerBaseline.Over(
            TimeSpan.FromDays(90),
            transactionCount: 10,
            new Money(100m, Currency.Zar),
            [MerchantId.From("MERCH-1"), MerchantId.From("MERCH-1"), MerchantId.From("MERCH-2")]);

        baseline.KnownMerchants.Count.ShouldBe(2);
    }

    [Fact]
    public void Requires_a_positive_period()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => CustomerBaseline.Over(TimeSpan.Zero, 10, null, []));
    }

    [Fact]
    public void Rejects_a_negative_transaction_count()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => CustomerBaseline.Over(TimeSpan.FromDays(90), -1, null, []));
    }

    [Fact]
    public void Allows_a_period_with_no_transactions_in_it()
    {
        // A customer who exists but has done nothing in the last ninety days. Distinct from None,
        // which means nothing was loaded at all.
        var baseline = CustomerBaseline.Over(TimeSpan.FromDays(90), 0, null, []);

        baseline.HasHistory.ShouldBeFalse();
        baseline.Period.ShouldBe(TimeSpan.FromDays(90));
    }
}
