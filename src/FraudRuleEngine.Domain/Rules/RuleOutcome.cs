namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// The result of running one rule against one transaction.
/// </summary>
/// <remarks>
/// Produced even when the rule does not fire, with a reason either way. That costs a row per rule per
/// transaction and buys the answer to "which rules did not fire, and why not", which analysts ask when
/// something turns out to have been fraud that nothing caught.
///
/// <para>
/// Carries severity but not a score. Turning severity into points belongs to the scoring policy, so
/// tuning lives in one place. See <see cref="RuleSeverity"/>.
/// </para>
/// </remarks>
public sealed record RuleOutcome
{
    private RuleOutcome(RuleId ruleId, bool isTriggered, RuleSeverity severity, string reason)
    {
        RuleId = ruleId;
        IsTriggered = isTriggered;
        Severity = severity;
        Reason = reason;
    }

    public RuleId RuleId { get; }

    public bool IsTriggered { get; }

    /// <summary><see cref="RuleSeverity.None"/> when the rule did not fire.</summary>
    public RuleSeverity Severity { get; }

    /// <summary>Why, in terms a person can act on, including the values that drove it.</summary>
    public string Reason { get; }

    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="severity"/> is <see cref="RuleSeverity.None"/>. A rule that fired has to say how
    /// strongly, or the scoring policy has nothing to work with.
    /// </exception>
    public static RuleOutcome Triggered(RuleId ruleId, RuleSeverity severity, string reason)
    {
        EnsureIdentified(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (severity is RuleSeverity.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(severity),
                severity,
                "A rule that triggered must report a severity above None.");
        }

        return new RuleOutcome(ruleId, isTriggered: true, severity, reason);
    }

    /// <param name="reason">
    /// Why it did not fire. Populated rather than blank, because this is what makes a stored assessment
    /// reviewable afterwards.
    /// </param>
    public static RuleOutcome Clear(RuleId ruleId, string reason)
    {
        EnsureIdentified(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new RuleOutcome(ruleId, isTriggered: false, RuleSeverity.None, reason);
    }

    private static void EnsureIdentified(RuleId ruleId)
    {
        if (!ruleId.IsInitialised)
        {
            throw new ArgumentException(
                "An outcome must name the rule that produced it.",
                nameof(ruleId));
        }
    }
}
