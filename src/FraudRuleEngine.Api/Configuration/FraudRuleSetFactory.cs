using System.Collections.Frozen;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Turns the validated options into domain objects. The options carry primitives a configuration file
/// can express; the domain wants its own value types, and this is the one place that bridges them.
/// </summary>
internal static class FraudRuleSetFactory
{
    public static WeightedRiskScoringPolicy Scoring(ScoringOptions scoring) =>
        new(
            new Dictionary<RuleSeverity, int>
            {
                [RuleSeverity.Low] = scoring.LowSeverityWeight,
                [RuleSeverity.Medium] = scoring.MediumSeverityWeight,
                [RuleSeverity.High] = scoring.HighSeverityWeight,
            },
            new RiskScore(scoring.ReviewThreshold),
            new RiskScore(scoring.DeclineThreshold));

    public static IReadOnlyList<IFraudRule> Rules(RulesOptions rules) =>
    [
        new HighValueTransactionRule(Money(rules.HighValueTransaction.Thresholds)),
        new HighRiskCategoryRule(rules.HighRiskCategory.Categories.ToFrozenSet()),
        new DeniedMerchantRule(rules.DeniedMerchant.MerchantIds.Select(MerchantId.From).ToFrozenSet()),
        new UnusualHourRule(rules.UnusualHour.WindowStart, rules.UnusualHour.WindowEnd),
        new TransactionVelocityRule(rules.TransactionVelocity.Threshold, rules.TransactionVelocity.Window),
        new ImpossibleTravelRule(
            rules.ImpossibleTravel.MaximumSpeedKilometresPerHour,
            rules.ImpossibleTravel.Window),
        new FirstTimeMerchantHighValueRule(
            Money(rules.FirstTimeMerchantHighValue.Thresholds),
            rules.FirstTimeMerchantHighValue.MinimumBaselineTransactions),
        new AmountEscalationRule(
            rules.AmountEscalation.Multiple,
            rules.AmountEscalation.MinimumBaselineTransactions),
    ];

    private static Dictionary<Currency, Money> Money(Dictionary<Currency, decimal> thresholds) =>
        thresholds.ToDictionary(entry => entry.Key, entry => new Money(entry.Value, entry.Key));
}
