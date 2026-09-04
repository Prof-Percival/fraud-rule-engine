using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using FraudRuleEngine.Api.Configuration;
using Microsoft.Extensions.Options;

namespace FraudRuleEngine.Api.Middleware;

/// <summary>
/// Resolves a presented API key to the client it belongs to.
/// </summary>
/// <remarks>
/// Keys are hashed once when the configuration is read and looked up by digest, so a request costs one
/// hash and one lookup rather than a comparison against every configured key. Only the digests are held,
/// so the raw keys are not sitting in memory to be read out of a dump.
/// </remarks>
internal sealed class ApiKeyRegistry
{
    private readonly IOptionsMonitor<ApiKeyOptions> _options;
    private FrozenDictionary<string, string> _clientsByDigest;
    private ApiKeyOptions? _source;

    public ApiKeyRegistry(IOptionsMonitor<ApiKeyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _clientsByDigest = FrozenDictionary<string, string>.Empty;
    }

    /// <returns>The client the key belongs to, or null when the key is not recognised.</returns>
    public string? ClientFor(string presentedKey)
    {
        var current = _options.CurrentValue;

        // Rebuilt only when the configuration object changes, which is on reload rather than per request.
        if (!ReferenceEquals(_source, current))
        {
            _clientsByDigest = current.Keys.ToFrozenDictionary(
                entry => Digest(entry.Key),
                entry => entry.Value,
                StringComparer.Ordinal);

            _source = current;
        }

        return _clientsByDigest.GetValueOrDefault(Digest(presentedKey));
    }

    private static string Digest(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
