using FraudRuleEngine.Application.Abstractions;
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

        services.AddSingleton(provider =>
            new FraudRuleEvaluator(FraudRuleSetFactory.Rules(Options(provider).Rules)));

        services.AddSingleton<IRiskScoringPolicy>(provider =>
            FraudRuleSetFactory.Scoring(Options(provider).Scoring));

        // Scoped, because it depends on the scoped store and context source.
        services.AddScoped<EvaluateTransactionHandler>();

        return services;
    }

    private static FraudRuleSetOptions Options(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<FraudRuleSetOptions>>().Value;
}
