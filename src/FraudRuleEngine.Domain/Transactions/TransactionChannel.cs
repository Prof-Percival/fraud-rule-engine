namespace FraudRuleEngine.Domain.Transactions;

/// <summary>
/// How the transaction was initiated.
/// </summary>
/// <remarks>
/// Modelled separately from the category because the channel determines what verification actually
/// happened. Chip and PIN means the card was present and a PIN was entered; card not present means
/// somebody typed a number in. The same amount at the same merchant carries different risk depending
/// which it was.
/// </remarks>
public enum TransactionChannel
{
    /// <summary>Absent or unrecognised. Implies nothing about card presence.</summary>
    Unknown = 0,

    /// <summary>Card present, verified by chip and PIN.</summary>
    ChipAndPin,

    /// <summary>Card present, verified by contactless tap.</summary>
    Contactless,

    /// <summary>Card details entered without the card present.</summary>
    CardNotPresent,

    /// <summary>Withdrawal or deposit at an ATM.</summary>
    Atm,

    /// <summary>Transfer initiated in online or mobile banking.</summary>
    OnlineBanking,

    /// <summary>Standing instruction collected by a third party.</summary>
    DebitOrder,

    /// <summary>Initiated by a person at a branch counter.</summary>
    Branch,
}
