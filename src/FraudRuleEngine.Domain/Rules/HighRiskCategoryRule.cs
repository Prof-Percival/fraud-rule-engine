using System.Collections.Frozen;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Flags a transaction whose category sits on the elevated risk watchlist.
/// </summary>
public sealed class HighRiskCategoryRule
{
    private readonly FrozenSet<TransactionCategory> _highRiskCategories = new[]
    {
        TransactionCategory.Gambling,
        TransactionCategory.Cryptocurrency,
        TransactionCategory.InternationalTransfer,
    }.ToFrozenSet();

    public string? Evaluate(TransactionEvent transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!_highRiskCategories.Contains(transaction.Category))
        {
            return null;
        }

        return $"Category {transaction.Category} is on the elevated risk watchlist.";
    }
}
