using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;

namespace FraudRuleEngine.Domain.Tests.Scoring;

public sealed class WeightedRiskScoringPolicyTests
{
    private readonly WeightedRiskScoringPolicy _policy = new();

    [Fact]
    public void Scores_zero_when_nothing_fired()
    {
        var score = _policy.Score([Clear("First"), Clear("Second"), Clear("Third")]);

        score.ShouldBe(RiskScore.Zero);
        _policy.Decide(score).ShouldBe(FraudDecision.Approve);
    }

    [Theory]
    [InlineData(RuleSeverity.Low, 10)]
    [InlineData(RuleSeverity.Medium, 25)]
    [InlineData(RuleSeverity.High, 45)]
    public void Weights_a_single_hit_by_its_severity(RuleSeverity severity, int expected)
    {
        _policy.Score([Triggered("Only", severity)]).Value.ShouldBe(expected);
    }

    [Fact]
    public void Sums_the_weights_of_everything_that_fired()
    {
        var score = _policy.Score(
        [
            Triggered("Weak", RuleSeverity.Low),
            Clear("Quiet"),
            Triggered("Middling", RuleSeverity.Medium),
            Triggered("Strong", RuleSeverity.High),
        ]);

        score.Value.ShouldBe(80);
    }

    [Fact]
    public void Ignores_rules_that_did_not_fire()
    {
        // Clear outcomes are persisted and carry a reason, so they are present in the list. They must
        // contribute nothing, or every rule that ran would inflate the score.
        var withClears = _policy.Score([Triggered("Only", RuleSeverity.Medium), Clear("A"), Clear("B")]);
        var withoutClears = _policy.Score([Triggered("Only", RuleSeverity.Medium)]);

        withClears.ShouldBe(withoutClears);
    }

    [Fact]
    public void Caps_the_score_at_one_hundred()
    {
        // Four high severity hits total 180. A transaction that trips everything is not a programming
        // error, so the total is clamped rather than rejected.
        var score = _policy.Score(
        [
            Triggered("A", RuleSeverity.High),
            Triggered("B", RuleSeverity.High),
            Triggered("C", RuleSeverity.High),
            Triggered("D", RuleSeverity.High),
        ]);

        score.Value.ShouldBe(RiskScore.Maximum);
    }

    [Fact]
    public void Refuses_to_score_a_transaction_no_rule_examined()
    {
        // An empty list would score zero and approve. Same silent failure the evaluator refuses at
        // startup, refused here too.
        var act = () => { _ = _policy.Score([]); };

        act.ShouldThrow<ArgumentException>()
            .ParamName.ShouldBe("outcomes");
    }

    [Fact]
    public void Produces_the_same_score_for_the_same_outcomes_in_any_order()
    {
        // Summation is commutative, and the assessment has to be reproducible: two evaluations of one
        // transaction must not disagree because rules ran in a different sequence.
        var forwards = _policy.Score(
            [Triggered("A", RuleSeverity.Low), Triggered("B", RuleSeverity.High)]);

        var backwards = _policy.Score(
            [Triggered("B", RuleSeverity.High), Triggered("A", RuleSeverity.Low)]);

        forwards.ShouldBe(backwards);
    }

    [Theory]
    [InlineData(0, FraudDecision.Approve)]
    [InlineData(1, FraudDecision.Approve)]
    [InlineData(39, FraudDecision.Approve)]
    [InlineData(40, FraudDecision.Review)]
    [InlineData(41, FraudDecision.Review)]
    [InlineData(74, FraudDecision.Review)]
    [InlineData(75, FraudDecision.Decline)]
    [InlineData(76, FraudDecision.Decline)]
    [InlineData(100, FraudDecision.Decline)]
    public void Bands_a_score_into_a_decision(int score, FraudDecision expected)
    {
        // Every boundary pinned in both directions. A threshold of 40 that excludes 40 is really 41,
        // and whoever tunes it would have no way of knowing which was meant.
        _policy.Decide(new RiskScore(score)).ShouldBe(expected);
    }

    [Fact]
    public void No_single_rule_can_decline_a_transaction_on_its_own()
    {
        // The most important property of the numbers, and deliberate rather than incidental. The
        // heaviest signal available is 45 against a decline threshold of 75, so refusing needs
        // corroboration. Declining on one signal is how a bank strands a customer whose only mistake
        // was buying something expensive on holiday.
        foreach (var severity in new[] { RuleSeverity.Low, RuleSeverity.Medium, RuleSeverity.High })
        {
            var score = _policy.Score([Triggered("Only", severity)]);

            _policy.Decide(score).ShouldNotBe(FraudDecision.Decline);
        }
    }

    [Fact]
    public void A_single_strong_hit_reaches_review()
    {
        var score = _policy.Score([Triggered("Strong", RuleSeverity.High)]);

        _policy.Decide(score).ShouldBe(FraudDecision.Review);
    }

    [Fact]
    public void Two_strong_hits_reach_decline()
    {
        var score = _policy.Score(
            [Triggered("A", RuleSeverity.High), Triggered("B", RuleSeverity.High)]);

        _policy.Decide(score).ShouldBe(FraudDecision.Decline);
    }

    [Fact]
    public void Two_weak_hits_are_worth_less_than_one_strong_one()
    {
        // The weights are unevenly spaced on purpose. A pair of things that are each individually
        // common is still fairly common.
        var twoWeak = _policy.Score(
            [Triggered("A", RuleSeverity.Low), Triggered("B", RuleSeverity.Low)]);

        var oneStrong = _policy.Score([Triggered("C", RuleSeverity.High)]);

        (twoWeak < oneStrong).ShouldBeTrue();
    }

    [Fact]
    public void Weak_signals_still_accumulate_into_a_review()
    {
        // What the category and unusual hour rules are for. None of them is worth acting on alone, and
        // four of them together are.
        var score = _policy.Score(
        [
            Triggered("A", RuleSeverity.Low),
            Triggered("B", RuleSeverity.Low),
            Triggered("C", RuleSeverity.Low),
            Triggered("D", RuleSeverity.Low),
        ]);

        score.Value.ShouldBe(40);
        _policy.Decide(score).ShouldBe(FraudDecision.Review);
    }

    [Fact]
    public void Rejects_a_null_outcome_list()
    {
        Should.Throw<ArgumentNullException>(() => _policy.Score(null!));
    }

    private static RuleOutcome Triggered(string id, RuleSeverity severity) =>
        RuleOutcome.Triggered(RuleId.From(id), severity, $"{id} fired.");

    private static RuleOutcome Clear(string id) =>
        RuleOutcome.Clear(RuleId.From(id), $"{id} did not fire.");
}
