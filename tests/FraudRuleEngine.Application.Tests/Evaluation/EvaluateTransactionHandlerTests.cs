using FraudRuleEngine.Application.Evaluation;
using FraudRuleEngine.Application.Tests.TestSupport;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Application.Tests.Evaluation;

public sealed class EvaluateTransactionHandlerTests
{
    private static readonly DateTimeOffset EvaluatedAt = new(2026, 9, 2, 19, 0, 0, TimeSpan.Zero);

    private readonly FakeCustomerContextSource _contextSource = new();
    private readonly FakeAssessmentStore _store = new();

    [Fact]
    public async Task Produces_an_assessment_carrying_the_transaction_identifiers()
    {
        var transaction = ATransaction.Valid(eventId: "evt-4471", customerId: "CUST-4471");

        var assessment = await HandlerWith(RuleThatClears()).HandleAsync(transaction, TestContext.Current.CancellationToken);

        assessment.EventId.ShouldBe(transaction.EventId);
        assessment.TransactionId.ShouldBe(transaction.TransactionId);
        assessment.CustomerId.ShouldBe(transaction.CustomerId);
        assessment.Id.IsInitialised.ShouldBeTrue();
    }

    [Fact]
    public async Task Records_every_rule_that_ran_not_only_the_ones_that_fired()
    {
        var assessment = await HandlerWith(RuleThatClears("Quiet"), RuleThatTriggers("Loud"))
            .HandleAsync(ATransaction.Valid(), TestContext.Current.CancellationToken);

        assessment.RuleOutcomes.Count.ShouldBe(2);
        assessment.TriggeredRules.Select(outcome => outcome.RuleId.Value).ShouldBe(["Loud"]);
    }

    [Fact]
    public async Task Scores_and_decides_from_the_outcomes()
    {
        // Two high severity hits total 90 against a decline threshold of 75.
        var assessment = await HandlerWith(
                RuleThatTriggers("A", RuleSeverity.High),
                RuleThatTriggers("B", RuleSeverity.High))
            .HandleAsync(ATransaction.Valid(), TestContext.Current.CancellationToken);

        assessment.RiskScore.Value.ShouldBe(90);
        assessment.Decision.ShouldBe(FraudDecision.Decline);
        assessment.RequiresAttention.ShouldBeTrue();
    }

    [Fact]
    public async Task Approves_a_transaction_nothing_flagged()
    {
        var assessment = await HandlerWith(RuleThatClears()).HandleAsync(ATransaction.Valid(), TestContext.Current.CancellationToken);

        assessment.RiskScore.ShouldBe(RiskScore.Zero);
        assessment.Decision.ShouldBe(FraudDecision.Approve);
        assessment.RequiresAttention.ShouldBeFalse();
    }

    [Fact]
    public async Task Stamps_the_rule_set_version_and_the_time_it_was_evaluated()
    {
        var assessment = await HandlerWith(RuleThatClears()).HandleAsync(ATransaction.Valid(), TestContext.Current.CancellationToken);

        assessment.RuleSetVersion.Value.ShouldBe("test-1");
        assessment.EvaluatedAt.ShouldBe(EvaluatedAt);
    }

    [Fact]
    public async Task Records_when_it_was_evaluated_rather_than_when_the_transaction_occurred()
    {
        // Distinct fields on purpose. An event that took hours to arrive is assessed now, and the
        // difference between the two is how you notice a backlog.
        var transaction = ATransaction.Valid();

        var assessment = await HandlerWith(RuleThatClears()).HandleAsync(transaction, TestContext.Current.CancellationToken);

        assessment.EvaluatedAt.ShouldNotBe(transaction.OccurredAt);
        assessment.EvaluatedAt.ShouldBe(EvaluatedAt);
    }

    [Fact]
    public async Task Enriches_once_per_transaction()
    {
        // The point of loading the context up front. Eight rules must not mean eight round trips.
        await HandlerWith(RuleThatClears("A"), RuleThatClears("B"), RuleThatClears("C"))
            .HandleAsync(ATransaction.Valid(), TestContext.Current.CancellationToken);

        _contextSource.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Persists_the_transaction_alongside_its_assessment()
    {
        var transaction = ATransaction.Valid();

        var assessment = await HandlerWith(RuleThatClears()).HandleAsync(transaction, TestContext.Current.CancellationToken);

        _store.Calls.ShouldBe(1);
        _store.SavedTransaction.ShouldBe(transaction);
        _store.SavedAssessment.ShouldBe(assessment);
    }

    [Fact]
    public async Task Persists_before_returning()
    {
        // A caller that acted on a decline which was never recorded would leave a customer refused
        // with nothing to show why, so a failing store must fail the request.
        var handler = new EvaluateTransactionHandler(
            _contextSource,
            new FraudRuleEvaluator([RuleThatTriggers("A", RuleSeverity.High)]),
            StandardPolicy.Scoring(),
            new ThrowingAssessmentStore(),
            new FixedRuleSetVersion(),
            new FrozenClock(EvaluatedAt));

        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.HandleAsync(ATransaction.Valid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Passes_the_transaction_to_enrichment_so_history_is_loaded_for_the_right_customer()
    {
        TransactionEvent? seen = null;
        var contextSource = new FakeCustomerContextSource(transaction =>
        {
            seen = transaction;
            return FraudEvaluationContext.WithoutHistory(transaction, TimeSpan.FromHours(24));
        });

        var transaction = ATransaction.Valid(customerId: "CUST-9999");

        var handler = new EvaluateTransactionHandler(
            contextSource,
            new FraudRuleEvaluator([RuleThatClears()]),
            StandardPolicy.Scoring(),
            _store,
            new FixedRuleSetVersion(),
            new FrozenClock(EvaluatedAt));

        await handler.HandleAsync(transaction, TestContext.Current.CancellationToken);

        seen.ShouldNotBeNull();
        seen.CustomerId.Value.ShouldBe("CUST-9999");
    }

    [Fact]
    public async Task Gives_each_assessment_its_own_identifier()
    {
        var handler = HandlerWith(RuleThatClears());

        var first = await handler.HandleAsync(ATransaction.Valid(eventId: "evt-1"), TestContext.Current.CancellationToken);
        var second = await handler.HandleAsync(ATransaction.Valid(eventId: "evt-2"), TestContext.Current.CancellationToken);

        first.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public async Task Rejects_a_null_transaction()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => HandlerWith(RuleThatClears()).HandleAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Rejects_missing_dependencies()
    {
        var evaluator = new FraudRuleEvaluator([RuleThatClears()]);
        var policy = StandardPolicy.Scoring();
        var version = new FixedRuleSetVersion();
        var clock = new FrozenClock(EvaluatedAt);

        Should.Throw<ArgumentNullException>(() => new EvaluateTransactionHandler(
            null!, evaluator, policy, _store, version, clock));

        Should.Throw<ArgumentNullException>(() => new EvaluateTransactionHandler(
            _contextSource, null!, policy, _store, version, clock));

        Should.Throw<ArgumentNullException>(() => new EvaluateTransactionHandler(
            _contextSource, evaluator, null!, _store, version, clock));

        Should.Throw<ArgumentNullException>(() => new EvaluateTransactionHandler(
            _contextSource, evaluator, policy, null!, version, clock));

        Should.Throw<ArgumentNullException>(() => new EvaluateTransactionHandler(
            _contextSource, evaluator, policy, _store, null!, clock));

        Should.Throw<ArgumentNullException>(() => new EvaluateTransactionHandler(
            _contextSource, evaluator, policy, _store, version, null!));
    }

    private EvaluateTransactionHandler HandlerWith(params IFraudRule[] rules) =>
        new(
            _contextSource,
            new FraudRuleEvaluator(rules),
            StandardPolicy.Scoring(),
            _store,
            new FixedRuleSetVersion(),
            new FrozenClock(EvaluatedAt));

    private static StubRule RuleThatClears(string id = "Quiet") => new(id, null);

    private static StubRule RuleThatTriggers(string id = "Loud", RuleSeverity severity = RuleSeverity.Medium) =>
        new(id, severity);

    private sealed class StubRule : IFraudRule
    {
        private readonly RuleSeverity? _severity;

        internal StubRule(string id, RuleSeverity? severity)
        {
            Id = RuleId.From(id);
            _severity = severity;
        }

        public RuleId Id { get; }

        public RuleOutcome Evaluate(FraudEvaluationContext context) => _severity is { } severity
            ? RuleOutcome.Triggered(Id, severity, $"{Id} fired.")
            : RuleOutcome.Clear(Id, $"{Id} did not fire.");
    }
}

public sealed class IdempotentEvaluationTests
{
    private static readonly DateTimeOffset EvaluatedAt = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeCustomerContextSource _contextSource = new();
    private readonly FakeAssessmentStore _store = new();

    [Fact]
    public async Task Returns_the_original_verdict_when_the_event_was_already_assessed()
    {
        // Two deliveries of one event must produce one answer. Returning the freshly computed assessment
        // would hand the caller a decision nobody can look up, and it can legitimately differ from the
        // stored one because the customer's history has moved on since.
        var transaction = ATransaction.Valid(eventId: "evt-dup");
        var handler = Handler();

        var first = await handler.HandleAsync(transaction, TestContext.Current.CancellationToken);

        _store.ExistingForNextSave = first;
        var second = await handler.HandleAsync(transaction, TestContext.Current.CancellationToken);

        second.Id.ShouldBe(first.Id);
        second.RiskScore.ShouldBe(first.RiskScore);
        second.EvaluatedAt.ShouldBe(first.EvaluatedAt);
    }

    [Fact]
    public async Task Looks_the_original_up_only_when_the_save_clashed()
    {
        var handler = Handler();

        await handler.HandleAsync(ATransaction.Valid(), TestContext.Current.CancellationToken);

        _store.LookupCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Fails_loudly_if_a_clash_is_reported_but_nothing_is_stored()
    {
        // Should be unreachable. Returning the unsaved assessment instead would be worse than throwing,
        // because the caller would act on a verdict that does not exist.
        var handler = new EvaluateTransactionHandler(
            _contextSource,
            new FraudRuleEvaluator([new StubRule("Quiet", null)]),
            StandardPolicy.Scoring(),
            new InconsistentAssessmentStore(),
            new FixedRuleSetVersion(),
            new FrozenClock(EvaluatedAt));

        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.HandleAsync(ATransaction.Valid(), TestContext.Current.CancellationToken));
    }

    private EvaluateTransactionHandler Handler() =>
        new(
            _contextSource,
            new FraudRuleEvaluator([new StubRule("Loud", RuleSeverity.High)]),
            StandardPolicy.Scoring(),
            _store,
            new FixedRuleSetVersion(),
            new FrozenClock(EvaluatedAt));

    private sealed class StubRule : IFraudRule
    {
        private readonly RuleSeverity? _severity;

        internal StubRule(string id, RuleSeverity? severity)
        {
            Id = RuleId.From(id);
            _severity = severity;
        }

        public RuleId Id { get; }

        public RuleOutcome Evaluate(FraudEvaluationContext context) => _severity is { } severity
            ? RuleOutcome.Triggered(Id, severity, $"{Id} fired.")
            : RuleOutcome.Clear(Id, $"{Id} did not fire.");
    }
}
