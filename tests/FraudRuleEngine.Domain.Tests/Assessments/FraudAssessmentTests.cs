using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;
using FraudRuleEngine.Domain.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Assessments;

public sealed class FraudAssessmentTests
{
    private static readonly DateTimeOffset EvaluatedAt = new(2026, 9, 2, 19, 0, 0, TimeSpan.Zero);

    private static readonly RuleOutcome AnOutcome = RuleOutcome.Clear(RuleId.From("Only"), "Nothing.");

    [Fact]
    public void Copies_the_identifiers_it_needs_from_the_transaction()
    {
        // Copied rather than reached for through a navigation property, so assessments can be queried
        // by customer or by event without joining.
        var transaction = TransactionEventBuilder.AValidEvent()
            .WithEventId(EventId.From("evt-4471"))
            .WithCustomerId(CustomerId.From("CUST-4471"))
            .Build();

        var assessment = An(transaction: transaction);

        assessment.EventId.ShouldBe(transaction.EventId);
        assessment.TransactionId.ShouldBe(transaction.TransactionId);
        assessment.CustomerId.ShouldBe(transaction.CustomerId);
    }

    [Fact]
    public void Separates_the_rules_that_fired_from_the_full_set()
    {
        var assessment = An(outcomes:
        [
            RuleOutcome.Clear(RuleId.From("Quiet"), "Nothing."),
            RuleOutcome.Triggered(RuleId.From("Loud"), RuleSeverity.High, "Something."),
            RuleOutcome.Clear(RuleId.From("AlsoQuiet"), "Nothing."),
        ]);

        assessment.RuleOutcomes.Count.ShouldBe(3);
        assessment.TriggeredRules.Select(outcome => outcome.RuleId.Value).ShouldBe(["Loud"]);
    }

    [Theory]
    [InlineData(FraudDecision.Approve, false)]
    [InlineData(FraudDecision.Review, true)]
    [InlineData(FraudDecision.Decline, true)]
    public void Knows_whether_somebody_has_to_look_at_it(FraudDecision decision, bool expected)
    {
        An(decision: decision).RequiresAttention.ShouldBe(expected);
    }

    [Fact]
    public void Keeps_its_own_copy_of_the_outcome_list()
    {
        // Guards against a caller mutating the list afterwards and silently changing stored evidence.
        var outcomes = new List<RuleOutcome> { AnOutcome };

        var assessment = An(outcomes: outcomes);
        outcomes.Add(RuleOutcome.Triggered(RuleId.From("Injected"), RuleSeverity.High, "Added later."));

        assessment.RuleOutcomes.Count.ShouldBe(1);
    }

    // The validation tests below construct directly with every argument spelled out. Routing them
    // through a helper that fills in defaults would repair the very argument under test, and the test
    // would pass while asserting nothing.

    [Fact]
    public void Must_have_an_identifier()
    {
        var act = () =>
        {
            _ = new FraudAssessment(
                default,
                TransactionEventBuilder.AValidEvent().Build(),
                RiskScore.Zero,
                FraudDecision.Approve,
                [AnOutcome],
                RuleSetVersion.From("v1"),
                EvaluatedAt);
        };

        act.ShouldThrow<ArgumentException>()
            .ParamName.ShouldBe("id");
    }

    [Fact]
    public void Must_record_the_rule_set_version_that_produced_it()
    {
        // Without it an assessment cannot be explained once the thresholds have changed, which is an
        // audit requirement rather than a nicety.
        var act = () =>
        {
            _ = new FraudAssessment(
                AssessmentId.New(),
                TransactionEventBuilder.AValidEvent().Build(),
                RiskScore.Zero,
                FraudDecision.Approve,
                [AnOutcome],
                default,
                EvaluatedAt);
        };

        act.ShouldThrow<ArgumentException>()
            .ParamName.ShouldBe("ruleSetVersion");
    }

    [Fact]
    public void Must_record_the_outcome_of_every_rule_that_ran()
    {
        // An assessment with no outcomes claims a verdict nothing supports, and would be
        // indistinguishable from one where every rule was disabled.
        var act = () =>
        {
            _ = new FraudAssessment(
                AssessmentId.New(),
                TransactionEventBuilder.AValidEvent().Build(),
                RiskScore.Zero,
                FraudDecision.Approve,
                [],
                RuleSetVersion.From("v1"),
                EvaluatedAt);
        };

        act.ShouldThrow<ArgumentException>()
            .ParamName.ShouldBe("ruleOutcomes");
    }

    [Fact]
    public void Must_record_when_it_was_made()
    {
        var act = () =>
        {
            _ = new FraudAssessment(
                AssessmentId.New(),
                TransactionEventBuilder.AValidEvent().Build(),
                RiskScore.Zero,
                FraudDecision.Approve,
                [AnOutcome],
                RuleSetVersion.From("v1"),
                default);
        };

        act.ShouldThrow<ArgumentOutOfRangeException>()
            .ParamName.ShouldBe("evaluatedAt");
    }

    [Fact]
    public void Rejects_a_missing_transaction_or_outcome_list()
    {
        Should.Throw<ArgumentNullException>(() => new FraudAssessment(
            AssessmentId.New(),
            null!,
            RiskScore.Zero,
            FraudDecision.Approve,
            [AnOutcome],
            RuleSetVersion.From("v1"),
            EvaluatedAt));

        Should.Throw<ArgumentNullException>(() => new FraudAssessment(
            AssessmentId.New(),
            TransactionEventBuilder.AValidEvent().Build(),
            RiskScore.Zero,
            FraudDecision.Approve,
            null!,
            RuleSetVersion.From("v1"),
            EvaluatedAt));
    }

    /// <summary>A valid assessment, for tests that vary one thing about it.</summary>
    private static FraudAssessment An(
        TransactionEvent? transaction = null,
        RiskScore riskScore = default,
        FraudDecision decision = FraudDecision.Approve,
        IReadOnlyList<RuleOutcome>? outcomes = null) =>
        new(
            AssessmentId.New(),
            transaction ?? TransactionEventBuilder.AValidEvent().Build(),
            riskScore,
            decision,
            outcomes ?? [AnOutcome],
            RuleSetVersion.From("v1"),
            EvaluatedAt);
}
