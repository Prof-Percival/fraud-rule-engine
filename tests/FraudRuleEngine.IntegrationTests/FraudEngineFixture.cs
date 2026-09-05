using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace FraudRuleEngine.IntegrationTests;

/// <summary>
/// A real database and the real HTTP pipeline, shared by every test in the collection.
/// </summary>
/// <remarks>
/// The database comes from <c>ConnectionStrings__Default</c> when it is set, which is the compose and CI
/// path, and otherwise from a throwaway container, which is the path when running from an IDE. Same test
/// code either way.
/// </remarks>
public sealed class FraudEngineFixture : IAsyncLifetime
{
    public const string ApiKey = "integration-test-key-0001";

    private PostgreSqlContainer? _container;
    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = null!;

    /// <summary>A client with no API key, for the tests that check a request is refused.</summary>
    public HttpClient Anonymous { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        string connectionString;

        if (!string.IsNullOrWhiteSpace(supplied))
        {
            connectionString = supplied;
        }
        else
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("fraudengine")
                .WithUsername("fraudengine")
                .WithPassword("fraudengine")
                .Build();

            await _container.StartAsync();
            connectionString = _container.GetConnectionString();
        }

        _factory = new Factory(connectionString);

        // Development, so the host applies migrations on start and the schema exists before the first
        // request. Creating the client is what builds the host.
        Client = _factory.CreateClient();
        Client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        Anonymous = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        Anonymous?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);

            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = connectionString,

                    // A key of our own rather than the development one, so a test cannot pass by
                    // accident on a key that only exists for local running.
                    [$"ApiKey:Keys:{ApiKey}"] = "integration-tests",

                    // Generous, so the ordinary tests are not throttled. The rate limit test sets its
                    // own allowance.
                    ["ApiKey:RequestsPerWindow"] = "10000",
                }));

            return base.CreateHost(builder);
        }
    }
}

/// <summary>
/// One fixture for the whole suite, so the container starts once rather than per class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SharedFraudEngine : ICollectionFixture<FraudEngineFixture>
{
    public const string Name = "fraud engine";
}
