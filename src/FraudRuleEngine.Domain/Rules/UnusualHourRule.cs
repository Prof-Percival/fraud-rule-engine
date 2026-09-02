namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Flags a transaction in the small hours, local to where it happened. Compromised cards get used while
/// the cardholder is asleep and unlikely to see a notification.
/// </summary>
/// <remarks>
/// The window is applied to local time, not UTC, and that distinction is the rule. Three in the morning
/// in Johannesburg is one in the morning UTC; in Los Angeles it is eleven the previous morning. A UTC
/// window would flag ordinary afternoon spending in one timezone and miss genuine overnight activity in
/// another.
///
/// <para>
/// Reports <see cref="RuleSeverity.Low"/> for two reasons. It uses the local time where the transaction
/// happened rather than where the customer lives, so somebody shopping abroad is judged on that hour,
/// and the window is fixed rather than learned, so anyone working nights trips it regularly.
/// </para>
/// </remarks>
public sealed class UnusualHourRule : IFraudRule
{
    /// <summary>Inclusive. One through five avoids the evening, when plenty of people shop online.</summary>
    private readonly TimeOnly _windowStart = new(1, 0);

    /// <summary>Exclusive, so 05:00 belongs to the ordinary day.</summary>
    private readonly TimeOnly _windowEnd = new(5, 0);

    public RuleId Id { get; } = RuleId.From("UnusualHour");

    public RuleOutcome Evaluate(FraudEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var localTime = context.Transaction.LocalTimeOfDay;

        if (localTime < _windowStart || localTime >= _windowEnd)
        {
            return RuleOutcome.Clear(
                Id,
                $"Local time {localTime:HH:mm} is outside the unusual hours window "
                    + $"of {_windowStart:HH:mm} to {_windowEnd:HH:mm}.");
        }

        return RuleOutcome.Triggered(
            Id,
            RuleSeverity.Low,
            $"Local time {localTime:HH:mm} falls within the unusual hours window "
                + $"of {_windowStart:HH:mm} to {_windowEnd:HH:mm}.");
    }
}
