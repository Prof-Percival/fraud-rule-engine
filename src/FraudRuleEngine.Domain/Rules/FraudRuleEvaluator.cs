using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Runs every registered rule against a transaction and collects the outcomes.
/// </summary>
/// <remarks>
/// Takes its rules as a collection rather than naming them, so a caller no longer has to know which
/// rules exist or how many there are.
///
/// <para>
/// Evaluation is sequential and in registration order. Parallelising was considered and rejected:
/// the rules are in memory predicates costing microseconds, so <c>Task.WhenAll</c> would add
/// scheduling overhead and non deterministic ordering in exchange for nothing measurable. Ordering
/// has value in itself, because two runs over the same input produce outcomes in the same sequence,
/// which makes stored assessments comparable.
/// </para>
///
/// <para>
/// One rule throwing fails the whole evaluation rather than being swallowed. Returning a partial
/// assessment would mean recording a decision made on an unknown subset of the rules, which is worse
/// than failing loudly: a transaction that was never properly assessed would look assessed.
/// </para>
/// </remarks>
public sealed class FraudRuleEvaluator
{
    private readonly IFraudRule[] _rules;

    /// <summary>Creates an evaluator over the given rules.</summary>
    /// <exception cref="ArgumentException">
    /// No rules were supplied, a rule has an uninitialised identifier, or two rules share one.
    /// </exception>
    public FraudRuleEvaluator(IEnumerable<IFraudRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = [.. rules];

        if (_rules.Length == 0)
        {
            // An engine with no rules approves everything. That is a silent failure and it is
            // exactly the sort of misconfiguration that goes unnoticed for a long time, so it
            // stops the process at startup instead.
            throw new ArgumentException("At least one rule must be registered.", nameof(rules));
        }

        EnsureIdentifiersAreUsable(_rules);
    }

    /// <summary>How many rules this evaluator will run.</summary>
    public int RuleCount => _rules.Length;

    /// <summary>
    /// Runs every rule against the transaction, returning one outcome per rule in registration order.
    /// </summary>
    public IReadOnlyList<RuleOutcome> Evaluate(TransactionEvent transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var outcomes = new RuleOutcome[_rules.Length];

        for (var i = 0; i < _rules.Length; i++)
        {
            outcomes[i] = _rules[i].Evaluate(transaction);
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

            if (!seen.Add(rule.Id))
            {
                // Two rules sharing an identifier would produce assessments where an outcome cannot
                // be traced back to the rule that made it, and analyst queries counting hits per
                // rule would silently merge the two. Caught at startup rather than in the data.
                throw new ArgumentException(
                    $"More than one rule is registered under the identifier '{rule.Id}'.",
                    nameof(rules));
            }
        }
    }
}
