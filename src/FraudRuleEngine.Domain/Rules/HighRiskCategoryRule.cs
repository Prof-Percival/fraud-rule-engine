using System.Collections.Frozen;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Flags a transaction whose category sits on the elevated risk watchlist.
/// </summary>
/// <remarks>
/// These categories are where funds go once an account is compromised: the money moves fast and is hard
/// to claw back. Weak signal on its own, since plenty of customers gamble and buy crypto legitimately,
/// hence <see cref="RuleSeverity.Low"/> and its value in combination with other rules.
/// </remarks>
public sealed class HighRiskCategoryRule : IFraudRule
{
    private readonly FrozenSet<TransactionCategory> _highRiskCategories;

    public HighRiskCategoryRule(IReadOnlySet<TransactionCategory> highRiskCategories)
    {
        ArgumentNullException.ThrowIfNull(highRiskCategories);

        if (highRiskCategories.Count == 0)
        {
            throw new ArgumentException(
                "At least one category is required, or the rule can never fire.",
                nameof(highRiskCategories));
        }

        _highRiskCategories = highRiskCategories.ToFrozenSet();
    }

    public RuleId Id { get; } = RuleId.From("HighRiskCategory");

    public RuleOutcome Evaluate(FraudEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var transaction = context.Transaction;

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
