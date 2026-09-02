using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class FraudEvaluationContextTests
{
    [Fact]
    public void Carries_the_transaction_and_its_history()
    {
        var transaction = Transaction("evt-1", "CUST-1");
        var earlier = Transaction("evt-0", "CUST-1");

        var context = ContextBuilder.WithHistory(transaction, [earlier]);

        context.Transaction.ShouldBe(transaction);
        context.History.RecentTransactions.ShouldHaveSingleItem();
    }

    [Fact]
    public void Supports_a_customer_with_no_history_at_all()
    {
        // A customer's first ever transaction. Rules have to cope with this rather than treating it as
        // an error, so it is a first class state.
        var context = FraudEvaluationContext.WithoutHistory(
            Transaction("evt-1", "CUST-1"),
            TimeSpan.FromHours(24));

        context.History.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_history_belonging_to_a_different_customer()
    {
        // An enrichment query with a wrong or missing customer predicate would produce velocity and
        // escalation results that are wrong and entirely plausible looking, so nobody would notice.
        // This check is the only thing that would ever catch it.
        var transaction = Transaction("evt-1", "CUST-1");
        var somebodyElse = Transaction("evt-0", "CUST-2");

        var act = () => { _ = ContextBuilder.WithHistory(transaction, [somebodyElse]); };

        act.ShouldThrow<ArgumentException>()
            .Message.ShouldContain("CUST-2");
    }

    [Fact]
    public void Rejects_history_containing_the_transaction_being_evaluated()
    {
        // If the transaction appeared in its own history then every count would be one too high, and
        // the velocity rule would drift towards firing on perfectly ordinary transactions.
        var transaction = Transaction("evt-1", "CUST-1");
        var itself = Transaction("evt-1", "CUST-1");

        var act = () => { _ = ContextBuilder.WithHistory(transaction, [itself]); };

        act.ShouldThrow<ArgumentException>()
            .Message.ShouldContain("must not contain");
    }

    [Fact]
    public void Carries_the_customer_baseline_alongside_the_recent_history()
    {
        // Two different questions over two different periods. The history holds individual
        // transactions for velocity; the baseline is an aggregate for judging what is normal.
        var baseline = CustomerBaseline.Over(
            TimeSpan.FromDays(90),
            transactionCount: 42,
            new Money(320m, Currency.Zar),
            [MerchantId.From("MERCH-0001")]);

        var context = ContextBuilder.WithBaseline(Transaction("evt-1", "CUST-1"), baseline);

        context.Baseline.TransactionCount.ShouldBe(42);
        context.Baseline.AverageAmount.ShouldBe(new Money(320m, Currency.Zar));
        context.Baseline.HasUsed(MerchantId.From("MERCH-0001")).ShouldBeTrue();
    }

    [Fact]
    public void Defaults_to_no_baseline_for_a_customer_nobody_has_seen()
    {
        var context = FraudEvaluationContext.WithoutHistory(
            Transaction("evt-1", "CUST-1"),
            TimeSpan.FromHours(24));

        context.Baseline.HasHistory.ShouldBeFalse();
        context.Baseline.AverageAmount.ShouldBeNull();
    }

    [Fact]
    public void Rejects_a_missing_transaction_history_or_baseline()
    {
        var history = CustomerHistory.Empty(TimeSpan.FromHours(1));
        var transaction = Transaction("evt-1", "CUST-1");

        Should.Throw<ArgumentNullException>(
            () => new FraudEvaluationContext(null!, history, CustomerBaseline.None));

        Should.Throw<ArgumentNullException>(
            () => new FraudEvaluationContext(transaction, null!, CustomerBaseline.None));

        Should.Throw<ArgumentNullException>(
            () => new FraudEvaluationContext(transaction, history, null!));
    }

    private static TransactionEvent Transaction(string eventId, string customerId) =>
        TransactionEventBuilder.AValidEvent()
            .WithEventId(EventId.From(eventId))
            .WithCustomerId(CustomerId.From(customerId))
            .Build();
}
