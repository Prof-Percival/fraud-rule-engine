using Microsoft.Extensions.Options;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Checks the API keys at startup, so a service that would refuse every caller, or accept a guessable
/// key, fails the boot instead of running.
/// </summary>
internal sealed class ApiKeyOptionsValidator : IValidateOptions<ApiKeyOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();

        if (options.Keys.Count == 0)
        {
            errors.Add("ApiKey:Keys needs at least one key, or every request would be refused.");
        }

        if (options.RequestsPerWindow <= 0)
        {
            errors.Add("ApiKey:RequestsPerWindow must be positive, or no client could make a request.");
        }

        if (options.Window <= TimeSpan.Zero)
        {
            errors.Add("ApiKey:Window must be a positive duration.");
        }

        foreach (var (key, client) in options.Keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add("ApiKey:Keys contains a blank key.");
                continue;
            }

            if (key.Length < ApiKeyOptions.MinimumKeyLength)
            {
                errors.Add(
                    $"ApiKey:Keys contains a key shorter than {ApiKeyOptions.MinimumKeyLength} characters.");
            }

            if (string.IsNullOrWhiteSpace(client))
            {
                errors.Add("ApiKey:Keys has a key with no client name, which nothing could be attributed to.");
            }
        }

        var named = new HashSet<string>(options.Keys.Values, StringComparer.Ordinal);

        foreach (var (client, settings) in options.Clients)
        {
            if (settings.RequestsPerWindow is <= 0)
            {
                errors.Add(
                    $"ApiKey:Clients:{client}:RequestsPerWindow must be positive, or that client could "
                    + "make no request at all. Remove the entry to leave it on the default.");
            }

            // A name here that no key maps to is almost always a typo, and the effect of a typo is that
            // the client silently keeps the default allowance rather than the one written down.
            if (!named.Contains(client))
            {
                errors.Add(
                    $"ApiKey:Clients:{client} has an allowance but no key maps to that client name.");
            }
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
