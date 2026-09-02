namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// One fraud rule. Evaluation is synchronous and does no IO, so anything a rule needs beyond the
/// transaction has to be loaded into the context before it runs.
/// See docs/adr/0003-rules-as-pure-functions.md.
/// </summary>
public interface IFraudRule
{
    /// <summary>Stable across releases, because it is persisted on every outcome.</summary>
    RuleId Id { get; }

    /// <summary>Returns an outcome whether or not the rule fires.</summary>
    RuleOutcome Evaluate(FraudEvaluationContext context);
}
