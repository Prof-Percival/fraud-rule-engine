using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Identifies a fraud rule.
/// </summary>
/// <remarks>
/// A string rather than an enum, because these values are persisted on every outcome and queried by
/// analysts. An enum would put its ordinal in the database, so renumbering would silently rewrite the
/// meaning of historical data. It follows that renaming a rule identifier is a data migration, not a
/// refactor.
/// </remarks>
public readonly record struct RuleId
{
    private readonly string? _value;

    private RuleId(string value) => _value = value;

    public const int MaximumLength = 64;

    /// <summary>False for a defaulted instance that never went through <see cref="From"/>.</summary>
    public bool IsInitialised => _value is not null;

    /// <exception cref="InvalidOperationException">The identifier was never initialised.</exception>
    public string Value => _value ?? throw IdentifierText.NotInitialised(nameof(RuleId));

    public static RuleId From(string value) =>
        new(IdentifierText.Validate(value, MaximumLength, nameof(RuleId)));

    public override string ToString() => _value ?? string.Empty;
}
