using FraudRuleEngine.Application.Abstractions;
using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FraudRuleEngine.Infrastructure.Persistence;

internal sealed class FraudAssessmentStore : IFraudAssessmentStore
{
    /// <summary>PostgreSQL's SQLSTATE for a unique constraint violation.</summary>
    private const string UniqueViolation = "23505";

    private readonly FraudEngineDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public FraudAssessmentStore(FraudEngineDbContext dbContext, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<SaveResult> SaveAsync(
        TransactionEvent transaction,
        FraudAssessment assessment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(assessment);

        _dbContext.Transactions.Add(StoredRecordMapper.ToStored(transaction, _timeProvider.GetUtcNow()));
        _dbContext.Assessments.Add(StoredRecordMapper.ToStored(assessment));

        try
        {
            // One SaveChanges, so both land or neither does. EF Core wraps a single call in a
            // transaction, which is what makes the atomicity this port promises real.
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Nothing written is read back through the tracker, and a batch reuses one scoped context for
            // every item. Holding the saved entities would make a repeated event id inside a batch fail on
            // a tracking collision before reaching the database, which hides it from the duplicate path
            // below, and would leave change detection walking every earlier item on each save.
            _dbContext.ChangeTracker.Clear();

            return SaveResult.Saved;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // Letting the insert fail is the detection mechanism, not a fallback for one. Checking first
            // and inserting second leaves a window where two concurrent deliveries of the same event both
            // find nothing and both proceed; only the constraint closes it.
            //
            // The context is left holding the rejected entities, so it is reset before anything else uses
            // it. Without this, the next SaveChanges on this scoped context would retry the same failing
            // insert.
            _dbContext.ChangeTracker.Clear();

            return SaveResult.AlreadyAssessed;
        }
    }

    public async Task<FraudAssessment?> FindByEventAsync(
        EventId eventId,
        CancellationToken cancellationToken)
    {
        var id = eventId.Value;

        var stored = await _dbContext.Assessments
            .AsNoTracking()
            .Where(assessment => assessment.EventId == id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return null;
        }

        var outcomes = await _dbContext.RuleOutcomes
            .AsNoTracking()
            .Where(outcome => outcome.AssessmentId == stored.Id)
            .OrderBy(outcome => outcome.Ordinal)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return StoredRecordMapper.ToDomain(stored, outcomes);
    }
}
