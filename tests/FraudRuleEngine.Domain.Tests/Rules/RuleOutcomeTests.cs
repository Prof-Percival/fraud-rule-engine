using FraudRuleEngine.Domain.Rules;

namespace FraudRuleEngine.Domain.Tests.Rules;

public sealed class RuleOutcomeTests
{
    private static readonly RuleId AnyRule = RuleId.From("AnyRule");

    [Fact]
    public void A_triggered_outcome_carries_its_severity_and_reason()
    {
        var outcome = RuleOutcome.Triggered(AnyRule, RuleSeverity.High, "Amount 48500 ZAR is large.");

        outcome.IsTriggered.ShouldBeTrue();
        outcome.Severity.ShouldBe(RuleSeverity.High);
        outcome.Reason.ShouldBe("Amount 48500 ZAR is large.");
        outcome.RuleId.ShouldBe(AnyRule);
    }

    [Fact]
    public void A_clear_outcome_still_carries_a_reason()
    {
        var outcome = RuleOutcome.Clear(AnyRule, "Amount is below the threshold.");

        outcome.IsTriggered.ShouldBeFalse();
        outcome.Severity.ShouldBe(RuleSeverity.None);
        outcome.Reason.ShouldBe("Amount is below the threshold.");
    }

    [Fact]
    public void A_triggered_outcome_must_report_a_severity()
    {
        // None means "did not fire", so allowing it here would let a rule claim a hit while telling
        // the scoring policy the hit is worth nothing.
        var act = () => { _ = RuleOutcome.Triggered(AnyRule, RuleSeverity.None, "Something."); };

        act.ShouldThrow<ArgumentOutOfRangeException>()
            .ParamName.ShouldBe("severity");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_outcome_must_explain_itself(string? reason)
    {
        Should.Throw<ArgumentException>(() => RuleOutcome.Triggered(AnyRule, RuleSeverity.Low, reason!));
        Should.Throw<ArgumentException>(() => RuleOutcome.Clear(AnyRule, reason!));
    }

    [Fact]
    public void An_outcome_must_name_the_rule_that_produced_it()
    {
        // Outcomes are persisted per rule per assessment. One that cannot say which rule made it is
        // a row nobody can interpret afterwards.
        Should.Throw<ArgumentException>(
            () => RuleOutcome.Triggered(default, RuleSeverity.Low, "Something."));

        Should.Throw<ArgumentException>(
            () => RuleOutcome.Clear(default, "Something."));
    }

    [Fact]
    public void Outcomes_compare_by_value()
    {
        var first = RuleOutcome.Triggered(AnyRule, RuleSeverity.Medium, "Same reason.");
        var second = RuleOutcome.Triggered(AnyRule, RuleSeverity.Medium, "Same reason.");
        var different = RuleOutcome.Triggered(AnyRule, RuleSeverity.High, "Same reason.");

        first.ShouldBe(second);
        first.ShouldNotBe(different);
    }
}
