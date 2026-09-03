using System.Collections.Frozen;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Flags a transaction whose amount is at or above the high value threshold for its currency.
/// </summary>
/// <remarks>
/// The simplest rule in the set and the one every fraud system starts with. It needs nothing but the
/// transaction itself: no customer history, no reference data, no external lookup.
/// </remarks>
public sealed class HighValueTransactionRule : IFraudRule
{
    private readonly FrozenDictionary<Currency, Money> _thresholds;

    public HighValueTransactionRule(IReadOnlyDictionary<Currency, Money> thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        if (thresholds.Count == 0)
        {
            throw new ArgumentException(
                "At least one currency threshold is required, or the rule can never fire.",
                nameof(thresholds));
        }

        _thresholds = thresholds.ToFrozenDictionary();
    }

    /// <inheritdoc />
    public RuleId Id { get; } = RuleId.From("HighValueTransaction");

    /// <inheritdoc />
    /// <remarks>
    /// The reason quotes the actual amount and the threshold it crossed. A flag reaching a human with
    /// no numbers attached forces them to go and find the transaction to judge it, and at that point
    /// the flag has cost more than it saved.
    /// </remarks>
    public RuleOutcome Evaluate(FraudEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var transaction = context.Transaction;

        // A currency with no configured threshold does not fire. The alternative, treating an unknown
        // currency as suspicious, would flag every transaction in it, which is noise rather than
        // detection.
        if (!_thresholds.TryGetValue(transaction.Amount.Currency, out var threshold))
        {
            return RuleOutcome.Clear(
                Id,
                $"No high value threshold is configured for {transaction.Amount.Currency}.");
        }

        // At or above, not above. A threshold of 25,000 means 25,000 itself is high value, otherwise
        // the limit is really 25,000.0001 and nobody reading the config would know that.
        if (transaction.Amount < threshold)
        {
            return RuleOutcome.Clear(
                Id,
                $"Amount {transaction.Amount} is below the high value threshold of {threshold}.");
        }

        return RuleOutcome.Triggered(
            Id,
            RuleSeverity.High,
            $"Amount {transaction.Amount} is at or above the high value threshold of {threshold}.");
    }
}
