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
///
/// <para>
/// The connection string here is never used to connect. Migrations are generated from the model, and the
/// provider only needs to know which SQL dialect to emit.
/// </para>
/// </remarks>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FraudEngineDbContext>
{
    public FraudEngineDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FraudEngineDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time_only;Username=none;Password=none")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new FraudEngineDbContext(options);
    }
}
