using FraudRuleEngine.Domain.Transactions;

// The web SDK's implicit usings bring in Microsoft.Extensions.Logging, which also has an EventId.
using DomainEventId = FraudRuleEngine.Domain.Transactions.EventId;

namespace FraudRuleEngine.Api.Contracts;

/// <summary>
/// Turns a request into a domain transaction, or into a list of what is wrong with it.
/// </summary>
/// <remarks>
/// Collects every problem rather than throwing on the first. A caller fixing a malformed payload one
/// field per round trip is a poor experience, and for a batch it means several attempts to ingest one
/// file.
///
/// <para>
/// This duplicates checks the domain constructors also make, which is deliberate rather than redundant.
/// Here the job is to produce a useful message naming the field; there the job is to guarantee no
/// invalid transaction exists at all. The domain cannot be relaxed on the strength of this running
/// first, because the domain is reachable from elsewhere.
/// </para>
/// </remarks>
public static class TransactionRequestMapper
{
    public static bool TryMap(
        EvaluateTransactionRequest request,
        out TransactionEvent transaction,
        out IReadOnlyDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problems = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var eventId = Identifier(problems, nameof(request.EventId), request.EventId, DomainEventId.MaximumLength);
        var transactionId = Identifier(problems, nameof(request.TransactionId), request.TransactionId, TransactionId.MaximumLength);
        var customerId = Identifier(problems, nameof(request.CustomerId), request.CustomerId, CustomerId.MaximumLength);
        var accountId = Identifier(problems, nameof(request.AccountId), request.AccountId, AccountId.MaximumLength);
        var merchantId = Identifier(problems, nameof(request.MerchantId), request.MerchantId, MerchantId.MaximumLength);

        var currency = RequiredEnum<Currency>(problems, nameof(request.Currency), request.Currency);
        var category = OptionalEnum<TransactionCategory>(problems, nameof(request.Category), request.Category);
        var channel = OptionalEnum<TransactionChannel>(problems, nameof(request.Channel), request.Channel);

        var amount = Amount(problems, request.Amount);
        var merchantName = MerchantName(problems, request.MerchantName);
        var country = Country(problems, request.CountryCode);
        var occurredAt = OccurredAt(problems, request.OccurredAt);

        if (problems.Count > 0)
        {
            transaction = null!;
            errors = problems.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal);
            return false;
        }

        transaction = new TransactionEvent(
            DomainEventId.From(eventId!),
            TransactionId.From(transactionId!),
            CustomerId.From(customerId!),
            AccountId.From(accountId!),
            new Money(amount!.Value, currency!.Value),
            category,
            channel,
            new Merchant(MerchantId.From(merchantId!), merchantName!),
            CountryCode.From(country!),
            occurredAt!.Value);

        errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        return true;
    }

    private static string? Identifier(
        Dictionary<string, List<string>> problems,
        string field,
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(problems, field, "Required.");
            return null;
        }

        if (value.Length > maximumLength)
        {
            Add(problems, field, $"Must be at most {maximumLength} characters.");
            return null;
        }

        return value;
    }

    private static decimal? Amount(Dictionary<string, List<string>> problems, decimal? value)
    {
        if (value is null)
        {
            Add(problems, nameof(EvaluateTransactionRequest.Amount), "Required.");
            return null;
        }

        if (value <= 0m)
        {
            Add(problems, nameof(EvaluateTransactionRequest.Amount), "Must be greater than zero.");
            return null;
        }

        if (value.Value.Scale > Money.MaximumScale)
        {
            Add(
                problems,
                nameof(EvaluateTransactionRequest.Amount),
                $"Must carry at most {Money.MaximumScale} decimal places.");
            return null;
        }

        return value;
    }

    private static string? MerchantName(Dictionary<string, List<string>> problems, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(problems, nameof(EvaluateTransactionRequest.MerchantName), "Required.");
            return null;
        }

        if (value.Length > Merchant.MaximumNameLength)
        {
            Add(
                problems,
                nameof(EvaluateTransactionRequest.MerchantName),
                $"Must be at most {Merchant.MaximumNameLength} characters.");
            return null;
        }

        return value;
    }

    private static string? Country(Dictionary<string, List<string>> problems, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(problems, nameof(EvaluateTransactionRequest.CountryCode), "Required.");
            return null;
        }

        if (value.Length != CountryCode.Length || !value.All(char.IsLetter))
        {
            Add(
                problems,
                nameof(EvaluateTransactionRequest.CountryCode),
                "Must be a two letter ISO 3166-1 alpha-2 code.");
            return null;
        }

        return value;
    }

    private static DateTimeOffset? OccurredAt(Dictionary<string, List<string>> problems, DateTimeOffset? value)
    {
        if (value is null || value == default(DateTimeOffset))
        {
            Add(problems, nameof(EvaluateTransactionRequest.OccurredAt), "Required.");
            return null;
        }

        return value;
    }

    /// <summary>
    /// Currency must be recognised, because there is no sensible default for money.
    /// </summary>
    private static TEnum? RequiredEnum<TEnum>(
        Dictionary<string, List<string>> problems,
        string field,
        string? value)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(problems, field, "Required.");
            return null;
        }

        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            || Convert.ToInt32(parsed, System.Globalization.CultureInfo.InvariantCulture) == 0)
        {
            Add(problems, field, $"Must be one of: {SupportedNames<TEnum>()}.");
            return null;
        }

        return parsed;
    }

    /// <summary>
    /// Category and channel fall back to Unknown rather than being rejected.
    /// </summary>
    /// <remarks>
    /// The upstream service owns this vocabulary, so it can add a value at any time. Refusing traffic
    /// because of a word this build has not seen would turn an upstream release into an outage, and the
    /// rules already treat Unknown as carrying no signal.
    /// </remarks>
    private static TEnum OptionalEnum<TEnum>(
        Dictionary<string, List<string>> problems,
        string field,
        string? value)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(problems, field, "Required.");
            return default;
        }

        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : default;
    }

    private static string SupportedNames<TEnum>()
        where TEnum : struct, Enum =>
        string.Join(
            ", ",
            Enum.GetNames<TEnum>().Where(name => !string.Equals(name, "None", StringComparison.Ordinal)));

    private static void Add(Dictionary<string, List<string>> problems, string field, string message)
    {
        if (!problems.TryGetValue(field, out var messages))
        {
            messages = [];
            problems[field] = messages;
        }

        messages.Add(message);
    }
}
