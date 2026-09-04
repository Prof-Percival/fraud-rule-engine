using FraudRuleEngine.Api.Configuration;
using FraudRuleEngine.Api.Middleware;
using Microsoft.Extensions.Options;

namespace FraudRuleEngine.Api.Tests.Middleware;

public sealed class ApiKeyRegistryTests
{
    private const string Key = "0123456789abcdefgh";

    [Fact]
    public void Resolves_a_configured_key_to_its_client()
    {
        Registry(Key, "payments").ClientFor(Key).ShouldBe("payments");
    }

    [Fact]
    public void Does_not_resolve_an_unknown_key()
    {
        Registry(Key, "payments").ClientFor("some-other-key-value").ShouldBeNull();
    }

    [Fact]
    public void Is_case_sensitive()
    {
        Registry(Key, "payments").ClientFor(Key.ToUpperInvariant()).ShouldBeNull();
    }

    [Fact]
    public void Does_not_resolve_an_empty_key()
    {
        Registry(Key, "payments").ClientFor("").ShouldBeNull();
    }

    [Fact]
    public void Picks_up_a_changed_configuration()
    {
        // Keys are hashed once and cached, so a reload has to rebuild rather than keep serving the old set.
        var monitor = new MutableMonitor(Options(Key, "payments"));
        var registry = new ApiKeyRegistry(monitor);

        registry.ClientFor(Key).ShouldBe("payments");

        monitor.Current = Options("a-replacement-key-value", "ledger");

        registry.ClientFor(Key).ShouldBeNull();
        registry.ClientFor("a-replacement-key-value").ShouldBe("ledger");
    }

    private static ApiKeyRegistry Registry(string key, string client) =>
        new(new MutableMonitor(Options(key, client)));

    private static ApiKeyOptions Options(string key, string client)
    {
        var options = new ApiKeyOptions();
        options.Keys[key] = client;

        return options;
    }

    private sealed class MutableMonitor(ApiKeyOptions value) : IOptionsMonitor<ApiKeyOptions>
    {
        public ApiKeyOptions Current { get; set; } = value;

        public ApiKeyOptions CurrentValue => Current;

        public ApiKeyOptions Get(string? name) => Current;

        public IDisposable? OnChange(Action<ApiKeyOptions, string?> listener) => null;
    }
}
