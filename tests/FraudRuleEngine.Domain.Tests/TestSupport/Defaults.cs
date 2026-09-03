using System.Collections.Frozen;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.TestSupport;

/// <summary>
/// Builds domain objects with the shipped reference values, so the numbers live in one place and a test
/// that does not care about a particular threshold need not restate it.
/// </summary>
internal static class Defaults
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

    public static HighValueTransactionRule HighValue() =>
        new(new Dictionary<Currency, Money>
        {
            [Currency.Zar] = new Money(25_000m, Currency.Zar),
            [Currency.Usd] = new Money(1_500m, Currency.Usd),
            [Currency.Eur] = new Money(1_400m, Currency.Eur),
            [Currency.Gbp] = new Money(1_200m, Currency.Gbp),
        });

    public static FirstTimeMerchantHighValueRule FirstTimeMerchantHighValue() =>
        new(
            new Dictionary<Currency, Money>
            {
                [Currency.Zar] = new Money(5_000m, Currency.Zar),
                [Currency.Usd] = new Money(300m, Currency.Usd),
                [Currency.Eur] = new Money(280m, Currency.Eur),
                [Currency.Gbp] = new Money(240m, Currency.Gbp),
            },
            minimumBaselineTransactions: 10);

    public static HighRiskCategoryRule HighRiskCategory() =>
        new(new[]
        {
            TransactionCategory.Gambling,
            TransactionCategory.Cryptocurrency,
            TransactionCategory.InternationalTransfer,
        }.ToFrozenSet());

    public static DeniedMerchantRule DeniedMerchant() =>
        new(new[]
        {
            MerchantId.From("MERCH-DENY-0001"),
            MerchantId.From("MERCH-DENY-0002"),
            MerchantId.From("MERCH-DENY-0003"),
        }.ToFrozenSet());

    public static UnusualHourRule UnusualHour() =>
        new(new TimeOnly(1, 0), new TimeOnly(5, 0));

    public static AmountEscalationRule AmountEscalation() =>
        new(multiple: 5m, minimumBaselineTransactions: 10);

    public static TransactionVelocityRule TransactionVelocity() =>
        new(threshold: 5, window: TimeSpan.FromMinutes(15));

    public static ImpossibleTravelRule ImpossibleTravel() =>
        new(maximumSpeedKilometresPerHour: 1_000, window: TimeSpan.FromHours(12));
}
