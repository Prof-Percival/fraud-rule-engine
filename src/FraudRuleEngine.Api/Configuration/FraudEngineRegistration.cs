using FraudRuleEngine.Domain.Rules;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Registers the rule engine with the container.
/// </summary>
/// <remarks>
/// Lives in the API project because that is the composition root, and it is the only place entitled
/// to know about every layer. The domain has no dependency on the container, which is enforced by an
/// architecture test rather than trusted.
/// </remarks>
internal static class FraudEngineRegistration
{
    /// <summary>
    /// Registers every fraud rule and the evaluator that runs them.
    /// </summary>
    /// <remarks>
    /// Rules are registered against <see cref="IFraudRule"/> so the evaluator receives them as a
    /// collection and never names one. Adding a rule is one line here, and no existing rule or the
    /// evaluator changes.
    ///
    /// <para>
    /// Everything is a singleton. Rules hold immutable configuration and no per request state, so
    /// there is nothing to isolate between requests and nothing to gain from rebuilding them each
    /// time. That is only true while rules stay free of IO, which is the reason the interface forbids
    /// it.
    /// </para>
    /// </remarks>
    internal static IServiceCollection AddFraudRuleEngine(this IServiceCollection services)
    {
        services.AddSingleton<IFraudRule, HighValueTransactionRule>();
        services.AddSingleton<IFraudRule, HighRiskCategoryRule>();
        services.AddSingleton<IFraudRule, DeniedMerchantRule>();
        services.AddSingleton<IFraudRule, UnusualHourRule>();
        services.AddSingleton<IFraudRule, TransactionVelocityRule>();
        services.AddSingleton<IFraudRule, ImpossibleTravelRule>();

        services.AddSingleton<FraudRuleEvaluator>();

        return services;
    }
}
