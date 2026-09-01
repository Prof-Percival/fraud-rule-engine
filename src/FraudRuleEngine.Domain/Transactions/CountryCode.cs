namespace FraudRuleEngine.Domain.Transactions;

/// <summary>
/// An ISO 3166-1 alpha-2 country code, such as <c>ZA</c>.
/// </summary>
/// <remarks>
/// A validated string rather than a closed enum, unlike <see cref="Currency"/>. Transactions arrive from
/// anywhere, so a closed set would mean rejecting legitimate traffic from a country nobody added.
///
/// <para>
/// Format is validated, membership of the real ISO register is not. A well formed but unassigned code
/// costs nothing here: it simply has no coordinates, and the impossible travel rule declines to fire.
/// </para>
/// </remarks>
public readonly record struct CountryCode
{
    private readonly string? _value;

    private CountryCode(string value) => _value = value;

    public const int Length = 2;

    /// <summary>False for a defaulted instance that never went through <see cref="From"/>.</summary>
    public bool IsInitialised => _value is not null;

    /// <exception cref="InvalidOperationException">The code was never initialised.</exception>
    public string Value => _value ?? throw IdentifierText.NotInitialised(nameof(CountryCode));

    /// <summary>
    /// Case is normalised to upper, since a country code is case insensitive and <c>za</c> and
    /// <c>ZA</c> are the same country.
    /// </summary>
    public static CountryCode From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length != Length || !value.All(char.IsLetter))
        {
            throw new ArgumentException(
                $"A country code must be exactly {Length} letters, but was '{value}'.",
                nameof(value));
        }

        return new CountryCode(value.ToUpperInvariant());
    }

    public override string ToString() => _value ?? string.Empty;
}
