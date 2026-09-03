using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Application.Tests.TestSupport;

/// <summary>
/// A valid transaction for tests that do not care about its details.
/// </summary>
/// <remarks>
/// Deliberately not shared with the domain test project's builder. Application tests care about
/// orchestration, not about which fields a rule reads, so a single factory is enough and copying it
/// here is cheaper than making the two projects depend on each other's test code.
/// </remarks>
internal static class ATransaction
{
    internal static TransactionEvent Valid(
        string eventId = "evt-0001",
        string customerId = "CUST-0001",
        decimal amount = 250m) =>
        new(
            EventId.From(eventId),
            TransactionId.From("TXN-0001"),
            CustomerId.From(customerId),
            AccountId.From("ACC-0001"),
            new Money(amount, Currency.Zar),
            TransactionCategory.Groceries,
            TransactionChannel.ChipAndPin,
            new Merchant(MerchantId.From("MERCH-0001"), "Checkers Hyper Constantia"),
            CountryCode.From("ZA"),
            new DateTimeOffset(2026, 9, 2, 14, 30, 0, TimeSpan.FromHours(2)));
}
