using FraudRuleEngine.Application.Abstractions;
using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FraudRuleEngine.Infrastructure.Persistence;

internal sealed class FraudAssessmentStore : IFraudAssessmentStore
{
    private readonly FraudEngineDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public FraudAssessmentStore(FraudEngineDbContext dbContext, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task SaveAsync(
        TransactionEvent transaction,
        FraudAssessment assessment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(assessment);

        _dbContext.Transactions.Add(StoredRecordMapper.ToStored(transaction, _timeProvider.GetUtcNow()));
        _dbContext.Assessments.Add(StoredRecordMapper.ToStored(assessment));

        // One SaveChanges, so both land or neither does. EF Core wraps a single call in a transaction,
        // which is what makes the atomicity this port promises real rather than assumed.
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
