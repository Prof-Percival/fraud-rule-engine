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

    /// <summary>Requests a client may make per window when it has no allowance of its own.</summary>
    public int RequestsPerWindow { get; set; } = 6_000;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Keys mapped to the client they belong to. The client name is what the logs and the rate limiter
    /// partition on, so a key can be revoked and traffic attributed without the key itself being logged.
    /// </summary>
    public Dictionary<string, string> Keys { get; } = new(StringComparer.Ordinal);

    /// <summary>Allowances for individual clients, keyed by the client name their keys map to.</summary>
    public Dictionary<string, ClientOptions> Clients { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The allowance in force for one client.
    /// </summary>
    /// <remarks>
    /// Resolved by client rather than by key, so the two keys a client holds during a rotation share one
    /// allowance instead of doubling what it may send.
    /// </remarks>
    public int AllowanceFor(string client) =>
        Clients.TryGetValue(client, out var settings) && settings.RequestsPerWindow is { } allowance
            ? allowance
            : RequestsPerWindow;
}

/// <summary>
/// What one client may send. Absent values leave it on the defaults.
/// </summary>
public sealed class ClientOptions
{
    public int? RequestsPerWindow { get; set; }
}
