using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.TestSupport;

/// <summary>
/// Builds a <see cref="FraudEvaluationContext"/> for a test.
/// </summary>
/// <remarks>
/// Most rules care about the transaction and nothing else, so <see cref="For"/> keeps those tests
/// reading exactly as they did before the context existed. The history dependent rules use
/// <see cref="WithHistory"/>.
/// </remarks>
internal static class ContextBuilder
{
    /// <summary>The default window tests get when they do not care about one.</summary>
    internal static readonly TimeSpan DefaultLookback = TimeSpan.FromHours(24);

    /// <summary>A context for a transaction whose customer has no prior activity.</summary>
    internal static FraudEvaluationContext For(TransactionEvent transaction) =>
        FraudEvaluationContext.WithoutHistory(transaction, DefaultLookback);

    /// <summary>A context built from a builder, for a customer with no prior activity.</summary>
    internal static FraudEvaluationContext For(TransactionEventBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return For(builder.Build());
    }

    /// <summary>A context for a transaction preceded by the given history.</summary>
    internal static FraudEvaluationContext WithHistory(
        TransactionEvent transaction,
        IEnumerable<TransactionEvent> history,
        TimeSpan? lookback = null) =>
        new(transaction, new CustomerHistory(lookback ?? DefaultLookback, history));
}
