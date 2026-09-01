namespace FraudRuleEngine.Domain.Transactions;

// Identifiers arrive as strings from upstream, so without wrapping them nothing stops a customer
// identifier being passed where an account identifier belongs: both compile, and the mistake shows up
// as an assessment attached to the wrong customer.
//
// Written by hand rather than generated. A source generator would mean a package reference in the
// domain project, which is the one thing ADR 0001 rules out.
//
// A struct always has a default that no constructor can intercept, so each type exposes IsInitialised
// and Value throws rather than returning a null the annotations say cannot happen. TransactionEvent
// checks on the way in, so a defaulted identifier fails at construction rather than at the database.

/// <summary>
/// The producer's unique identifier for an event, used as the idempotency key on ingestion.
/// </summary>
public readonly record struct EventId
{
    private readonly string? _value;

    private EventId(string value) => _value = value;

    /// <summary>Large enough for a UUID or a ULID.</summary>
    public const int MaximumLength = 64;

    public bool IsInitialised => _value is not null;

    /// <exception cref="InvalidOperationException">The identifier was never initialised.</exception>
    public string Value => _value ?? throw IdentifierText.NotInitialised(nameof(EventId));

    public static EventId From(string value) =>
        new(IdentifierText.Validate(value, MaximumLength, nameof(EventId)));

    public override string ToString() => _value ?? string.Empty;
}

/// <summary>Identifies the transaction an event describes.</summary>
public readonly record struct TransactionId
{
    private readonly string? _value;

    private TransactionId(string value) => _value = value;

    public const int MaximumLength = 64;

    public bool IsInitialised => _value is not null;

    /// <exception cref="InvalidOperationException">The identifier was never initialised.</exception>
    public string Value => _value ?? throw IdentifierText.NotInitialised(nameof(TransactionId));

    public static TransactionId From(string value) =>
        new(IdentifierText.Validate(value, MaximumLength, nameof(TransactionId)));

    public override string ToString() => _value ?? string.Empty;
}

/// <summary>Identifies the customer the transaction belongs to.</summary>
public readonly record struct CustomerId
{
    private readonly string? _value;

    private CustomerId(string value) => _value = value;

    public const int MaximumLength = 64;

    public bool IsInitialised => _value is not null;

    /// <exception cref="InvalidOperationException">The identifier was never initialised.</exception>
    public string Value => _value ?? throw IdentifierText.NotInitialised(nameof(CustomerId));

    public static CustomerId From(string value) =>
        new(IdentifierText.Validate(value, MaximumLength, nameof(CustomerId)));

    public override string ToString() => _value ?? string.Empty;
}

/// <summary>Identifies the account the transaction was made against.</summary>
public readonly record struct AccountId
{
    private readonly string? _value;

    private AccountId(string value) => _value = value;

    public const int MaximumLength = 64;

    public bool IsInitialised => _value is not null;

    /// <exception cref="InvalidOperationException">The identifier was never initialised.</exception>
    public string Value => _value ?? throw IdentifierText.NotInitialised(nameof(AccountId));

    public static AccountId From(string value) =>
        new(IdentifierText.Validate(value, MaximumLength, nameof(AccountId)));

    public override string ToString() => _value ?? string.Empty;
}

/// <summary>Identifies the merchant the transaction was made with.</summary>
public readonly record struct MerchantId
{
    private readonly string? _value;

    private MerchantId(string value) => _value = value;

    public const int MaximumLength = 64;

    public bool IsInitialised => _value is not null;

    /// <exception cref="InvalidOperationException">The identifier was never initialised.</exception>
    public string Value => _value ?? throw IdentifierText.NotInitialised(nameof(MerchantId));

    public static MerchantId From(string value) =>
        new(IdentifierText.Validate(value, MaximumLength, nameof(MerchantId)));

    public override string ToString() => _value ?? string.Empty;
}

internal static class IdentifierText
{
    internal static string Validate(string value, int maximumLength, string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value.Length,
                $"{typeName} may be at most {maximumLength} characters.");
        }

        return value;
    }

    internal static InvalidOperationException NotInitialised(string typeName) =>
        new($"{typeName} was never initialised. Construct it with {typeName}.From.");
}
