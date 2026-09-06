using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FraudRuleEngine.Infrastructure.Persistence;

/// <summary>
/// Builds a context for the EF Core tooling, so generating a migration does not need a running database
/// or a bootable application.
/// </summary>
/// <remarks>
/// Without this, <c>dotnet ef</c> starts the API host to find the context, which means design time work
/// depends on configuration and the whole dependency graph resolving. That fails for unrelated reasons
/// and is confusing when it does.
/// </remarks>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FraudEngineDbContext>
{
    private const string ModelOnlyPlaceholder =
        "Host=localhost;Database=design_time_only;Username=none;Password=none";

    public FraudEngineDbContext CreateDbContext(string[] args)
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        var options = new DbContextOptionsBuilder<FraudEngineDbContext>()
            .UseNpgsql(string.IsNullOrWhiteSpace(configured) ? ModelOnlyPlaceholder : configured)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new FraudEngineDbContext(options);
    }
}
