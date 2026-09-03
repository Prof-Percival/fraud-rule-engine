using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Application.Abstractions;

/// <summary>
/// Persists a transaction and its assessment.
/// </summary>
/// <remarks>
/// One port taking both rather than a repository each, because they have to be written atomically. An
/// assessment without its transaction cannot be explained, and a transaction without its assessment
/// looks unassessed and would be scored again on a retry. Splitting them into two repositories would
/// put the transactional boundary somewhere the caller has to remember, which is exactly where it gets
/// forgotten.
/// </remarks>
public interface IFraudAssessmentStore
{
    Task SaveAsync(
        TransactionEvent transaction,
        FraudAssessment assessment,
        CancellationToken cancellationToken);
}
