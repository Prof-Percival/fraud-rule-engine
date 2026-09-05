using System.Globalization;

namespace FraudRuleEngine.IntegrationTests;

/// <summary>
/// Builds request payloads. Every field is set, so a test only states what it is actually about.
/// </summary>
internal static class ATransaction
{
    /// <summary>
    /// A customer nobody else is using. The collection shares one database, so a test that counts rows
    /// needs a customer of its own or a neighbour's data changes the answer.
    /// </summary>
    public static string NewCustomerId() => $"CUST-{Guid.NewGuid():N}"[..20];

    public static Dictionary<string, object?> Valid(
        string? eventId = null,
        string? customerId = null,
        decimal amount = 250.00m,
        string currency = "ZAR",
        string category = "Groceries",
        string channel = "ChipAndPin",
        string merchantId = "MERCH-5001",
        string merchantName = "Test Supermarket",
        string countryCode = "ZA",
        DateTimeOffset? occurredAt = null)
    {
        var id = eventId ?? $"it-{Guid.NewGuid():N}";

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["eventId"] = id,
            ["transactionId"] = $"TXN-{id}",
            ["customerId"] = customerId ?? $"CUST-{Guid.NewGuid():N}"[..20],
            ["accountId"] = "ACC-0001",
            ["amount"] = amount,
            ["currency"] = currency,
            ["category"] = category,
            ["channel"] = channel,
            ["merchantId"] = merchantId,
            ["merchantName"] = merchantName,
            ["countryCode"] = countryCode,
            ["occurredAt"] = (occurredAt ?? new DateTimeOffset(2026, 8, 20, 14, 30, 0, TimeSpan.FromHours(2)))
                .ToString("o", CultureInfo.InvariantCulture),
        };
    }
}
