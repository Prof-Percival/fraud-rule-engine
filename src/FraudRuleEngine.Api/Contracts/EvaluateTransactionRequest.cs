namespace FraudRuleEngine.Api.Contracts;

/// <summary>
/// A transaction submitted for assessment.
/// </summary>
/// <remarks>
/// A separate type from the domain model, so the published shape does not move every time the model
/// does. Everything is nullable and loosely typed on purpose: a missing field has to come back as a
/// validation error naming it, not as a deserialisation failure naming a line and column.
/// </remarks>
public sealed record EvaluateTransactionRequest
{
    /// <summary>Producer's unique identifier for this delivery, and the idempotency key.</summary>
    public string? EventId { get; init; }

    public string? TransactionId { get; init; }

    public string? CustomerId { get; init; }

    public string? AccountId { get; init; }

    public decimal? Amount { get; init; }

    /// <summary>ISO 4217 alpha-3, such as <c>ZAR</c>.</summary>
    public string? Currency { get; init; }

    public string? Category { get; init; }

    public string? Channel { get; init; }

    public string? MerchantId { get; init; }

    public string? MerchantName { get; init; }

    /// <summary>ISO 3166-1 alpha-2, such as <c>ZA</c>.</summary>
    public string? CountryCode { get; init; }

    /// <summary>
    /// When the transaction happened, with its offset. The offset is significant: it is the only record
    /// of the local wall clock, which the unusual hour rule reads.
    /// </summary>
    public DateTimeOffset? OccurredAt { get; init; }
}
