namespace FraudRuleEngine.Domain.Transactions;

/// <summary>
/// The merchant a transaction was made with.
/// </summary>
/// <remarks>
/// Grouped into a type rather than two loose properties, because three rules treat the merchant as a
/// unit: the deny list matches on it, the first time merchant rule compares it against history, and an
/// analyst reviewing a flag needs the name rather than the identifier.
/// </remarks>
public sealed record Merchant
{
    public const int MaximumNameLength = 200;

    /// <param name="name">
    /// Trading name as it would appear on a statement. Carried because a flag a human has to action is
    /// far easier to judge with a name than an identifier.
    /// </param>
    public Merchant(MerchantId id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Length > MaximumNameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                name.Length,
                $"A merchant name may be at most {MaximumNameLength} characters.");
        }

        Id = id;
        Name = name;
    }

    public MerchantId Id { get; }

    public string Name { get; }
}
