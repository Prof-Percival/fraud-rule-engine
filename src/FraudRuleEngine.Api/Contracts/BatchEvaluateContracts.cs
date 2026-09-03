namespace FraudRuleEngine.Api.Contracts;

/// <summary>A set of transactions submitted together.</summary>
public sealed record BatchEvaluateRequest
{
    /// <summary>
    /// Upper bound on one batch.
    /// </summary>
    /// <remarks>
    /// Bounded because each item costs two queries and two writes, so an unbounded batch is a request
    /// that holds a connection and a transaction open for as long as the caller likes. Five hundred is
    /// large enough to be worth batching and small enough to finish inside a sane request timeout.
    /// </remarks>
    public const int MaximumItems = 500;

    public IReadOnlyList<EvaluateTransactionRequest>? Transactions { get; init; }
}

/// <summary>
/// The outcome of every item, in the order they were submitted.
/// </summary>
/// <remarks>
/// A per item result rather than a single verdict for the batch. One malformed transaction in a file of
/// four hundred should not reject the other three hundred and ninety nine, and the caller needs to know
/// precisely which one to fix.
/// </remarks>
public sealed record BatchEvaluateResponse
{
    public required int Submitted { get; init; }

    public required int Accepted { get; init; }

    public required int Rejected { get; init; }

    public required IReadOnlyList<BatchItemResult> Results { get; init; }
}

public sealed record BatchItemResult
{
    /// <summary>Position in the submitted list, so a result ties back even without an event id.</summary>
    public required int Index { get; init; }

    /// <summary>Echoed back where it was supplied, since that is what a caller recognises.</summary>
    public string? EventId { get; init; }

    public required bool Accepted { get; init; }

    /// <summary>Present when the item was accepted.</summary>
    public FraudAssessmentResponse? Assessment { get; init; }

    /// <summary>Present when it was not, keyed by field.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    /// <summary>Set when the item failed for a reason other than validation.</summary>
    public string? Error { get; init; }
}
