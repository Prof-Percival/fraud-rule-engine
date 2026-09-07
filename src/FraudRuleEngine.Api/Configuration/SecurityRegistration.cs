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

        services.AddSingleton<ApiKeyRegistry>();

        services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                configureOptions: null);

        // A fallback policy rather than per route metadata, so an endpoint added later is protected by
        // default and the routes mapped outside a group are covered too. Naming no scheme is deliberate:
        // naming one makes authorization authenticate a second time.
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
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

                var allowance = keys.AllowanceFor(client);
                var window = keys.Window;

                // The numbers belong in the partition key, not only in the options handed to the factory.
                // A partition's limiter is built once and reused for the life of the process, so a client
                // already sending would otherwise keep the allowance it started with and a change to
                // configuration would appear to do nothing.
                var partition = string.Create(
                    CultureInfo.InvariantCulture, $"{client}|{allowance}|{window}");

                return RateLimitPartition.GetFixedWindowLimiter(
                    partition,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = allowance,
                        Window = window,
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
