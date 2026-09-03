using System.Collections.Frozen;
using FraudRuleEngine.Application.Evaluation;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Registers the rule engine. Lives here because the API project is the composition root and the only
/// place entitled to know about every layer.
/// </summary>
internal static class FraudEngineRegistration
{
    /// <summary>
    /// Rules are registered against the interface, so the evaluator receives them as a collection and
    /// never names one.
    /// </summary>
    /// <remarks>
    /// Singletons because rules hold immutable configuration and no per request state. That is only true
    /// while they stay free of IO, which is what the interface forbids.
    /// </remarks>
    internal static IServiceCollection AddFraudRuleEngine(this IServiceCollection services)
    {
        services.AddSingleton<IFraudRule>(_ => new HighValueTransactionRule(
            new Dictionary<Currency, Money>
            {
                [Currency.Zar] = new Money(25_000m, Currency.Zar),
                [Currency.Usd] = new Money(1_500m, Currency.Usd),
                [Currency.Eur] = new Money(1_400m, Currency.Eur),
                [Currency.Gbp] = new Money(1_200m, Currency.Gbp),
            }));

        services.AddSingleton<IFraudRule>(_ => new HighRiskCategoryRule(new[]
        {
            TransactionCategory.Gambling,
            TransactionCategory.Cryptocurrency,
            TransactionCategory.InternationalTransfer,
        }.ToFrozenSet()));

        services.AddSingleton<IFraudRule>(_ => new DeniedMerchantRule(new[]
        {
            MerchantId.From("MERCH-DENY-0001"),
            MerchantId.From("MERCH-DENY-0002"),
            MerchantId.From("MERCH-DENY-0003"),
        }.ToFrozenSet()));

        services.AddSingleton<IFraudRule>(_ => new UnusualHourRule(new TimeOnly(1, 0), new TimeOnly(5, 0)));

        services.AddSingleton<IFraudRule>(_ => new TransactionVelocityRule(
            threshold: 5,
            window: TimeSpan.FromMinutes(15)));

        services.AddSingleton<IFraudRule>(_ => new ImpossibleTravelRule(
            maximumSpeedKilometresPerHour: 1_000,
            window: TimeSpan.FromHours(12)));

        services.AddSingleton<IFraudRule>(_ => new FirstTimeMerchantHighValueRule(
            new Dictionary<Currency, Money>
            {
                [Currency.Zar] = new Money(5_000m, Currency.Zar),
                [Currency.Usd] = new Money(300m, Currency.Usd),
                [Currency.Eur] = new Money(280m, Currency.Eur),
                [Currency.Gbp] = new Money(240m, Currency.Gbp),
            },
            minimumBaselineTransactions: 10));

        services.AddSingleton<IFraudRule>(_ => new AmountEscalationRule(
            multiple: 5m,
            minimumBaselineTransactions: 10));

        services.AddSingleton<FraudRuleEvaluator>();

        services.AddSingleton<IRiskScoringPolicy>(_ => new WeightedRiskScoringPolicy(
            new Dictionary<RuleSeverity, int>
            {
                [RuleSeverity.Low] = 10,
                [RuleSeverity.Medium] = 25,
                [RuleSeverity.High] = 45,
            },
            reviewThreshold: new RiskScore(40),
            declineThreshold: new RiskScore(75)));

        // Scoped, because it depends on the scoped store and context source.
        services.AddScoped<EvaluateTransactionHandler>();

        return services;
    }
}
