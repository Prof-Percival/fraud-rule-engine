namespace FraudRuleEngine.Domain.Transactions;

/// <summary>
/// A single categorised transaction, as received from upstream, ready to be assessed for fraud.
/// </summary>
/// <remarks>
/// This is the input to the whole engine and it is immutable. An event describes something that has
/// already happened, so there is nothing to mutate: a correction is a new event, not an edit to
/// this one. That matters for auditability, since an assessment has to remain explainable in terms
/// of exactly what was assessed.
///
/// <para>
/// Invariants are enforced in the constructor rather than left to the caller, because this type is
/// built from data supplied by another service. Validation living at the API boundary as well is
/// not duplication: that layer turns bad input into a useful problem response, and this layer
/// guarantees that nothing downstream has to wonder whether the amount might be negative.
/// </para>
///
/// <para>
/// Reversals and credits are out of scope. The amount is required to be positive, which means this
/// type models spend. A refund would be a distinct event type with its own rules, since the fraud
/// signals for money leaving an account and money returning to it are not the same.
/// </para>
/// </remarks>
public sealed record TransactionEvent
{
    /// <summary>Creates a transaction event.</summary>
    /// <param name="eventId">
    /// The producer's identifier for this event, used as the idempotency key. Distinct from
    /// <paramref name="transactionId"/>, because the same transaction can legitimately be delivered
    /// more than once and each delivery carries the same event identifier.
    /// </param>
    /// <param name="transactionId">The identifier of the underlying transaction.</param>
    /// <param name="customerId">The customer the transaction belongs to.</param>
    /// <param name="accountId">The account the transaction was made against.</param>
    /// <param name="amount">The transaction amount. Must be positive.</param>
    /// <param name="category">The category assigned upstream.</param>
    /// <param name="channel">How the transaction was initiated.</param>
    /// <param name="merchant">The merchant involved.</param>
    /// <param name="country">Where the transaction took place.</param>
    /// <param name="occurredAt">
    /// When the transaction took place, as reported by the producer. This is the transaction's own
    /// time and not the time this service saw it, which is the distinction that makes velocity and
    /// impossible travel rules meaningful.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="merchant"/> is null.</exception>
    /// <exception cref="ArgumentException">An identifier was not constructed through its factory.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The amount is not positive, or <paramref name="occurredAt"/> is not set.
    /// </exception>
    public TransactionEvent(
        EventId eventId,
        TransactionId transactionId,
        CustomerId customerId,
        AccountId accountId,
        Money amount,
        TransactionCategory category,
        TransactionChannel channel,
        Merchant merchant,
        CountryCode country,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(merchant);

        EnsureInitialised(eventId.IsInitialised, nameof(eventId));
        EnsureInitialised(transactionId.IsInitialised, nameof(transactionId));
        EnsureInitialised(customerId.IsInitialised, nameof(customerId));
        EnsureInitialised(accountId.IsInitialised, nameof(accountId));
        EnsureInitialised(country.IsInitialised, nameof(country));

        if (!amount.IsPositive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "A transaction amount must be positive.");
        }

        if (occurredAt == default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurredAt),
                occurredAt,
                "A transaction must carry the time it occurred.");
        }

        EventId = eventId;
        TransactionId = transactionId;
        CustomerId = customerId;
        AccountId = accountId;
        Amount = amount;
        Category = category;
        Channel = channel;
        Merchant = merchant;
        Country = country;
        OccurredAt = occurredAt.ToUniversalTime();
    }

    /// <summary>The producer's identifier for this event, and the idempotency key on ingestion.</summary>
    public EventId EventId { get; }

    /// <summary>The identifier of the underlying transaction.</summary>
    public TransactionId TransactionId { get; }

    /// <summary>The customer the transaction belongs to.</summary>
    public CustomerId CustomerId { get; }

    /// <summary>The account the transaction was made against.</summary>
    public AccountId AccountId { get; }

    /// <summary>The transaction amount, always positive.</summary>
    public Money Amount { get; }

    /// <summary>The category assigned by the upstream service.</summary>
    public TransactionCategory Category { get; }

    /// <summary>How the transaction was initiated.</summary>
    public TransactionChannel Channel { get; }

    /// <summary>The merchant involved.</summary>
    public Merchant Merchant { get; }

    /// <summary>Where the transaction took place.</summary>
    public CountryCode Country { get; }

    /// <summary>
    /// When the transaction occurred, normalised to UTC.
    /// </summary>
    /// <remarks>
    /// Stored in UTC so that comparisons across events are unambiguous, which velocity and
    /// impossible travel both depend on. Rules that need to know what the local time was for the
    /// customer, the unusual hour rule being the one, convert from here using the customer's
    /// timezone rather than relying on whatever offset the producer happened to send.
    /// </remarks>
    public DateTimeOffset OccurredAt { get; }

    private static void EnsureInitialised(bool isInitialised, string parameterName)
    {
        if (!isInitialised)
        {
            throw new ArgumentException(
                "Identifier was not constructed through its From factory.",
                parameterName);
        }
    }
}
