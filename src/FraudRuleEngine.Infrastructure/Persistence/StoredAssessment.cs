namespace FraudRuleEngine.Infrastructure.Persistence;

/// <summary>An assessment as it sits in the database.</summary>
internal sealed class StoredAssessment
{
    public required Guid Id { get; init; }

    public required string EventId { get; init; }

    public required string TransactionId { get; init; }

    public required string CustomerId { get; init; }

    public required int RiskScore { get; init; }

    public required string Decision { get; init; }

    public required string RuleSetVersion { get; init; }

    public required DateTimeOffset EvaluatedAtUtc { get; init; }

    public List<StoredRuleOutcome> RuleOutcomes { get; init; } = [];
}

/// <summary>
/// One rule's verdict on one assessment.
/// </summary>
/// <remarks>
/// A row per rule per assessment, including the rules that did not fire. That is deliberately more
/// rows than storing only the hits: "which rules did not fire on this, and why not" is a question
/// analysts ask when a transaction turns out to have been fraud, and it cannot be answered later if the
/// clear outcomes were discarded.
/// </remarks>
internal sealed class StoredRuleOutcome
{
    public required long Id { get; init; }

    public required Guid AssessmentId { get; init; }

    public required string RuleId { get; init; }

    public required bool IsTriggered { get; init; }

    public required string Severity { get; init; }

    public required string Reason { get; init; }

    /// <summary>
    /// Position in the evaluation order, so the sequence survives a round trip.
    /// </summary>
    /// <remarks>
    /// Without it the outcomes come back in whatever order the query planner chooses, and two reads of
    /// one assessment could present differently.
    /// </remarks>
    public required int Ordinal { get; init; }
}
