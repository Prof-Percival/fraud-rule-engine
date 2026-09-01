namespace FraudRuleEngine.Domain.Transactions;

/// <summary>
/// Thrown when an operation would combine two <see cref="Money"/> values in different currencies.
/// </summary>
/// <remarks>
/// Adding rand to dollars has no meaningful answer. Converting silently at some ambient rate, which some
/// systems do, produces numbers that look right and reconcile to nothing.
/// </remarks>
public sealed class CurrencyMismatchException : DomainException
{
    public CurrencyMismatchException()
        : base("Cannot combine monetary amounts in different currencies.")
    {
    }

    public CurrencyMismatchException(string message)
        : base(message)
    {
    }

    public CurrencyMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public CurrencyMismatchException(Currency left, Currency right)
        : base($"Cannot combine monetary amounts in different currencies: {left} and {right}.")
    {
        Left = left;
        Right = right;
    }

    public Currency Left { get; }

    public Currency Right { get; }
}
