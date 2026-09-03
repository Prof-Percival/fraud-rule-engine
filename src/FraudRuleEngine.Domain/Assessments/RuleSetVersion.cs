using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Assessments;

/// <summary>
/// Identifies the rule set and thresholds that produced an assessment.
/// </summary>
/// <remarks>
/// Stamped on every assessment because rules and thresholds change. Without it, an assessment from six
/// months ago cannot be explained: the configuration that produced the score no longer exists, so
/// nobody can say why the transaction was flagged or defend the decision if asked. In a regulated
/// environment that is a requirement rather than a refinement.
///
/// <para>
/// It also makes a difference operationally. When flag volume jumps overnight, the first question is
/// which version the flags were produced under, and that is only answerable if it was recorded at the
/// time.
/// </para>
/// </remarks>
public readonly record struct RuleSetVersion
{
    private readonly string? _value;

    private RuleSetVersion(string value) => _value = value;

    public const int MaximumLength = 64;

    public bool IsInitialised => _value is not null;

    /// <exception cref="InvalidOperationException">The version was never initialised.</exception>
    public string Value => _value ?? throw IdentifierText.NotInitialised(nameof(RuleSetVersion));

    public static RuleSetVersion From(string value) =>
        new(IdentifierText.Validate(value, MaximumLength, nameof(RuleSetVersion)));

    public override string ToString() => _value ?? string.Empty;
}
