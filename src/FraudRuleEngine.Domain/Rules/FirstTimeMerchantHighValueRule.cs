using System.Collections.Frozen;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Flags a large transaction at a merchant the customer has never used before.
/// </summary>
/// <remarks>
/// Neither half is suspicious alone. Customers shop somewhere new constantly and large amounts at
/// familiar merchants are routine; the combination carries signal, because somebody who has taken over
/// an account has no reason to spend small amounts at familiar shops.
///
/// <para>
/// Also the rule most likely to annoy a real customer, since buying a fridge somewhere new is ordinary.
/// Hence <see cref="RuleSeverity.Medium"/> and a threshold set well above everyday spending.
/// </para>
/// </remarks>
public sealed class FirstTimeMerchantHighValueRule : IFraudRule
{
    private readonly FrozenDictionary<Currency, Money> _thresholds;
    private readonly int _minimumBaselineTransactions;

    public FirstTimeMerchantHighValueRule(
        IReadOnlyDictionary<Currency, Money> thresholds,
        int minimumBaselineTransactions)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        if (thresholds.Count == 0)
        {
            throw new ArgumentException(
                "At least one currency threshold is required, or the rule can never fire.",
                nameof(thresholds));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(minimumBaselineTransactions, 1);

        _thresholds = thresholds.ToFrozenDictionary();
        _minimumBaselineTransactions = minimumBaselineTransactions;
    }

    /// <inheritdoc />
    public RuleId Id { get; } = RuleId.From("FirstTimeMerchantHighValue");

    /// <inheritdoc />
    public RuleOutcome Evaluate(FraudEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var transaction = context.Transaction;
        var baseline = context.Baseline;

        // "Every merchant is new to this customer" says how little is known, not anything about the
        // transaction.
        if (baseline.TransactionCount < _minimumBaselineTransactions)
        {
            return RuleOutcome.Clear(
                Id,
                $"Only {baseline.TransactionCount} prior transaction(s) on record, too few to tell a "
                    + $"new merchant from a new customer. At least {_minimumBaselineTransactions} "
                    + "are needed.");
        }

        if (baseline.HasUsed(transaction.Merchant.Id))
        {
            return RuleOutcome.Clear(
                Id,
                $"Customer has used merchant {transaction.Merchant.Id} before.");
        }

        if (!_thresholds.TryGetValue(transaction.Amount.Currency, out var threshold))
        {
            return RuleOutcome.Clear(
                Id,
                $"No first time merchant threshold is configured for {transaction.Amount.Currency}.");
        }

        if (transaction.Amount < threshold)
        {
            return RuleOutcome.Clear(
                Id,
                $"Merchant {transaction.Merchant.Id} is new to this customer, but {transaction.Amount} "
                    + $"is below the {threshold} threshold.");
        }

        return RuleOutcome.Triggered(
            Id,
            RuleSeverity.Medium,
            $"{transaction.Amount} at {transaction.Merchant.Name} ({transaction.Merchant.Id}), a "
                + $"merchant this customer has not used in the last {baseline.Period.TotalDays:F0} "
                + $"days, against a threshold of {threshold}.");
    }
}
