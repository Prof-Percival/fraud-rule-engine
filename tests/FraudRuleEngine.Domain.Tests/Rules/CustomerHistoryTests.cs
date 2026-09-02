using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class CustomerHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Orders_transactions_most_recent_first_whatever_order_they_arrive_in()
    {
        // Sorted on construction so that every rule sees the same sequence, and so two evaluations of
        // one transaction cannot differ because the database returned rows in a different order.
        var history = new CustomerHistory(
            TimeSpan.FromHours(24),
            [
                At(Now.AddMinutes(-30)),
                At(Now.AddMinutes(-5)),
                At(Now.AddMinutes(-90)),
            ]);

        history.RecentTransactions
            .Select(transaction => transaction.OccurredAtUtc)
            .ShouldBe([Now.AddMinutes(-5), Now.AddMinutes(-30), Now.AddMinutes(-90)]);
    }

    [Fact]
    public void Reports_an_empty_history_as_empty()
    {
        var history = CustomerHistory.Empty(TimeSpan.FromHours(24));

        history.IsEmpty.ShouldBeTrue();
        history.RecentTransactions.ShouldBeEmpty();
        history.Lookback.ShouldBe(TimeSpan.FromHours(24));
    }

    [Fact]
    public void Requires_a_positive_window()
    {
        // A zero window would make an empty history indistinguishable from one where nothing was
        // loaded, and every rule asking a question about a period would get a misleading answer.
        Should.Throw<ArgumentOutOfRangeException>(() => new CustomerHistory(TimeSpan.Zero, []));
        Should.Throw<ArgumentOutOfRangeException>(
            () => new CustomerHistory(TimeSpan.FromMinutes(-5), []));
    }

    [Fact]
    public void Returns_only_the_transactions_inside_the_requested_window()
    {
        var history = new CustomerHistory(
            TimeSpan.FromHours(24),
            [
                At(Now.AddMinutes(-2)),
                At(Now.AddMinutes(-8)),
                At(Now.AddMinutes(-45)),
                At(Now.AddHours(-6)),
            ]);

        history.Within(TimeSpan.FromMinutes(10), Now).Count.ShouldBe(2);
        history.Within(TimeSpan.FromHours(1), Now).Count.ShouldBe(3);
        history.Within(TimeSpan.FromHours(24), Now).Count.ShouldBe(4);
    }

    [Fact]
    public void Includes_a_transaction_exactly_on_the_window_boundary()
    {
        var history = new CustomerHistory(TimeSpan.FromHours(24), [At(Now.AddMinutes(-10))]);

        history.Within(TimeSpan.FromMinutes(10), Now).Count.ShouldBe(1);
        history.Within(TimeSpan.FromMinutes(9), Now).ShouldBeEmpty();
    }

    [Fact]
    public void Refuses_a_window_wider_than_it_was_loaded_with()
    {
        // The important one. Returning whatever happened to be loaded would let a rule silently under
        // report: asking for an hour when thirty minutes was fetched would show half the traffic and
        // clear transactions that should have been flagged. No error, no alert, detection just stops.
        var history = new CustomerHistory(TimeSpan.FromMinutes(30), [At(Now.AddMinutes(-5))]);

        var act = () => { _ = history.Within(TimeSpan.FromHours(1), Now); };

        act.ShouldThrow<ArgumentOutOfRangeException>()
            .ParamName.ShouldBe("window");
    }

    [Fact]
    public void Allows_a_window_exactly_as_wide_as_it_was_loaded_with()
    {
        var history = new CustomerHistory(TimeSpan.FromMinutes(30), [At(Now.AddMinutes(-5))]);

        history.Within(TimeSpan.FromMinutes(30), Now).Count.ShouldBe(1);
    }

    [Fact]
    public void Ignores_transactions_after_the_reference_point()
    {
        // Guards against an enrichment query that ignores the upper bound. A transaction later than the
        // one being judged cannot have influenced it, and counting it would make velocity depend on
        // when the evaluation happened to run rather than on what the customer did.
        var history = new CustomerHistory(
            TimeSpan.FromHours(24),
            [At(Now.AddMinutes(-5)), At(Now.AddMinutes(5))]);

        history.Within(TimeSpan.FromHours(1), Now).Count.ShouldBe(1);
    }

    private static TransactionEvent At(DateTimeOffset occurredAt) =>
        TransactionEventBuilder.AValidEvent()
            .WithEventId(EventId.From($"evt-{occurredAt.Ticks}"))
            .OccurringAt(occurredAt)
            .Build();
}
