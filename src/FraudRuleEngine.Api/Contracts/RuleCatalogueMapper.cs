using System.Globalization;
using FraudRuleEngine.Api.Configuration;
using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Api.Contracts;

/// <summary>
/// Describes the live rule set for the catalogue endpoint.
/// </summary>
internal static class RuleCatalogueMapper
{
    /// <remarks>
    /// Rule identity comes from the registered rules rather than from configuration, so the endpoint
    /// reports what is running rather than what was asked for. A rule with no settings listed here still
    /// appears, because leaving it out would understate the set.
    /// </remarks>
    public static RuleCatalogueResponse Map(
        IReadOnlyList<IFraudRule> rules,
        FraudRuleSetOptions options,
        RuleSetVersion version)
    {
        var settings = SettingsByRuleId(options.Rules);

        return new RuleCatalogueResponse
        {
            RuleSetVersion = version.Value,
            Scoring = new ScoringResponse
            {
                LowSeverityWeight = options.Scoring.LowSeverityWeight,
                MediumSeverityWeight = options.Scoring.MediumSeverityWeight,
                HighSeverityWeight = options.Scoring.HighSeverityWeight,
                ReviewThreshold = options.Scoring.ReviewThreshold,
                DeclineThreshold = options.Scoring.DeclineThreshold,
            },
            Rules =
            [
                .. rules
                    .Select(rule => rule.Id.Value)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .Select(id => new RuleResponse
                    {
                        Id = id,
                        Settings = settings.GetValueOrDefault(id)
                            ?? new Dictionary<string, string>(StringComparer.Ordinal),
                    }),
            ],
        };
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>> SettingsByRuleId(
        RulesOptions rules) =>
        new(StringComparer.Ordinal)
        {
            ["HighValueTransaction"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Thresholds"] = Amounts(rules.HighValueTransaction.Thresholds),
            },
            ["FirstTimeMerchantHighValue"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Thresholds"] = Amounts(rules.FirstTimeMerchantHighValue.Thresholds),
                ["MinimumBaselineTransactions"] =
                    Text(rules.FirstTimeMerchantHighValue.MinimumBaselineTransactions),
            },
            ["HighRiskCategory"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Categories"] = string.Join(", ", rules.HighRiskCategory.Categories),
            },
            ["DeniedMerchant"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MerchantCount"] = Text(rules.DeniedMerchant.MerchantIds.Count),
            },
            ["UnusualHour"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Window"] = $"{rules.UnusualHour.WindowStart:HH\\:mm} to {rules.UnusualHour.WindowEnd:HH\\:mm} local",
            },
            ["AmountEscalation"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Multiple"] = Text(rules.AmountEscalation.Multiple),
                ["MinimumBaselineTransactions"] = Text(rules.AmountEscalation.MinimumBaselineTransactions),
            },
            ["TransactionVelocity"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Threshold"] = Text(rules.TransactionVelocity.Threshold),
                ["Window"] = Text(rules.TransactionVelocity.Window),
            },
            ["ImpossibleTravel"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MaximumSpeedKilometresPerHour"] =
                    Text(rules.ImpossibleTravel.MaximumSpeedKilometresPerHour),
                ["Window"] = Text(rules.ImpossibleTravel.Window),
            },
        };

    // The deny list is reported as a count, not as the merchants themselves. Handing out who is blocked
    // tells anyone with a key which merchants to avoid using.
    private static string Amounts(Dictionary<Currency, decimal> thresholds) =>
        string.Join(
            ", ",
            thresholds
                .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                .Select(entry => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{entry.Key.ToString().ToUpperInvariant()} {entry.Value}")));

    private static string Text<T>(T value) where T : IFormattable =>
        value.ToString(null, CultureInfo.InvariantCulture);
}
