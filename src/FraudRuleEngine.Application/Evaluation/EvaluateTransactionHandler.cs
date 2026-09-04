using System.Diagnostics;
using FraudRuleEngine.Application.Abstractions;
using FraudRuleEngine.Application.Diagnostics;
using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Application.Evaluation;

/// <summary>
/// Assesses one transaction: enrich, evaluate, score, decide, persist.
/// </summary>
/// <remarks>
/// The only place that sequence is expressed. It lives here rather than in an HTTP handler because it
/// is real logic with an order that matters, and it has to be testable without a web host.
///
/// <para>
/// All the IO is at the edges. Enrichment happens once at the start and persistence once at the end,
/// with nothing in between touching the outside world, which is what keeps evaluation deterministic and
/// its latency predictable.
/// </para>
/// </remarks>
public sealed class EvaluateTransactionHandler
{
    private readonly ICustomerContextSource _contextSource;
    private readonly FraudRuleEvaluator _evaluator;
    private readonly IRiskScoringPolicy _scoringPolicy;
    private readonly IFraudAssessmentStore _store;
    private readonly IRuleSetVersionProvider _ruleSetVersion;
    private readonly TimeProvider _timeProvider;
    private readonly FraudMetrics _metrics;

    public EvaluateTransactionHandler(
        ICustomerContextSource contextSource,
        FraudRuleEvaluator evaluator,
        IRiskScoringPolicy scoringPolicy,
        IFraudAssessmentStore store,
        IRuleSetVersionProvider ruleSetVersion,
        TimeProvider timeProvider,
        FraudMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(contextSource);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(scoringPolicy);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(ruleSetVersion);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(metrics);

        _contextSource = contextSource;
        _evaluator = evaluator;
        _scoringPolicy = scoringPolicy;
        _store = store;
        _ruleSetVersion = ruleSetVersion;
        _timeProvider = timeProvider;
        _metrics = metrics;
    }

    public async Task<FraudAssessment> HandleAsync(
        TransactionEvent transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var start = Stopwatch.GetTimestamp();

        var context = await _contextSource.LoadAsync(transaction, cancellationToken).ConfigureAwait(false);

        var outcomes = _evaluator.Evaluate(context);
        var score = _scoringPolicy.Score(outcomes);
        var decision = _scoringPolicy.Decide(score);

        var assessment = new FraudAssessment(
            AssessmentId.New(),
            transaction,
            score,
            decision,
            outcomes,
            _ruleSetVersion.Current,
            _timeProvider.GetUtcNow());

        // Persisted before returning, not after. A caller that acted on a decline which was never
        // recorded would leave a customer refused with nothing to show why.
        var result = await _store.SaveAsync(transaction, assessment, cancellationToken).ConfigureAwait(false);

        if (result is SaveResult.Saved)
        {
            // Recorded only for a genuine assessment, so a retried duplicate does not inflate the counts.
            _metrics.RecordAssessment(decision, outcomes, Stopwatch.GetElapsedTime(start));
            return assessment;
        }

        // The event had already been assessed, so return the original verdict rather than the one just
        // computed. Two deliveries of one event must produce one answer: replying with a fresh assessment
        // that was never stored would mean the caller acting on a decision nobody can look up, and the
        // second answer can legitimately differ from the first because the customer's history has moved
        // on since.
        var existing = await _store.FindByEventAsync(transaction.EventId, cancellationToken).ConfigureAwait(false);

        // Only reachable if the original was deleted between the failed insert and this read, which
        // nothing in this service does. Loud rather than silently returning the unsaved assessment.
        return existing ?? throw new InvalidOperationException(
            $"Event {transaction.EventId} was reported as already assessed but no assessment was found.");
    }
}
