namespace FraudRuleEngine.Domain.Transactions;

/// <summary>
/// The currencies this service handles, as ISO 4217 alpha-3 codes.
/// </summary>
/// <remarks>
/// A closed enum rather than an open string, so there is no way to end up with "zar" and "ZAR" as two
/// different currencies. Adding one needs a deploy, which is the right trade while the set changes
/// rarely.
///
/// <para>
/// Minor units are not modelled. These four all use two decimal places, so <see cref="Money"/> can
/// hold one scale for all of them. A currency with different precision, JPY having none and KWD having
/// three, would force a per currency lookup.
/// </para>
/// </remarks>
public enum Currency
{
    /// <summary>Not a currency. Named so that a defaulted struct cannot look valid.</summary>
    None = 0,

    /// <summary>South African rand.</summary>
    Zar,

    /// <summary>United States dollar.</summary>
    Usd,

    /// <summary>Euro.</summary>
    Eur,

    /// <summary>Pound sterling.</summary>
    Gbp,
}
