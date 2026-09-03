using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Application.Abstractions;

/// <summary>
/// Persists a transaction and its assessment.
/// </summary>
/// <remarks>
/// One port taking both rather than a repository each, because they have to be written atomically. An
/// assessment without its transaction cannot be explained, and a transaction without its assessment
/// looks unassessed and would be scored again on a retry.
/// </remarks>
public interface IFraudAssessmentStore
{
    /// <summary>
    /// Writes both, or reports that this event was already assessed.
    /// </summary>
    /// <remarks>
    /// Reporting the clash rather than throwing, because a duplicate delivery is expected traffic and not
    /// an error. Producers retry, and at least once delivery means the same event will arrive twice.
    /// </remarks>
    Task<SaveResult> SaveAsync(
        TransactionEvent transaction,
        FraudAssessment assessment,
        CancellationToken cancellationToken);

    /// <summary>The assessment already made for this event, if there is one.</summary>
    Task<FraudAssessment?> FindByEventAsync(EventId eventId, CancellationToken cancellationToken);
}

public enum SaveResult
{
    Saved = 0,

    /// <summary>
    /// This event had already been assessed, so nothing was written.
    /// </summary>
    /// <remarks>
    /// Detected by the database rejecting the write, not by looking first. A read before write would let
    /// two concurrent deliveries of one event both pass the check and then both try to insert.
    /// </remarks>
    AlreadyAssessed,
}
