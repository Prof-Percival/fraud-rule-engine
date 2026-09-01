using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// One fraud rule.
/// </summary>
/// <remarks>
/// <see cref="Evaluate"/> is synchronous and performs no IO. That constraint is the point of the
/// interface rather than an incidental detail of it, and everything else in the design follows from
/// it. Rules cannot query a database, call a service or await anything, which means:
///
/// <list type="bullet">
/// <item>every rule test is a plain in memory test with no mocks and no fixtures;</item>
/// <item>evaluation latency does not grow as rules are added, because none of them do IO;</item>
/// <item>all rules see one consistent view of the data, so an assessment is reproducible.</item>
/// </list>
///
/// <para>
/// The cost is that anything a rule needs beyond the transaction has to be loaded before evaluation
/// starts, by something that does not yet know which rules will fire. See
/// docs/adr/0003-rules-as-pure-functions.md for that tradeoff and what would be done about it if it
/// ever mattered.
/// </para>
///
/// <para>
/// Adding a rule is one class, one registration and one test file. No existing rule is touched,
/// which is the property being bought here.
/// </para>
/// </remarks>
public interface IFraudRule
{
    /// <summary>
    /// Identifies this rule. Stable across releases, since it is persisted on every outcome.
    /// </summary>
    RuleId Id { get; }

    /// <summary>
    /// Judges one transaction, returning an outcome whether or not the rule fires.
    /// </summary>
    RuleOutcome Evaluate(TransactionEvent transaction);
}
