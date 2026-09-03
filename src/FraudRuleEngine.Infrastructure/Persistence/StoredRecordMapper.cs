using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Infrastructure.Persistence;

/// <summary>
/// Translates between the domain model and the stored rows.
/// </summary>
/// <remarks>
/// The one place that knows both shapes. Reading is the harder direction: the domain constructors
/// validate, so a row that violates an invariant fails here rather than producing a half valid object
/// that misbehaves somewhere further away.
/// </remarks>
internal static class StoredRecordMapper
{
    internal static StoredTransaction ToStored(TransactionEvent transaction, DateTimeOffset ingestedAtUtc) =>
        new()
        {
            EventId = transaction.EventId.Value,
            TransactionId = transaction.TransactionId.Value,
            CustomerId = transaction.CustomerId.Value,
            AccountId = transaction.AccountId.Value,
            Amount = transaction.Amount.Amount,
            Currency = transaction.Amount.Currency.ToString(),
            Category = transaction.Category.ToString(),
            Channel = transaction.Channel.ToString(),
            MerchantId = transaction.Merchant.Id.Value,
            MerchantName = transaction.Merchant.Name,
            CountryCode = transaction.Country.Value,
            OccurredAtUtc = transaction.OccurredAtUtc,
            OffsetMinutes = (int)transaction.OccurredAt.Offset.TotalMinutes,
            IngestedAtUtc = ingestedAtUtc,
        };

    internal static TransactionEvent ToDomain(StoredTransaction stored)
    {
        // The offset is reapplied so the local wall clock is recovered, which the unusual hour rule
        // needs. timestamptz keeps the instant and discards the offset, hence the separate column.
        var occurredAt = stored.OccurredAtUtc.ToOffset(TimeSpan.FromMinutes(stored.OffsetMinutes));

        return new TransactionEvent(
            EventId.From(stored.EventId),
            TransactionId.From(stored.TransactionId),
            CustomerId.From(stored.CustomerId),
            AccountId.From(stored.AccountId),
            new Money(stored.Amount, ParseOrUnknown<Currency>(stored.Currency)),
            ParseOrUnknown<TransactionCategory>(stored.Category),
            ParseOrUnknown<TransactionChannel>(stored.Channel),
            new Merchant(MerchantId.From(stored.MerchantId), stored.MerchantName),
            CountryCode.From(stored.CountryCode),
            occurredAt);
    }

    internal static StoredAssessment ToStored(FraudAssessment assessment)
    {
        var stored = new StoredAssessment
        {
            Id = assessment.Id.Value,
            EventId = assessment.EventId.Value,
            TransactionId = assessment.TransactionId.Value,
            CustomerId = assessment.CustomerId.Value,
            RiskScore = assessment.RiskScore.Value,
            Decision = assessment.Decision.ToString(),
            RuleSetVersion = assessment.RuleSetVersion.Value,
            EvaluatedAtUtc = assessment.EvaluatedAt.ToUniversalTime(),
        };

        for (var ordinal = 0; ordinal < assessment.RuleOutcomes.Count; ordinal++)
        {
            var outcome = assessment.RuleOutcomes[ordinal];

            stored.RuleOutcomes.Add(new StoredRuleOutcome
            {
                Id = default,
                AssessmentId = stored.Id,
                RuleId = outcome.RuleId.Value,
                IsTriggered = outcome.IsTriggered,
                Severity = outcome.Severity.ToString(),
                Reason = outcome.Reason,
                Ordinal = ordinal,
            });
        }

        return stored;
    }

    /// <summary>
    /// Reads an enum stored by name, falling back to the zero value when the name is unrecognised.
    /// </summary>
    /// <remarks>
    /// A row written by a newer build can carry a category or channel this one has never heard of.
    /// Falling back beats throwing: the whole point of having an Unknown member is that unfamiliar input
    /// is survivable, and refusing to read a row would make a rollback unable to serve its own data.
    /// </remarks>
    private static TEnum ParseOrUnknown<TEnum>(string value)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) ? parsed : default;
}
