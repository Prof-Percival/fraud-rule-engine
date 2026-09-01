using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Flags a transaction that took place during the small hours, local to where it happened.
/// </summary>
public sealed class UnusualHourRule : IFraudRule
{
    /// <summary>Inclusive. One through five avoids the evening, when plenty of people shop online.</summary>
    private readonly TimeOnly _windowStart = new(1, 0);

    /// <summary>Exclusive, so 05:00 belongs to the ordinary day.</summary>
    private readonly TimeOnly _windowEnd = new(5, 0);

    /// <inheritdoc />
    public RuleId Id { get; } = RuleId.From("UnusualHour");

    /// <inheritdoc />
    public RuleOutcome Evaluate(TransactionEvent transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var localTime = transaction.LocalTimeOfDay;

        // Inclusive at the start, exclusive at the end. 05:00 exactly is not in the window, so the
        // boundary belongs to the ordinary part of the day rather than being ambiguous.
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
