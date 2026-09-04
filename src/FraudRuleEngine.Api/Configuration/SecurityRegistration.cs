using Microsoft.Extensions.Options;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Binds and validates the API keys.
/// </summary>
internal static class SecurityRegistration
{
    internal static IServiceCollection AddFraudEngineSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ApiKeyOptions>()
            .Bind(configuration.GetSection(ApiKeyOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ApiKeyOptions>, ApiKeyOptionsValidator>();

        return services;
    }
}
