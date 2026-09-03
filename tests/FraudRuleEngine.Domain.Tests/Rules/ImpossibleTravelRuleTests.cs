using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class ImpossibleTravelRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ImpossibleTravelRule _rule = Defaults.ImpossibleTravel();

    [Fact]
    public void Flags_a_card_used_in_two_distant_countries_minutes_apart()
    {
        // Johannesburg then London twenty minutes later. Around nine thousand kilometres, so roughly
        // twenty seven thousand km/h would be required. One of the two was not the cardholder.
        var context = ContextBuilder.WithHistory(
            CardPresent("ZA", Now),
            [CardPresent("GB", Now.AddMinutes(-20))]);

        var outcome = _rule.Evaluate(context);

        outcome.IsTriggered.ShouldBeTrue();
        outcome.Severity.ShouldBe(RuleSeverity.High);
    }

    [Fact]
    public void Does_not_flag_the_same_journey_given_enough_time()
    {
        // The same two countries fifteen hours apart is an ordinary long haul flight.
        var context = ContextBuilder.WithHistory(
            CardPresent("ZA", Now),
            [CardPresent("GB", Now.AddHours(-15))],
            lookback: TimeSpan.FromHours(24));

        _rule.Evaluate(context).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_flag_two_transactions_in_the_same_country()
    {
        var context = ContextBuilder.WithHistory(
            CardPresent("ZA", Now),
            [CardPresent("ZA", Now.AddMinutes(-1))]);

        _rule.Evaluate(context).IsTriggered.ShouldBeFalse();
    }

    [Theory]
    [InlineData(TransactionChannel.CardNotPresent)]
    [InlineData(TransactionChannel.OnlineBanking)]
    [InlineData(TransactionChannel.DebitOrder)]
    [InlineData(TransactionChannel.Unknown)]
    public void Ignores_a_transaction_whose_channel_implies_no_location(TransactionChannel channel)
    {
        // Buying from a foreign website says nothing about where the cardholder is. Treating it as a
        // location would flag most online shoppers, and Unknown is excluded for the same reason: an
        // unrecognised channel might not imply presence, so assuming it does invents a location.
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithEventId(EventId.From("evt-current"))
            .WithChannel(channel)
            .WithCountry("GB")
            .OccurringAt(Now)
            .Build();

        var context = ContextBuilder.WithHistory(transaction, [CardPresent("ZA", Now.AddMinutes(-5))]);

        _rule.Evaluate(context).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Ignores_an_earlier_transaction_whose_channel_implies_no_location()
    {
        // The mirror case. A card present transaction in South Africa preceded by an online purchase
        // from a British merchant is not travel.
        var earlierOnline = TransactionEventBuilder.AValidEvent()
            .WithEventId(EventId.From("evt-earlier"))
            .WithChannel(TransactionChannel.CardNotPresent)
            .WithCountry("GB")
            .OccurringAt(Now.AddMinutes(-5))
            .Build();

        var context = ContextBuilder.WithHistory(CardPresent("ZA", Now), [earlierOnline]);

        _rule.Evaluate(context).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Flags_two_card_present_transactions_at_the_same_instant_in_different_countries()
    {
        // No speed is fast enough to be in two places at once, so this fires without a speed to quote.
        var context = ContextBuilder.WithHistory(
            CardPresent("ZA", Now),
            [CardPresent("AU", Now)]);

        var outcome = _rule.Evaluate(context);

        outcome.IsTriggered.ShouldBeTrue();
        outcome.Reason.ShouldContain("same moment or earlier");
    }

    [Fact]
    public void Does_not_flag_a_country_it_has_no_coordinates_for()
    {
        // CountryCode validates format but not membership of the real ISO register, so a well formed
        // code that means nothing reaches here. Declining beats guessing at a location.
        var context = ContextBuilder.WithHistory(
            CardPresent("XQ", Now),
            [CardPresent("ZA", Now.AddMinutes(-1))]);

        var outcome = _rule.Evaluate(context);

        outcome.IsTriggered.ShouldBeFalse();
        outcome.Reason.ShouldContain("No coordinates");
    }

    [Fact]
    public void Does_not_flag_when_the_earlier_country_has_no_coordinates()
    {
        var context = ContextBuilder.WithHistory(
            CardPresent("ZA", Now),
            [CardPresent("XQ", Now.AddMinutes(-1))]);

        _rule.Evaluate(context).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Does_not_flag_a_customer_with_no_history()
    {
        _rule.Evaluate(ContextBuilder.For(CardPresent("ZA", Now))).IsTriggered.ShouldBeFalse();
    }

    [Fact]
    public void Finds_the_impossible_pair_among_several_plausible_ones()
    {
        // Only one entry in the history implies impossible travel. The rule must not stop at the first
        // country change it sees and conclude everything is fine.
        var history = new[]
        {
            CardPresent("ZA", Now.AddMinutes(-30)),
            CardPresent("BW", Now.AddHours(-8)),
            CardPresent("AU", Now.AddMinutes(-45)),
            CardPresent("ZA", Now.AddHours(-2)),
        };

        var context = ContextBuilder.WithHistory(CardPresent("ZA", Now), history);

        var outcome = _rule.Evaluate(context);

        outcome.IsTriggered.ShouldBeTrue();
        outcome.Reason.ShouldContain("AU");
    }

    [Fact]
    public void Quotes_the_countries_the_distance_and_the_speed()
    {
        var context = ContextBuilder.WithHistory(
            CardPresent("ZA", Now),
            [CardPresent("GB", Now.AddMinutes(-20))]);

        var reason = _rule.Evaluate(context).Reason;

        reason.ShouldContain("ZA");
        reason.ShouldContain("GB");
        reason.ShouldContain("km apart");
        reason.ShouldContain("km/h");
    }

    [Fact]
    public void Rejects_a_null_context()
    {
        Should.Throw<ArgumentNullException>(() => _rule.Evaluate(null!));
    }

    private static TransactionEvent CardPresent(string country, DateTimeOffset occurredAt) =>
        TransactionEventBuilder.AValidEvent()
            .WithEventId(EventId.From($"evt-{country}-{occurredAt.Ticks}"))
            .WithChannel(TransactionChannel.ChipAndPin)
            .WithCountry(country)
            .OccurringAt(occurredAt)
            .Build();
}
