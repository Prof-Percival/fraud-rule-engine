namespace FraudRuleEngine.Domain.Transactions;

/// <summary>
/// A single categorised transaction, as received from upstream, ready to be assessed for fraud.
/// </summary>
/// <remarks>
/// Immutable, because an event describes something that already happened and a correction is a new event
/// rather than an edit. Invariants are enforced here as well as at the API boundary, which is not
/// duplication: that layer turns bad input into a useful response, this one guarantees nothing
/// downstream has to wonder whether the amount might be negative.
///
/// <para>
/// The amount must be positive, so this models spend. A refund would be a separate event type, since the
/// fraud signals for money leaving an account and money returning to it are not the same.
/// </para>
/// </remarks>
public sealed record TransactionEvent
{
    /// <param name="eventId">
    /// The idempotency key. Distinct from <paramref name="transactionId"/>, because the same transaction
    /// can legitimately be delivered more than once and each delivery carries the same event identifier.
    /// </param>
    /// <param name="occurredAt">
    /// The transaction's own time, not when this service saw it. That distinction is what makes velocity
    /// and impossible travel meaningful.
    /// </param>
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
        OccurredAt = occurredAt;
    }

    public EventId EventId { get; }

    public TransactionId TransactionId { get; }

    public CustomerId CustomerId { get; }

    public AccountId AccountId { get; }

    /// <summary>Always positive.</summary>
    public Money Amount { get; }

    public TransactionCategory Category { get; }

    public TransactionChannel Channel { get; }

    public Merchant Merchant { get; }

    public CountryCode Country { get; }

    /// <summary>
    /// The offset is kept, not folded into UTC, because it is the only record of what the wall clock read
    /// where the transaction happened and a rule asking about three in the morning needs it. Keeping it
    /// costs nothing, since <see cref="DateTimeOffset"/> compares and subtracts on the instant.
    /// </summary>
    /// <remarks>
    /// This trusts the producer's offset. One that sends everything as UTC leaves local time
    /// unrecoverable. The better question is whether the hour was unusual for that customer rather than
    /// for the shop, which needs their own timezone from a profile.
    /// </remarks>
    public DateTimeOffset OccurredAt { get; }

    public DateTimeOffset OccurredAtUtc => OccurredAt.ToUniversalTime();

    /// <summary>Wall clock time where the transaction took place, from the reported offset.</summary>
    public TimeOnly LocalTimeOfDay => TimeOnly.FromTimeSpan(OccurredAt.TimeOfDay);

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
