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
    /// <summary>
    /// The amount at or above which a transaction is considered high value, per currency.
    /// </summary>
    private readonly FrozenDictionary<Currency, Money> _thresholds = new Dictionary<Currency, Money>
    {
        [Currency.Zar] = new Money(25_000m, Currency.Zar),
        [Currency.Usd] = new Money(1_500m, Currency.Usd),
        [Currency.Eur] = new Money(1_400m, Currency.Eur),
        [Currency.Gbp] = new Money(1_200m, Currency.Gbp),
    }.ToFrozenDictionary();

    /// <inheritdoc />
    public RuleId Id { get; } = RuleId.From("HighValueTransaction");

    public RuleOutcome Evaluate(TransactionEvent transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

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
