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

    /// <summary>
    /// Where to continue from, or null for the first page.
    /// </summary>
    /// <remarks>
    /// Keyset rather than a page number. Offset paging walks and discards every skipped row, and on a
    /// table taking inserts it repeats or skips rows as new assessments shift the boundary.
    /// </remarks>
    public AssessmentCursor? After { get; init; }

    public int PageSize { get; init; } = DefaultPageSize;
}

public sealed record AssessmentPage
{
    public required IReadOnlyList<AssessmentView> Items { get; init; }

    /// <summary>
    /// Pass back as <see cref="AssessmentQuery.After"/> for the next page. Null when this is the last.
    /// </summary>
    public string? NextCursor { get; init; }

    public required int PageSize { get; init; }

    /// <summary>
    /// There is no total count. It needs a second query scanning every matching row, which costs more
    /// than the page itself.
    /// </summary>
    public bool HasMore => NextCursor is not null;
}

public sealed record AssessmentView
{
    public required Guid AssessmentId { get; init; }

    public required string EventId { get; init; }

    public required string TransactionId { get; init; }

    public required string CustomerId { get; init; }

    public required int RiskScore { get; init; }

    public required string Decision { get; init; }

    public required string RuleSetVersion { get; init; }

    public required DateTimeOffset EvaluatedAt { get; init; }

    public IReadOnlyList<RuleOutcomeView> RuleOutcomes { get; init; } = [];
}

public sealed record RuleOutcomeView
{
    public required string RuleId { get; init; }

    public required bool Triggered { get; init; }

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
