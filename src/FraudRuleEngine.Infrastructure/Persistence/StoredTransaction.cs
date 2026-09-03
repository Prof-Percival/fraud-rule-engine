namespace FraudRuleEngine.Infrastructure.Persistence;

/// <summary>
/// A transaction event as it sits in the database.
/// </summary>
/// <remarks>
/// A separate type from <c>TransactionEvent</c> rather than mapping the domain model directly. The
/// domain type validates in its constructor and exposes no setters, which is exactly what you want in
/// memory and exactly what fights materialisation. Mapping it would mean relaxing those guarantees, or
/// teaching EF Core to construct typed identifiers and value objects it has no business knowing about.
///
/// <para>
/// The cost is a translation step in both directions. The benefit is that the schema can change shape
/// without the domain moving, and the domain keeps its invariants.
/// </para>
/// </remarks>
internal sealed class StoredTransaction
{
    public required string EventId { get; init; }

    public required string TransactionId { get; init; }

    public required string CustomerId { get; init; }

    public required string AccountId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string Category { get; init; }

    public required string Channel { get; init; }

    public required string MerchantId { get; init; }

    public required string MerchantName { get; init; }

    public required string CountryCode { get; init; }

    /// <summary>When the transaction happened, as an instant.</summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>
    /// The offset the producer reported, in minutes.
    /// </summary>
    /// <remarks>
    /// Stored separately because <c>timestamptz</c> keeps the instant and discards the offset. Without
    /// this column the local wall clock time is unrecoverable, and the unusual hour rule depends on it.
    /// </remarks>
    public required int OffsetMinutes { get; init; }

    public required DateTimeOffset IngestedAtUtc { get; init; }
}
