using FraudRuleEngine.Application.Abstractions;
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

    public EvaluateTransactionHandler(
        ICustomerContextSource contextSource,
        FraudRuleEvaluator evaluator,
        IRiskScoringPolicy scoringPolicy,
        IFraudAssessmentStore store,
        IRuleSetVersionProvider ruleSetVersion,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(contextSource);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(scoringPolicy);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(ruleSetVersion);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _contextSource = contextSource;
        _evaluator = evaluator;
        _scoringPolicy = scoringPolicy;
        _store = store;
        _ruleSetVersion = ruleSetVersion;
        _timeProvider = timeProvider;
    }

    public async Task<FraudAssessment> HandleAsync(
        TransactionEvent transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

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
        await _store.SaveAsync(transaction, assessment, cancellationToken).ConfigureAwait(false);

        return assessment;
    }
}
