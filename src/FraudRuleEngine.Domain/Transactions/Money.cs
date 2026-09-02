using System.Globalization;

namespace FraudRuleEngine.Domain.Transactions;

/// <summary>
/// An amount of money in a specific currency.
/// </summary>
/// <remarks>
/// A bare <see cref="decimal"/> carries no currency, so nothing stops rand being compared against
/// dollars. Pairing the two and refusing mismatched operations turns that from a production bug into a
/// compile or test failure.
///
/// <para>
/// Always <see cref="decimal"/>, never <see cref="double"/>. Binary floating point cannot represent 0.10
/// exactly, and the drift lands on threshold comparisons: a transaction of exactly the threshold amount
/// would trip the rule or not depending on how the number was arrived at.
/// </para>
///
/// <para>
/// Multiplication rounds, and it is the only operation here that needs to. Adding or subtracting two
/// amounts already within scale cannot exceed it, but multiplying by an arbitrary factor can, so the
/// result is rounded to <see cref="MaximumScale"/> places using banker's rounding. Rounding away from
/// zero instead would push every halfway case upward, and across a large volume of transactions that
/// bias adds up to real money.
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
    /// May be negative, since subtracting two amounts is legitimate. Whether a particular amount is
    /// allowed to be negative belongs to the thing holding it.
    /// </param>
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

        // Rejected rather than rounded, because rounding would silently change a figure the caller
        // supplied and the caller is better placed to say what a fifth decimal place meant.
        if (amount.Scale > MaximumScale)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                $"A monetary amount may carry at most {MaximumScale} decimal places.");
        }

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public bool IsPositive => Amount > 0m;

    public bool IsNegative => Amount < 0m;

    public bool IsZero => Amount == 0m;

    /// <summary>Zero in the given currency.</summary>
    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator -(Money value) => value.Negate();

    public static Money operator *(Money left, decimal factor) => left.Multiply(factor);

    public static Money operator *(decimal factor, Money right) => right.Multiply(factor);

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    /// <exception cref="CurrencyMismatchException">The currencies differ.</exception>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <exception cref="CurrencyMismatchException">The currencies differ.</exception>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Negate() => new(-Amount, Currency);

    /// <summary>
    /// Scales by a factor, rounding to <see cref="MaximumScale"/> places to even.
    /// </summary>
    /// <remarks>
    /// Rounds where the constructor rejects, and the difference is who produced the extra digits. A
    /// caller's fifth decimal place is theirs to explain; digits our own arithmetic produced are ours to
    /// handle, and refusing to multiply would be useless.
    /// </remarks>
    public Money Multiply(decimal factor) =>
        new(Math.Round(Amount * factor, MaximumScale, MidpointRounding.ToEven), Currency);

    /// <exception cref="CurrencyMismatchException">The currencies differ.</exception>
    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        Money other => CompareTo(other),
        _ => throw new ArgumentException($"Object must be of type {nameof(Money)}.", nameof(obj)),
    };

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
