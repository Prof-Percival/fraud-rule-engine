using Microsoft.EntityFrameworkCore;

namespace FraudRuleEngine.Infrastructure.Persistence;

internal sealed class FraudEngineDbContext : DbContext
{
    public FraudEngineDbContext(DbContextOptions<FraudEngineDbContext> options)
        : base(options)
    {
    }

    public DbSet<StoredTransaction> Transactions => Set<StoredTransaction>();

    public DbSet<StoredAssessment> Assessments => Set<StoredAssessment>();

    public DbSet<StoredRuleOutcome> RuleOutcomes => Set<StoredRuleOutcome>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuration lives in one class per entity rather than in overrides here, so this file stays
        // a table of contents and the mapping for a table is where somebody would look for it.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FraudEngineDbContext).Assembly);
    }
}
