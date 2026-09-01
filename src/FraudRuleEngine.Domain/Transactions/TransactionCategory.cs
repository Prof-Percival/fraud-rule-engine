namespace FraudRuleEngine.Domain.Transactions;

/// <summary>
/// The category an upstream service has already assigned to a transaction.
/// </summary>
/// <remarks>
/// This mirrors a contract owned elsewhere, so a category this service has never heard of has to be
/// survivable rather than fatal. Unrecognised input maps to <see cref="Unknown"/> and rules treat it as
/// carrying no signal.
/// </remarks>
public enum TransactionCategory
{
    /// <summary>Absent or unrecognised. Carries no signal.</summary>
    Unknown = 0,

    Groceries,
    Dining,
    Transport,
    Travel,
    Utilities,
    Retail,
    Healthcare,
    Subscriptions,
    CashWithdrawal,
    Transfer,
    Gambling,
    Cryptocurrency,
    InternationalTransfer,
}
