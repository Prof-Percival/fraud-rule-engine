using System.Globalization;

namespace FraudRuleEngine.Domain.Transactions;

/// <summary>
/// An amount of money in a specific currency.
/// </summary>
/// <remarks>
/// Two things this type exists to prevent.
///
/// <para>
/// A bare <see cref="decimal"/> carries no currency, so nothing stops rand being compared against
/// dollars and nothing complains when they are added. Pairing the amount with its currency and
/// refusing to combine mismatched ones moves that from a bug found in production to a build or
/// test failure.
/// </para>
///
/// <para>
/// The amount is <see cref="decimal"/> and never <see cref="double"/>. Binary floating point
/// cannot represent 0.10 exactly, so sums drift, and in a fraud engine that drift lands on
/// threshold comparisons: a transaction of exactly the threshold amount may or may not trip the
/// rule depending on how the number was arrived at. Decimal is base ten and exact for these
/// values.
/// </para>
///
/// <para>
/// Multiplication and aggregation are deliberately absent. They are not needed to hold a
/// transaction amount and compare it against a threshold, and they are the operations that force
/// a rounding policy to be decided. They get added when a rule actually needs them, along with
/// the rounding decision, rather than being guessed at now.
/// </para>
/// </remarks>
public readonly record struct Money : IComparable<Money>, IComparable
{
    /// <summary>
    /// Maximum number of decimal places an amount may carry, matching the
    /// <c>numeric(19,4)</c> column the value is persisted into.
    /// </summary>
    public const int MaximumScale = 4;

    /// <summary>
    /// Creates an amount in the given currency.
    /// </summary>
    /// <param name="amount">
    /// The amount. May be negative, since subtracting two amounts is a legitimate operation and
    /// credits exist. Whether a specific amount is allowed to be negative is a question for the
    /// thing holding it, not for this type.
    /// </param>
    /// <param name="currency">The currency. Must not be <see cref="Currency.None"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The currency is <see cref="Currency.None"/>, or the amount carries more than
    /// <see cref="MaximumScale"/> decimal places.
    /// </exception>
    public Money(decimal amount, Currency currency)
    {
        if (currency is Currency.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currency),
                currency,
                "A monetary amount must have a currency.");
        }

        if (amount.Scale > MaximumScale)
        {
            // Rejected rather than rounded. Rounding here would be a silent change to a
            // financial figure the caller supplied, and the caller is better placed to decide
            // what a fifth decimal place was supposed to mean.
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                $"A monetary amount may carry at most {MaximumScale} decimal places.");
        }

        Amount = amount;
        Currency = currency;
    }

    /// <summary>The amount.</summary>
    public decimal Amount { get; }

    /// <summary>The currency the amount is denominated in.</summary>
    public Currency Currency { get; }

    /// <summary>True when the amount is greater than zero.</summary>
    public bool IsPositive => Amount > 0m;

    /// <summary>True when the amount is less than zero.</summary>
    public bool IsNegative => Amount < 0m;

    /// <summary>True when the amount is exactly zero.</summary>
    public bool IsZero => Amount == 0m;

    /// <summary>Zero in the given currency.</summary>
    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator -(Money value) => value.Negate();

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    /// <summary>Adds another amount in the same currency.</summary>
    /// <exception cref="CurrencyMismatchException">The currencies differ.</exception>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>Subtracts another amount in the same currency.</summary>
    /// <exception cref="CurrencyMismatchException">The currencies differ.</exception>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>Returns this amount with the opposite sign.</summary>
    public Money Negate() => new(-Amount, Currency);

    /// <inheritdoc />
    /// <exception cref="CurrencyMismatchException">The currencies differ.</exception>
    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    /// <inheritdoc />
    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        Money other => CompareTo(other),
        _ => throw new ArgumentException($"Object must be of type {nameof(Money)}.", nameof(obj)),
    };

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount} {Currency.ToString().ToUpperInvariant()}");

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new CurrencyMismatchException(Currency, other.Currency);
        }
    }
}
