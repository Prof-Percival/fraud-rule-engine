using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class FraudRuleEvaluatorTests
{
    private readonly TransactionEvent _transaction = TransactionEventBuilder.AValidEvent().Build();

    [Fact]
    public void Returns_one_outcome_per_rule_even_when_none_fire()
    {
        var evaluator = new FraudRuleEvaluator(
        [
            StubRule.ThatClears("First"),
            StubRule.ThatClears("Second"),
            StubRule.ThatClears("Third"),
        ]);

        var outcomes = evaluator.Evaluate(_transaction);

        outcomes.Count.ShouldBe(3);
        outcomes.ShouldAllBe(outcome => !outcome.IsTriggered);
    }

    [Fact]
    public void Returns_outcomes_in_registration_order()
    {
        // Ordering is not cosmetic. Two runs over the same transaction produce outcomes in the same
        // sequence, which is what makes two stored assessments comparable.
        var evaluator = new FraudRuleEvaluator(
        [
            StubRule.ThatClears("Alpha"),
            StubRule.ThatTriggers("Bravo"),
            StubRule.ThatClears("Charlie"),
        ]);

        var outcomes = evaluator.Evaluate(_transaction);

        outcomes.Select(outcome => outcome.RuleId.Value)
            .ShouldBe(["Alpha", "Bravo", "Charlie"]);
    }

    [Fact]
    public void Runs_every_rule_rather_than_stopping_at_the_first_hit()
    {
        // Short circuiting would be faster and would lose the reason a transaction was flagged three
        // times over, which is exactly the information that separates a review from a decline.
        var evaluator = new FraudRuleEvaluator(
        [
            StubRule.ThatTriggers("First"),
            StubRule.ThatTriggers("Second"),
        ]);

        evaluator.Evaluate(_transaction).ShouldAllBe(outcome => outcome.IsTriggered);
    }

    [Fact]
    public void Reports_how_many_rules_it_will_run()
    {
        new FraudRuleEvaluator([StubRule.ThatClears("Only")]).RuleCount.ShouldBe(1);
    }

    [Fact]
    public void Refuses_to_be_built_with_no_rules()
    {
        // An engine with no rules approves everything, silently. Failing at startup beats discovering
        // that a month later.
        var act = () => { _ = new FraudRuleEvaluator([]); };

        act.ShouldThrow<ArgumentException>()
            .ParamName.ShouldBe("rules");
    }

    [Fact]
    public void Refuses_two_rules_sharing_an_identifier()
    {
        // Outcomes are stored against the rule identifier. Two rules sharing one would merge in any
        // query counting hits per rule, and an outcome could not be traced back to its rule.
        var act = () => { _ = new FraudRuleEvaluator([StubRule.ThatClears("Same"), StubRule.ThatTriggers("Same")]); };

        act.ShouldThrow<ArgumentException>()
            .Message.ShouldContain("Same");
    }

    [Fact]
    public void Refuses_a_rule_without_an_identifier()
    {
        var act = () => { _ = new FraudRuleEvaluator([new UnidentifiedRule()]); };

        act.ShouldThrow<ArgumentException>()
            .Message.ShouldContain(nameof(UnidentifiedRule));
    }

    [Fact]
    public void Lets_a_failing_rule_fail_the_whole_evaluation()
    {
        // Swallowing it would produce an assessment made on an unknown subset of the rules. A
        // transaction that was never properly assessed would look assessed, which is worse than an
        // error somebody has to deal with.
        var evaluator = new FraudRuleEvaluator([StubRule.ThatClears("Fine"), new ThrowingRule()]);

        Should.Throw<InvalidOperationException>(() => evaluator.Evaluate(_transaction));
    }

    [Fact]
    public void Rejects_a_null_transaction()
    {
        var evaluator = new FraudRuleEvaluator([StubRule.ThatClears("Only")]);

        Should.Throw<ArgumentNullException>(() => evaluator.Evaluate(null!));
    }

    /// <summary>
    /// A rule with a fixed answer. Hand written rather than mocked, because the interface has two
    /// members and a mocking framework would add a dependency and a setup call to say less.
    /// </summary>
    private sealed class StubRule : IFraudRule
    {
        private readonly bool _triggers;

        private StubRule(string id, bool triggers)
        {
            Id = RuleId.From(id);
            _triggers = triggers;
        }

        public RuleId Id { get; }

        public static StubRule ThatTriggers(string id) => new(id, triggers: true);

        public static StubRule ThatClears(string id) => new(id, triggers: false);

        public RuleOutcome Evaluate(TransactionEvent transaction) => _triggers
            ? RuleOutcome.Triggered(Id, RuleSeverity.Medium, "Stub triggered.")
            : RuleOutcome.Clear(Id, "Stub did not trigger.");
    }

    private sealed class UnidentifiedRule : IFraudRule
    {
        public RuleId Id => default;

        public RuleOutcome Evaluate(TransactionEvent transaction) =>
            throw new NotSupportedException("Never reached, the evaluator rejects this rule at construction.");
    }

    private sealed class ThrowingRule : IFraudRule
    {
        public RuleId Id { get; } = RuleId.From("Throwing");

        public RuleOutcome Evaluate(TransactionEvent transaction) =>
            throw new InvalidOperationException("Reference data unavailable.");
    }
}
