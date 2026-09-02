using FraudRuleEngine.Domain.Rules;

namespace FraudRuleEngine.Domain.Scoring;

/// <summary>
/// Sums the weights of every rule that fired, then bands the total into a decision.
/// </summary>
/// <remarks>
/// Deterministic and explainable, which for a fraud engine matters more than being clever. Given the
/// same outcomes it produces the same score every time, and the arithmetic can be shown to a customer,
/// an analyst or a regulator.
///
/// <para>
/// The consequence of plain summation is that no single rule can decline a transaction on its own. The
/// heaviest signal available is 45, and the decline threshold is 75, so a decline needs corroboration.
/// That is deliberate rather than an accident of the numbers. Declining on one signal is how a bank
/// strands a customer whose only crime was buying something expensive on holiday.
/// </para>
///
/// <para>
/// It follows that a deny list hit, which is a policy violation rather than a probability, only reaches
/// Review by itself. If the business wanted that to refuse outright it would be a change here rather
/// than in the rule, since a rule's job is to report what it saw.
/// </para>
/// </remarks>
public sealed class WeightedRiskScoringPolicy : IRiskScoringPolicy
{
    /// <summary>
    /// Points contributed by one triggered rule, by how strong that rule considers its hit.
    /// </summary>
    /// <remarks>
    /// The gaps are uneven on purpose. Two weak signals are worth less than one strong one, because a
    /// pair of things that are individually common is still fairly common.
    /// </remarks>
    private readonly Dictionary<RuleSeverity, int> _weights = new()
    {
        [RuleSeverity.Low] = 10,
        [RuleSeverity.Medium] = 25,
        [RuleSeverity.High] = 45,
    };

    /// <summary>At or above this, a person should look at the transaction.</summary>
    private readonly RiskScore _reviewThreshold = new(40);

    /// <summary>At or above this, refuse it.</summary>
    private readonly RiskScore _declineThreshold = new(75);

    public RiskScore Score(IReadOnlyList<RuleOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        // Scoring nothing would produce zero and approve a transaction no rule examined. That is the
        // same silent failure the evaluator refuses at startup, so it is refused here too.
        if (outcomes.Count == 0)
        {
            throw new ArgumentException(
                "Cannot score a transaction that no rule evaluated.",
                nameof(outcomes));
        }

        var total = 0;

        foreach (var outcome in outcomes)
        {
            if (outcome.IsTriggered && _weights.TryGetValue(outcome.Severity, out var weight))
            {
                total += weight;
            }
        }

        // Clamped rather than thrown. Eight rules at maximum severity total well over a hundred, and a
        // transaction that trips everything is not a programming error.
        return RiskScore.FromTotal(total);
    }

    public FraudDecision Decide(RiskScore score)
    {
        // Both boundaries are inclusive. A threshold of 40 that excludes 40 is really 41, and the
        // person tuning it would have no way of knowing.
        if (score >= _declineThreshold)
        {
            return FraudDecision.Decline;
        }

        return score >= _reviewThreshold
            ? FraudDecision.Review
            : FraudDecision.Approve;
    }
}
