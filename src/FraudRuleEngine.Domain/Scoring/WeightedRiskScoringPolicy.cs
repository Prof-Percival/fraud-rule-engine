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
/// Whether a single rule can decline on its own is now a matter of configuration rather than of these
/// numbers. With the shipped weights the heaviest signal is below the decline threshold, so a decline
/// needs corroboration, which is how a bank avoids stranding a customer whose only crime was buying
/// something expensive on holiday. An operator who sets a weight at or above the decline threshold
/// changes that, deliberately.
/// </para>
/// </remarks>
public sealed class WeightedRiskScoringPolicy : IRiskScoringPolicy
{
    private readonly IReadOnlyDictionary<RuleSeverity, int> _weights;
    private readonly RiskScore _reviewThreshold;
    private readonly RiskScore _declineThreshold;

    // Guards are the last line, not the first. Configuration is validated at startup with an operator
    // friendly message; these keep the policy from existing in a state that would score incoherently.
    public WeightedRiskScoringPolicy(
        IReadOnlyDictionary<RuleSeverity, int> weights,
        RiskScore reviewThreshold,
        RiskScore declineThreshold)
    {
        ArgumentNullException.ThrowIfNull(weights);

        // None is the severity of a clear outcome, which never contributes, so it needs no weight.
        foreach (var severity in Enum.GetValues<RuleSeverity>())
        {
            if (severity == RuleSeverity.None)
            {
                continue;
            }

            if (!weights.TryGetValue(severity, out var weight))
            {
                throw new ArgumentException(
                    $"No weight is configured for severity {severity}.",
                    nameof(weights));
            }

            // A zero weight silently retires a whole severity band, which is the quiet failure this
            // scoring is meant to make impossible. Disabling a rule is done by not registering it.
            if (weight <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(weights),
                    weight,
                    $"Weight for severity {severity} must be positive.");
            }
        }

        if (reviewThreshold >= declineThreshold)
        {
            throw new ArgumentException(
                $"Review threshold {reviewThreshold} must be below the decline threshold "
                    + $"{declineThreshold}, otherwise no transaction can reach review.",
                nameof(reviewThreshold));
        }

        _weights = weights;
        _reviewThreshold = reviewThreshold;
        _declineThreshold = declineThreshold;
    }

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

        // Clamped rather than thrown. Enough rules at high severity total over a hundred, and a
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
