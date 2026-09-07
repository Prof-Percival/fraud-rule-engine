using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FraudRuleEngine.IntegrationTests;

/// <summary>
/// The application, wired to a database and to keys the caller chooses.
/// </summary>
/// <remarks>
/// Separate from the fixture because the rate limit tests need a host whose allowance is small enough to
/// exhaust on purpose, and giving them one of their own keeps them from spending the allowance every
/// other test in the collection is sharing.
/// </remarks>
internal sealed class FraudEngineHost(
    string connectionString,
    int requestsPerWindow,
    IReadOnlyDictionary<string, string> keys,
    IReadOnlyDictionary<string, int>? allowances = null) : WebApplicationFactory<Program>
{
    public HttpClient ClientWithKey(string apiKey)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        return client;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Development, so migrations are applied on start and the schema is there before the first
        // request. Creating a client is what builds the host.
        builder.UseEnvironment(Environments.Development);

        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["ApiKey:RequestsPerWindow"] = requestsPerWindow.ToString(CultureInfo.InvariantCulture),
        };

        foreach (var (key, client) in keys)
        {
            settings[$"ApiKey:Keys:{key}"] = client;
        }

        foreach (var (client, allowance) in allowances ?? ReadOnlyDictionary<string, int>.Empty)
        {
            settings[$"ApiKey:Clients:{client}:RequestsPerWindow"] =
                allowance.ToString(CultureInfo.InvariantCulture);
        }

        builder.ConfigureHostConfiguration(configuration =>
            configuration.AddInMemoryCollection(settings));

        return base.CreateHost(builder);
    }
}
