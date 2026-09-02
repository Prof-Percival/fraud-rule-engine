using FraudRuleEngine.Domain.Rules;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Registers the rule engine. Lives here because the API project is the composition root and the only
/// place entitled to know about every layer.
/// </summary>
internal static class FraudEngineRegistration
{
    /// <summary>
    /// Rules are registered against the interface, so the evaluator receives them as a collection and
    /// never names one. Adding a rule is one line here.
    /// </summary>
    /// <remarks>
    /// Singletons because rules hold immutable configuration and no per request state. That is only true
    /// while they stay free of IO, which is what the interface forbids.
    /// </remarks>
    internal static IServiceCollection AddFraudRuleEngine(this IServiceCollection services)
    {
        services.AddSingleton<IFraudRule, HighValueTransactionRule>();
        services.AddSingleton<IFraudRule, HighRiskCategoryRule>();
        services.AddSingleton<IFraudRule, DeniedMerchantRule>();
        services.AddSingleton<IFraudRule, UnusualHourRule>();
        services.AddSingleton<IFraudRule, TransactionVelocityRule>();
        services.AddSingleton<IFraudRule, ImpossibleTravelRule>();
        services.AddSingleton<IFraudRule, FirstTimeMerchantHighValueRule>();
        services.AddSingleton<IFraudRule, AmountEscalationRule>();

        services.AddSingleton<FraudRuleEvaluator>();

        return services;
    }
}
