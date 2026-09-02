using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Tests.TestSupport;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class UnusualHourRuleTests
{
    private static readonly TimeSpan Johannesburg = TimeSpan.FromHours(2);

    private readonly UnusualHourRule _rule = new();

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 14)]
    [InlineData(3, 30)]
    [InlineData(4, 59)]
    public void Flags_a_transaction_inside_the_window(int hour, int minute)
    {
        var outcome = _rule.Evaluate(ContextBuilder.For(AtLocalTime(hour, minute)));

        outcome.IsTriggered.ShouldBeTrue();
        outcome.Severity.ShouldBe(RuleSeverity.Low);
    }

    [Theory]
    [InlineData(0, 59)]
    [InlineData(5, 0)]
    [InlineData(9, 15)]
    [InlineData(14, 30)]
    [InlineData(22, 45)]
    [InlineData(23, 59)]
    public void Does_not_flag_a_transaction_outside_the_window(int hour, int minute)
    {
        _rule.Evaluate(ContextBuilder.For(AtLocalTime(hour, minute))).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Treats_the_start_of_the_window_as_inside_and_the_end_as_outside()
    {
        // Both boundaries pinned down. Half open means 05:00 belongs to the ordinary day, so the
        // window cannot be read two ways.
        _rule.Evaluate(ContextBuilder.For(AtLocalTime(1, 0))).IsTriggered.ShouldBeTrue();
        _rule.Evaluate(ContextBuilder.For(AtLocalTime(5, 0))).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Judges_local_time_rather_than_utc()
    {
        // The point of the rule. Both transactions are the same instant. In Johannesburg the clock
        // reads 03:00 and the rule fires; the same instant in London reads 01:00, which is also
        // inside the window, so a third case is needed to show the two genuinely differ.
        var johannesburgThreeAm = TransactionEventBuilder.AValidEvent()
            .OccurringAt(new DateTimeOffset(2026, 9, 1, 3, 0, 0, Johannesburg))
            .Build();

        // Same instant as 15:00 in Johannesburg, which is ordinary afternoon spending. Judged against
        // UTC it would be 13:00, also ordinary, so this one agrees either way.
        var johannesburgAfternoon = TransactionEventBuilder.AValidEvent()
            .OccurringAt(new DateTimeOffset(2026, 9, 1, 15, 0, 0, Johannesburg))
            .Build();

        // The case that separates them. 02:00 in Auckland is 14:00 the previous day in UTC. A rule
        // reading UTC would call this ordinary afternoon spending and miss it entirely.
        var aucklandTwoAm = TransactionEventBuilder.AValidEvent()
            .OccurringAt(new DateTimeOffset(2026, 9, 1, 2, 0, 0, TimeSpan.FromHours(12)))
            .Build();

        _rule.Evaluate(ContextBuilder.For(johannesburgThreeAm)).IsTriggered.ShouldBeTrue();
        _rule.Evaluate(ContextBuilder.For(johannesburgAfternoon)).IsTriggered.ShouldBeFalse();

        aucklandTwoAm.OccurredAtUtc.Hour.ShouldBe(14);
        _rule.Evaluate(ContextBuilder.For(aucklandTwoAm)).IsTriggered.ShouldBeTrue();
    }

    [Fact]
    public void Does_not_flag_afternoon_spending_that_looks_nocturnal_in_utc()
    {
        // The mirror of the case above, and the more damaging failure of the two. 15:00 in Hawaii is
        // 01:00 the next day in UTC, squarely inside the window. A rule reading UTC would flag an
        // ordinary afternoon purchase.
        var hawaiiAfternoon = TransactionEventBuilder.AValidEvent()
            .OccurringAt(new DateTimeOffset(2026, 9, 1, 15, 0, 0, TimeSpan.FromHours(-10)))
            .Build();

        hawaiiAfternoon.OccurredAtUtc.Hour.ShouldBe(1);
        _rule.Evaluate(ContextBuilder.For(hawaiiAfternoon)).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Quotes_the_local_time_and_the_window_in_the_reason()
    {
        var reason = _rule.Evaluate(ContextBuilder.For(AtLocalTime(3, 30))).Reason;

        reason.ShouldContain("03:30");
        reason.ShouldContain("01:00");
        reason.ShouldContain("05:00");
    }

    [Fact]
    public void Rejects_a_null_transaction()
    {
        Should.Throw<ArgumentNullException>(() => _rule.Evaluate(null!));
    }

    private static Domain.Transactions.TransactionEvent AtLocalTime(int hour, int minute) =>
        TransactionEventBuilder.AValidEvent()
            .OccurringAt(new DateTimeOffset(2026, 9, 1, hour, minute, 0, Johannesburg))
            .Build();
}
