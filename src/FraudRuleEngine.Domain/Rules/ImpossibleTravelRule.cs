using System.Collections.Frozen;
using System.Globalization;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Flags a card used in two countries too far apart for the cardholder to have travelled between them.
/// </summary>
/// <remarks>
/// Close to conclusive rather than probabilistic, hence <see cref="RuleSeverity.High"/>. A card present
/// in Johannesburg and then in London twenty minutes later means one of the two was not the cardholder.
///
/// <para>
/// The limitation is the coordinate data rather than the logic. Country centroids make Johannesburg and
/// Cape Town the same point, and a card used either side of the Beitbridge border reads as a nine
/// hundred kilometre journey. City level geolocation is the fix; the speed ceiling is set generously in
/// the meantime. See <see cref="CountryLocations"/>.
/// </para>
/// </remarks>
public sealed class ImpossibleTravelRule : IFraudRule
{
    /// <summary>
    /// A commercial jet cruises around 900. A thousand allows for tailwinds and for terminal clocks that
    /// are not perfectly aligned. Generous on purpose, since a false positive here inconveniences a real
    /// customer.
    /// </summary>
    private readonly double _maximumSpeedKilometresPerHour = 1_000;

    private readonly TimeSpan _window = TimeSpan.FromHours(12);

    /// <summary>
    /// Channels where the card must have been physically present. Unknown is excluded, because assuming
    /// presence would invent a location.
    /// </summary>
    private readonly FrozenSet<TransactionChannel> _cardPresentChannels = new[]
    {
        TransactionChannel.ChipAndPin,
        TransactionChannel.Contactless,
        TransactionChannel.Atm,
        TransactionChannel.Branch,
    }.ToFrozenSet();

    public RuleId Id { get; } = RuleId.From("ImpossibleTravel");

    public RuleOutcome Evaluate(FraudEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var transaction = context.Transaction;

        // An online purchase says nothing about where the cardholder is, so treating a card not present
        // transaction as a location would flag anyone who buys from a foreign website.
        if (!_cardPresentChannels.Contains(transaction.Channel))
        {
            return RuleOutcome.Clear(
                Id,
                $"Channel {transaction.Channel} does not require the card to be present, so it "
                    + "implies no location.");
        }

        if (!CountryLocations.TryGetCentroid(transaction.Country, out var here))
        {
            return RuleOutcome.Clear(
                Id,
                $"No coordinates are known for {transaction.Country}, so no distance can be measured.");
        }

        // Scans the whole window rather than stopping at the first country change, since one plausible
        // hop does not mean the rest are.
        foreach (var previous in context.History.Within(_window, transaction.OccurredAtUtc))
        {
            if (!_cardPresentChannels.Contains(previous.Channel)
                || previous.Country == transaction.Country
                || !CountryLocations.TryGetCentroid(previous.Country, out var there))
            {
                continue;
            }

            var distanceKm = here.DistanceInKilometresTo(there);
            var elapsed = transaction.OccurredAtUtc - previous.OccurredAtUtc;

            // No speed is sufficient to be in two places at once.
            if (elapsed <= TimeSpan.Zero)
            {
                return Impossible(transaction, previous, distanceKm, speed: null);
            }

            var speedKmh = distanceKm / elapsed.TotalHours;

            if (speedKmh > _maximumSpeedKilometresPerHour)
            {
                return Impossible(transaction, previous, distanceKm, speedKmh);
            }
        }

        return RuleOutcome.Clear(
            Id,
            $"No earlier card present transaction in another country within the last "
                + $"{_window.TotalHours:F0} hours implies impossible travel.");
    }

    private RuleOutcome Impossible(
        TransactionEvent transaction,
        TransactionEvent previous,
        double distanceKm,
        double? speed)
    {
        var speedText = speed is null
            ? "at the same moment or earlier"
            : string.Create(CultureInfo.InvariantCulture, $"implying {speed.Value:F0} km/h");

        return RuleOutcome.Triggered(
            Id,
            RuleSeverity.High,
            string.Create(
                CultureInfo.InvariantCulture,
                $"Card present in {previous.Country} at {previous.OccurredAtUtc:u} and in "
                    + $"{transaction.Country} at {transaction.OccurredAtUtc:u}, "
                    + $"{distanceKm:F0} km apart, {speedText}, against a maximum of "
                    + $"{_maximumSpeedKilometresPerHour:F0} km/h."));
    }
}
