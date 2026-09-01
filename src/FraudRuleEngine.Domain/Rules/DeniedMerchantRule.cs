using System.Collections.Frozen;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Flags a transaction with a merchant on the deny list.
/// </summary>
public sealed class DeniedMerchantRule : IFraudRule
{
    private readonly FrozenSet<MerchantId> _deniedMerchants = new[]
    {
        MerchantId.From("MERCH-DENY-0001"),
        MerchantId.From("MERCH-DENY-0002"),
        MerchantId.From("MERCH-DENY-0003"),
    }.ToFrozenSet();

    /// <inheritdoc />
    public RuleId Id { get; } = RuleId.From("DeniedMerchant");

    /// <inheritdoc />
    public RuleOutcome Evaluate(TransactionEvent transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!_deniedMerchants.Contains(transaction.Merchant.Id))
        {
            return RuleOutcome.Clear(
                Id,
                $"Merchant {transaction.Merchant.Id} is not on the deny list.");
        }

        // The name goes in the reason even though matching ignores it, because the person picking this
        // up wants to know which shop it was without going and looking the identifier up.
        return RuleOutcome.Triggered(
            Id,
            RuleSeverity.High,
            $"Merchant {transaction.Merchant.Id} ({transaction.Merchant.Name}) is on the deny list.");
    }
}
