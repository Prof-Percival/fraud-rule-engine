using System.Collections.Frozen;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Flags a transaction with a merchant on the deny list.
/// </summary>
/// <remarks>
/// Not a probabilistic judgment like the other rules. A merchant is on the list because somebody put it
/// there, usually after confirmed fraud, so a hit is a policy violation and reports
/// <see cref="RuleSeverity.High"/> regardless of amount or category.
/// </remarks>
public sealed class DeniedMerchantRule : IFraudRule
{
    private readonly FrozenSet<MerchantId> _deniedMerchants = new[]
    {
        MerchantId.From("MERCH-DENY-0001"),
        MerchantId.From("MERCH-DENY-0002"),
        MerchantId.From("MERCH-DENY-0003"),
    }.ToFrozenSet();

    public RuleId Id { get; } = RuleId.From("DeniedMerchant");

    public RuleOutcome Evaluate(FraudEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var transaction = context.Transaction;

        // Matching is on the identifier and never the name. A name is free text the acquirer controls
        // and it varies in spelling between transactions, so denying by it would be easy to evade and
        // would also catch unrelated merchants sharing one.
        if (!_deniedMerchants.Contains(transaction.Merchant.Id))
        {
            return RuleOutcome.Clear(
                Id,
                $"Merchant {transaction.Merchant.Id} is not on the deny list.");
        }

        return RuleOutcome.Triggered(
            Id,
            RuleSeverity.High,
            $"Merchant {transaction.Merchant.Id} ({transaction.Merchant.Name}) is on the deny list.");
    }
}
