namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// How strong a signal a rule considers its own hit to be. Rules declare severity; the scoring policy
/// turns it into points, so tuning lives in one place rather than across eight rules.
/// </summary>
public enum RuleSeverity
{
    /// <summary>The rule did not trigger. The only valid severity for a clear outcome.</summary>
    None = 0,

    /// <summary>Common in legitimate behaviour. Meaningful only alongside other hits.</summary>
    Low,

    /// <summary>Notable, but not enough to act on without corroboration.</summary>
    Medium,

    /// <summary>Enough on its own to warrant a human looking at the transaction.</summary>
    High,
}
