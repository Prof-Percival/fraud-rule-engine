using System.Globalization;
using System.Threading.RateLimiting;
using FraudRuleEngine.Api.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Partitioned on the client the key belongs to, so one noisy caller cannot spend another's
            // allowance. Unauthenticated requests share one partition; they are refused by authorization
            // anyway, and keying them on the key would let an attacker exhaust a real client's window.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var client = httpContext.User.Identity?.Name ?? "unauthenticated";
                var keys = httpContext.RequestServices
                    .GetRequiredService<IOptionsMonitor<ApiKeyOptions>>().CurrentValue;

                return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = keys.RequestsPerWindow,
                    Window = keys.Window,
                    QueueLimit = 0,
                });
            });

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

                await problems.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Title = "Too many requests.",
                        Detail = "The request allowance for this client has been used up. Retry later.",
                        Status = StatusCodes.Status429TooManyRequests,
                        Type = "https://datatracker.ietf.org/doc/html/rfc9457",
                    },
                }).ConfigureAwait(false);
            };
        });

        return services;
    }
}
