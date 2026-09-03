using FraudRuleEngine.Application.Abstractions;
using FraudRuleEngine.Infrastructure.Configuration;
using FraudRuleEngine.Infrastructure.Enrichment;
using FraudRuleEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FraudRuleEngine.Infrastructure;

public static class InfrastructureRegistration
{
    /// <summary>
    /// Registers persistence and enrichment against the ports the application layer declares.
    /// </summary>
    /// <remarks>
    /// The DbContext and everything holding one are scoped, because a DbContext tracks changes and is not
    /// safe to share across requests. That is the opposite of the rules, which are singletons precisely
    /// because they hold no state.
    /// </remarks>
    public static IServiceCollection AddFraudEnginePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<FraudEngineDbContext>(options => options
            .UseNpgsql(connectionString)
            // PostgreSQL folds unquoted identifiers to lower case, so PascalCase columns would have to be
            // quoted in every hand written query anybody ever runs against this database.
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IFraudAssessmentStore, FraudAssessmentStore>();
        services.AddScoped<ICustomerContextSource, CustomerContextSource>();
        services.AddSingleton<IRuleSetVersionProvider, FixedRuleSetVersionProvider>();

        return services;
    }

    /// <summary>Applies any pending migrations. Intended for development only.</summary>
    /// <remarks>
    /// Deliberately not called in production. An application that migrates its own schema on boot will
    /// fight itself the moment it runs more than one replica, and a failed migration takes the process
    /// down rather than being something an operator can retry.
    /// </remarks>
    public static async Task ApplyMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FraudEngineDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
