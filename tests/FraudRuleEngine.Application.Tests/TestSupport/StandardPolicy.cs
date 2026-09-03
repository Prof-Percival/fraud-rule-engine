using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;

namespace FraudRuleEngine.Application.Tests.TestSupport;

/// <summary>
/// The scoring policy with its shipped weights and thresholds, so handler tests need not restate them.
/// </summary>
internal static class StandardPolicy
{
    public static WeightedRiskScoringPolicy Scoring() =>
        new(
            new Dictionary<RuleSeverity, int>
            {
                [RuleSeverity.Low] = 10,
                [RuleSeverity.Medium] = 25,
                [RuleSeverity.High] = 45,
            },
            reviewThreshold: new RiskScore(40),
            declineThreshold: new RiskScore(75));
}
