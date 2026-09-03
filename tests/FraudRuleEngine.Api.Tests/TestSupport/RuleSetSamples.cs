using FraudRuleEngine.Api.Configuration;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Api.Tests.TestSupport;

/// <summary>
/// A valid rule set, so a test that wants one bad setting can start from something that otherwise
/// passes.
/// </summary>
internal static class RuleSetSamples
{
    public static FraudRuleSetOptions Valid()
    {
        var options = new FraudRuleSetOptions
        {
            Version = "test",
            Scoring = new ScoringOptions
            {
                LowSeverityWeight = 10,
                MediumSeverityWeight = 25,
                HighSeverityWeight = 45,
                ReviewThreshold = 40,
                DeclineThreshold = 75,
            },
        };

        options.Rules.HighValueTransaction.Thresholds[Currency.Zar] = 25_000m;
        options.Rules.FirstTimeMerchantHighValue.Thresholds[Currency.Zar] = 5_000m;
        options.Rules.FirstTimeMerchantHighValue.MinimumBaselineTransactions = 10;
        options.Rules.HighRiskCategory.Categories.Add(TransactionCategory.Gambling);
        options.Rules.DeniedMerchant.MerchantIds.Add("MERCH-DENY-0001");
        options.Rules.UnusualHour.WindowStart = new TimeOnly(1, 0);
        options.Rules.UnusualHour.WindowEnd = new TimeOnly(5, 0);
        options.Rules.AmountEscalation.Multiple = 5m;
        options.Rules.AmountEscalation.MinimumBaselineTransactions = 10;
        options.Rules.TransactionVelocity.Threshold = 5;
        options.Rules.TransactionVelocity.Window = TimeSpan.FromMinutes(15);
        options.Rules.ImpossibleTravel.MaximumSpeedKilometresPerHour = 1_000;
        options.Rules.ImpossibleTravel.Window = TimeSpan.FromHours(12);

        return options;
    }
}
