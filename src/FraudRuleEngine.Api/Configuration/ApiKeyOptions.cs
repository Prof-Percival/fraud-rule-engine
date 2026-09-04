namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// The API keys accepted on the data endpoints, bound from the <c>ApiKey</c> configuration section.
/// </summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    public const string HeaderName = "X-Api-Key";

    /// <summary>Minimum key length, long enough that a key cannot be guessed by hand.</summary>
    public const int MinimumKeyLength = 16;

    /// <summary>Requests one client may make per window.</summary>
    public int RequestsPerWindow { get; set; } = 100;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Keys mapped to the client they belong to. The client name is what the logs and the rate limiter
    /// partition on, so a key can be revoked and traffic attributed without the key itself being logged.
    /// </summary>
    public Dictionary<string, string> Keys { get; } = new(StringComparer.Ordinal);
}
