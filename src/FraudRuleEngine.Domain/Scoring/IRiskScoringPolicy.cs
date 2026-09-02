using FraudRuleEngine.Domain.Rules;

namespace FraudRuleEngine.Domain.Scoring;

/// <summary>
/// Turns a set of rule outcomes into a score, and a score into a decision.
/// </summary>
/// <remarks>
/// The two steps are separate methods because they are tuned separately and for different reasons.
/// Weighting answers "how much does this kind of signal count", banding answers "how suspicious is
/// suspicious enough to act". Changing one should not require reasoning about the other.
///
/// <para>
/// Behind an interface so the whole policy can be swapped without touching a rule. Rules report
/// severity and nothing else, so they are unaffected by how it is weighed.
/// </para>
/// </remarks>
public interface IRiskScoringPolicy
{
    /// <exception cref="ArgumentException">
    /// No outcomes were supplied, which would score zero and approve a transaction nothing examined.
    /// </exception>
    RiskScore Score(IReadOnlyList<RuleOutcome> outcomes);

    FraudDecision Decide(RiskScore score);
}
