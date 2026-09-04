using FraudRuleEngine.Application.Abstractions;
using FraudRuleEngine.Application.Diagnostics;
using FraudRuleEngine.Application.Evaluation;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;
using Microsoft.Extensions.Options;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Registers the rule engine. Lives here because the API project is the composition root and the only
/// place entitled to know about every layer.
/// </summary>
internal static class FraudEngineRegistration
{
    /// <remarks>
    /// Rules and scoring are built from the validated <see cref="FraudRuleSetOptions"/>, so their numbers
    /// come from configuration rather than code. Singletons because they hold immutable configuration and
    /// no per request state, which stays true only while they do no IO.
    /// </remarks>
    internal static IServiceCollection AddFraudRuleEngine(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<FraudRuleSetOptions>()
            .Bind(configuration.GetSection(FraudRuleSetOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<FraudRuleSetOptions>, FraudRuleSetOptionsValidator>();

        // The version stamped on every assessment derives from the same options, so it names the numbers
        // the assessment was actually scored under.
        services.AddSingleton<IRuleSetVersionProvider, ConfiguredRuleSetVersionProvider>();

        // Registered as a collection, not just handed to the evaluator, so anything reporting which rules
        // are live reads the same instances that run. Otherwise the two can disagree.
        services.AddSingleton<IReadOnlyList<IFraudRule>>(provider =>
            FraudRuleSetFactory.Rules(Options(provider).Rules));

        services.AddSingleton(provider =>
            new FraudRuleEvaluator(provider.GetRequiredService<IReadOnlyList<IFraudRule>>()));

        services.AddSingleton<IRiskScoringPolicy>(provider =>
            FraudRuleSetFactory.Scoring(Options(provider).Scoring));

        // One meter for the process lifetime.
        services.AddSingleton<FraudMetrics>();

        // Scoped, because it depends on the scoped store and context source.
        services.AddScoped<EvaluateTransactionHandler>();

        return services;
    }

    private static FraudRuleSetOptions Options(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<FraudRuleSetOptions>>().Value;
}
