using System.Security.Claims;
using System.Text.Encodings.Web;
using FraudRuleEngine.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FraudRuleEngine.Api.Middleware;

/// <summary>
/// Authenticates a request by its <c>X-Api-Key</c> header.
/// </summary>
internal sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    internal const string SchemeName = "ApiKey";

    private readonly ApiKeyRegistry _registry;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ApiKeyRegistry registry,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyOptions.HeaderName, out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var presented = header.ToString();

        if (string.IsNullOrWhiteSpace(presented))
        {
            return Task.FromResult(AuthenticateResult.Fail("No API key was supplied."));
        }

        var client = _registry.ClientFor(presented);

        if (client is null)
        {
            // The key is never logged, even on failure. A rejected key is often a real key sent to the
            // wrong environment, and a log is a poor place to keep one.
            return Task.FromResult(AuthenticateResult.Fail("The API key is not recognised."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, client)],
            SchemeName);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

}
