namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Runs every registered rule against a transaction and collects the outcomes.
/// </summary>
/// <remarks>
/// Sequential and in registration order. Parallelising was considered and rejected: the rules are in
/// memory predicates costing microseconds, so <c>Task.WhenAll</c> would add scheduling overhead and
/// non deterministic ordering for nothing. Stable ordering makes two stored assessments comparable.
/// </remarks>
public sealed class FraudRuleEvaluator
{
    private readonly IFraudRule[] _rules;

    /// <exception cref="ArgumentException">
    /// No rules were supplied, a rule has an uninitialised identifier, or two rules share one.
    /// </exception>
    public FraudRuleEvaluator(IEnumerable<IFraudRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = [.. rules];

        // An engine with no rules approves everything silently, which is the kind of misconfiguration
        // that goes unnoticed for a long time.
        if (_rules.Length == 0)
        {
            throw new ArgumentException("At least one rule must be registered.", nameof(rules));
        }

        EnsureIdentifiersAreUsable(_rules);
    }

    public int RuleCount => _rules.Length;

    /// <summary>
    /// One outcome per rule, in registration order. A rule that throws fails the whole evaluation rather
    /// than being swallowed, because a partial assessment would make an unassessed transaction look
    /// assessed.
    /// </summary>
    public IReadOnlyList<RuleOutcome> Evaluate(FraudEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var outcomes = new RuleOutcome[_rules.Length];

        for (var i = 0; i < _rules.Length; i++)
        {
            outcomes[i] = _rules[i].Evaluate(context);
        }

        return outcomes;
    }

    private static void EnsureIdentifiersAreUsable(IFraudRule[] rules)
    {
        var seen = new HashSet<RuleId>(rules.Length);

        foreach (var rule in rules)
        {
            if (!rule.Id.IsInitialised)
            {
                throw new ArgumentException(
                    $"Rule {rule.GetType().Name} does not have an identifier.",
                    nameof(rules));
            }

            // Two rules sharing an identifier would merge in any query counting hits per rule, and an
            // outcome could not be traced back to the rule that made it.
            if (!seen.Add(rule.Id))
            {
                throw new ArgumentException(
                    $"More than one rule is registered under the identifier '{rule.Id}'.",
                    nameof(rules));
            }
        }
    }
}
