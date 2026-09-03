using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Assessments;

/// <summary>
/// The engine's verdict on one transaction, and the evidence for it.
/// </summary>
/// <remarks>
/// Carries every rule outcome, not only the ones that fired, along with the score, the decision and
/// the version of the rule set that produced them. That is enough to reconstruct the reasoning
/// afterwards without re-running anything, which is the whole point: a flag a human has to action, or
/// a decline a customer disputes, has to be explainable months later.
/// </remarks>
public sealed record FraudAssessment
{
    private readonly RuleOutcome[] _ruleOutcomes;

    /// <exception cref="ArgumentException">
    /// An identifier is uninitialised, or no rule outcomes were supplied.
    /// </exception>
    public FraudAssessment(
        AssessmentId id,
        TransactionEvent transaction,
        RiskScore riskScore,
        FraudDecision decision,
        IReadOnlyList<RuleOutcome> ruleOutcomes,
        RuleSetVersion ruleSetVersion,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(ruleOutcomes);

        if (!id.IsInitialised)
        {
            throw new ArgumentException("An assessment must have an identifier.", nameof(id));
        }

        if (!ruleSetVersion.IsInitialised)
        {
            throw new ArgumentException(
                "An assessment must record the rule set version that produced it.",
                nameof(ruleSetVersion));
        }

        // An assessment with no outcomes claims a verdict nothing supports. It would also be
        // indistinguishable from one where every rule was disabled.
        if (ruleOutcomes.Count == 0)
        {
            throw new ArgumentException(
                "An assessment must record the outcome of every rule that ran.",
                nameof(ruleOutcomes));
        }

        if (evaluatedAt == default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evaluatedAt),
                evaluatedAt,
                "An assessment must record when it was made.");
        }

        Id = id;
        EventId = transaction.EventId;
        TransactionId = transaction.TransactionId;
        CustomerId = transaction.CustomerId;
        RiskScore = riskScore;
        Decision = decision;
        RuleSetVersion = ruleSetVersion;
        EvaluatedAt = evaluatedAt;
        _ruleOutcomes = [.. ruleOutcomes];
    }

    private FraudAssessment(
        AssessmentId id,
        EventId eventId,
        TransactionId transactionId,
        CustomerId customerId,
        RiskScore riskScore,
        FraudDecision decision,
        IReadOnlyList<RuleOutcome> ruleOutcomes,
        RuleSetVersion ruleSetVersion,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(ruleOutcomes);

        Id = id;
        EventId = eventId;
        TransactionId = transactionId;
        CustomerId = customerId;
        RiskScore = riskScore;
        Decision = decision;
        RuleSetVersion = ruleSetVersion;
        EvaluatedAt = evaluatedAt;
        _ruleOutcomes = [.. ruleOutcomes];
    }

    public AssessmentId Id { get; }

    /// <summary>The event this assessed, which is also the idempotency key it arrived under.</summary>
    public EventId EventId { get; }

    public TransactionId TransactionId { get; }

    /// <summary>Copied from the transaction so assessments can be queried per customer directly.</summary>
    public CustomerId CustomerId { get; }

    public RiskScore RiskScore { get; }

    public FraudDecision Decision { get; }

    public RuleSetVersion RuleSetVersion { get; }

    /// <summary>
    /// When the engine reached this verdict, which is distinct from when the transaction occurred.
    /// </summary>
    public DateTimeOffset EvaluatedAt { get; }

    /// <summary>Every rule that ran, in evaluation order, whether or not it fired.</summary>
    public IReadOnlyList<RuleOutcome> RuleOutcomes => _ruleOutcomes;

    /// <summary>
    /// Rebuilds an assessment read back from storage.
    /// </summary>
    /// <remarks>
    /// Takes the identifiers directly, because the transaction they were copied from is not necessarily
    /// loaded when an assessment is read. The public constructor takes a transaction instead, which is
    /// the right shape when one is being made and the wrong shape when one is being restored.
    /// </remarks>
    public static FraudAssessment Restore(
        AssessmentId id,
        EventId eventId,
        TransactionId transactionId,
        CustomerId customerId,
        RiskScore riskScore,
        FraudDecision decision,
        IReadOnlyList<RuleOutcome> ruleOutcomes,
        RuleSetVersion ruleSetVersion,
        DateTimeOffset evaluatedAt) =>
        new(id, eventId, transactionId, customerId, riskScore, decision, ruleOutcomes, ruleSetVersion, evaluatedAt);

    /// <summary>Only the rules that fired, which is what an analyst reads first.</summary>
    public IEnumerable<RuleOutcome> TriggeredRules => _ruleOutcomes.Where(outcome => outcome.IsTriggered);

    public bool RequiresAttention => Decision is not FraudDecision.Approve;
}
