using System.Collections.Frozen;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Flags a transaction whose category sits on the elevated risk watchlist.
/// </summary>
public sealed class HighRiskCategoryRule : IFraudRule
{
    private readonly FrozenSet<TransactionCategory> _highRiskCategories = new[]
    {
        TransactionCategory.Gambling,
        TransactionCategory.Cryptocurrency,
        TransactionCategory.InternationalTransfer,
    }.ToFrozenSet();

    /// <inheritdoc />
    public RuleId Id { get; } = RuleId.From("HighRiskCategory");

    /// <inheritdoc />
    public RuleOutcome Evaluate(TransactionEvent transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!_highRiskCategories.Contains(transaction.Category))
        {
            return RuleOutcome.Clear(
                Id,
                $"Category {transaction.Category} is not on the elevated risk watchlist.");
        }

        return RuleOutcome.Triggered(
            Id,
            RuleSeverity.Low,
            $"Category {transaction.Category} is on the elevated risk watchlist.");
    }
}
