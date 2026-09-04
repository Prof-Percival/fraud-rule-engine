using FraudRuleEngine.Api.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Binds the API keys and requires one on every endpoint that is not explicitly anonymous.
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

        services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                configureOptions: null);

        // A fallback policy rather than per route metadata, so a new endpoint is protected by default and
        // the two routes mapped outside a group are covered too. Opting out has to be deliberate.
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }
}
