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

    /// <summary>Generous, so the shared tests are never throttled by each other.</summary>
    private const int SharedAllowance = 10_000;

    private PostgreSqlContainer? _container;
    private FraudEngineHost? _host;

    public HttpClient Client { get; private set; } = null!;

    /// <summary>A client with no API key, for the tests that check a request is refused.</summary>
    public HttpClient Anonymous { get; private set; } = null!;

    /// <summary>Exposed so a test needing different settings can raise a host against the same data.</summary>
    public string ConnectionString { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (!string.IsNullOrWhiteSpace(supplied))
        {
            ConnectionString = supplied;
        }
        else
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("fraudengine")
                .WithUsername("fraudengine")
                .WithPassword("fraudengine")
                .Build();

            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

        // A key of our own rather than the development one, so a test cannot pass by accident on a key
        // that only exists for local running.
        _host = new FraudEngineHost(
            ConnectionString,
            SharedAllowance,
            new Dictionary<string, string>(StringComparer.Ordinal) { [ApiKey] = "integration-tests" });

        Client = _host.ClientWithKey(ApiKey);
        Anonymous = _host.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        Anonymous?.Dispose();

        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
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
