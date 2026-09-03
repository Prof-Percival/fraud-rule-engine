using FraudRuleEngine.Domain.Scoring;

namespace FraudRuleEngine.Application.Abstractions;

/// <summary>
/// Reads stored assessments.
/// </summary>
/// <remarks>
/// Separate from <see cref="IFraudAssessmentStore"/> because reading and writing have almost nothing in
/// common here. Writing goes through the domain model and its invariants; reading answers questions for
/// a screen and never needs a domain object at all, so it projects straight to views and avoids
/// rehydrating a model only to flatten it again.
/// </remarks>
public interface IAssessmentQueries
{
    Task<AssessmentView?> FindAsync(Guid assessmentId, CancellationToken cancellationToken);

    Task<AssessmentPage> SearchAsync(AssessmentQuery query, CancellationToken cancellationToken);

    Task<AssessmentSummary> SummariseAsync(CancellationToken cancellationToken);
}

/// <summary>
/// What to look for. All filters are optional and combine with AND.
/// </summary>
public sealed record AssessmentQuery
{
    public const int MaximumPageSize = 200;

    public const int DefaultPageSize = 25;

    public string? CustomerId { get; init; }

    public FraudDecision? Decision { get; init; }

    public int? MinimumRiskScore { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;
}

public sealed record AssessmentPage
{
    public required IReadOnlyList<AssessmentView> Items { get; init; }

    /// <summary>
    /// How many rows match the filter across all pages.
    /// </summary>
    /// <remarks>
    /// Requires a second count query, which is part of what makes offset paging expensive on a large
    /// table.
    /// </remarks>
    public required int TotalCount { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }
}

public sealed record AssessmentView
{
    public required Guid Id { get; init; }

    public required string EventId { get; init; }

    public required string TransactionId { get; init; }

    public required string CustomerId { get; init; }

    public required int RiskScore { get; init; }

    public required string Decision { get; init; }

    public required string RuleSetVersion { get; init; }

    public required DateTimeOffset EvaluatedAtUtc { get; init; }

    public IReadOnlyList<RuleOutcomeView> RuleOutcomes { get; init; } = [];
}

public sealed record RuleOutcomeView
{
    public required string RuleId { get; init; }

    public required bool IsTriggered { get; init; }

    public required string Severity { get; init; }

    public required string Reason { get; init; }
}

/// <summary>
/// Aggregate counts, for an operational dashboard.
/// </summary>
/// <remarks>
/// A sudden shift in the decision split, or one rule's share of hits jumping, is the signal that
/// something upstream changed. That is a question about the whole table rather than any one assessment,
/// which is why it is a distinct query rather than something a caller derives by paging.
/// </remarks>
public sealed record AssessmentSummary
{
    public required int Total { get; init; }

    public required int Approved { get; init; }

    public required int Review { get; init; }

    public required int Declined { get; init; }

    public required IReadOnlyList<RuleHitCount> MostTriggeredRules { get; init; }
}

public sealed record RuleHitCount
{
    public required string RuleId { get; init; }

    public required int Hits { get; init; }
}
