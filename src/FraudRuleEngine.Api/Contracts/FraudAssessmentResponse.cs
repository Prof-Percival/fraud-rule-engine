using FraudRuleEngine.Domain.Assessments;

namespace FraudRuleEngine.Api.Contracts;

/// <summary>The engine's verdict, and the evidence behind it.</summary>
public sealed record FraudAssessmentResponse
{
    public required Guid AssessmentId { get; init; }

    public required string EventId { get; init; }

    public required string TransactionId { get; init; }

    public required string CustomerId { get; init; }

    public required int RiskScore { get; init; }

    /// <summary><c>Approve</c>, <c>Review</c> or <c>Decline</c>.</summary>
    public required string Decision { get; init; }

    /// <summary>Which rule set and thresholds produced this, so it stays explainable later.</summary>
    public required string RuleSetVersion { get; init; }

    public required DateTimeOffset EvaluatedAt { get; init; }

    /// <summary>
    /// Every rule that ran, in evaluation order, including the ones that did not fire.
    /// </summary>
    /// <remarks>
    /// The clear outcomes are returned as well as the hits, because the caller is usually deciding
    /// whether to act and "nothing else looked wrong either" is part of that.
    /// </remarks>
    public required IReadOnlyList<RuleOutcomeResponse> RuleOutcomes { get; init; }

    public static FraudAssessmentResponse From(FraudAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        return new FraudAssessmentResponse
        {
            AssessmentId = assessment.Id.Value,
            EventId = assessment.EventId.Value,
            TransactionId = assessment.TransactionId.Value,
            CustomerId = assessment.CustomerId.Value,
            RiskScore = assessment.RiskScore.Value,
            Decision = assessment.Decision.ToString(),
            RuleSetVersion = assessment.RuleSetVersion.Value,
            EvaluatedAt = assessment.EvaluatedAt,
            RuleOutcomes = [.. assessment.RuleOutcomes.Select(RuleOutcomeResponse.From)],
        };
    }
}

public sealed record RuleOutcomeResponse
{
    public required string RuleId { get; init; }

    public required bool Triggered { get; init; }

    /// <summary><c>None</c> when the rule did not fire.</summary>
    public required string Severity { get; init; }

    /// <summary>Why, including the values that drove it.</summary>
    public required string Reason { get; init; }

    public static RuleOutcomeResponse From(Domain.Rules.RuleOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return new RuleOutcomeResponse
        {
            RuleId = outcome.RuleId.Value,
            Triggered = outcome.IsTriggered,
            Severity = outcome.Severity.ToString(),
            Reason = outcome.Reason,
        };
    }
}
