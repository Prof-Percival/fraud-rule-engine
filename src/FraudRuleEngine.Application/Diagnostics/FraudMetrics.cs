using System.Diagnostics.Metrics;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;

namespace FraudRuleEngine.Application.Diagnostics;

/// <summary>
/// Fraud specific metrics: how many transactions are assessed, how the decisions split, which rules
/// fire, and how long assessment takes. A jump in one rule's trigger rate is an incident signal.
/// </summary>
/// <remarks>
/// Uses the in-box <see cref="Meter"/>, so OpenTelemetry picks the instruments up by name without this
/// layer depending on a telemetry package.
/// </remarks>
public sealed class FraudMetrics : IDisposable
{
    public const string MeterName = "FraudRuleEngine";

    private readonly Meter _meter;
    private readonly Counter<long> _evaluations;
    private readonly Counter<long> _decisions;
    private readonly Counter<long> _ruleTriggers;
    private readonly Histogram<double> _duration;

    public FraudMetrics()
    {
        _meter = new Meter(MeterName);
        _evaluations = _meter.CreateCounter<long>(
            "fraud.evaluations", "{evaluation}", "Transactions assessed.");
        _decisions = _meter.CreateCounter<long>(
            "fraud.decisions", "{decision}", "Assessments by decision.");
        _ruleTriggers = _meter.CreateCounter<long>(
            "fraud.rule_triggers", "{trigger}", "Rule triggers, by rule.");
        _duration = _meter.CreateHistogram<double>(
            "fraud.evaluation.duration", "ms", "Time to assess one transaction.");
    }

    public void RecordAssessment(FraudDecision decision, IReadOnlyList<RuleOutcome> outcomes, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        _evaluations.Add(1);
        _decisions.Add(1, new KeyValuePair<string, object?>("decision", decision.ToString()));
        _duration.Record(elapsed.TotalMilliseconds);

        foreach (var outcome in outcomes)
        {
            if (outcome.IsTriggered)
            {
                _ruleTriggers.Add(1, new KeyValuePair<string, object?>("rule", outcome.RuleId.ToString()));
            }
        }
    }

    public void Dispose() => _meter.Dispose();
}
