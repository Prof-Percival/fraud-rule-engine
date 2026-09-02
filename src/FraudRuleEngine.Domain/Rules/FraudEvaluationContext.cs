using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Everything a rule is allowed to look at. Assembled once per transaction, because rules do no IO.
/// See docs/adr/0003-rules-as-pure-functions.md.
/// </summary>
/// <remarks>
/// It follows that this over fetches, being built without knowing which rules will fire. A transaction
/// caught by the amount threshold still pays for the history load.
/// </remarks>
public sealed record FraudEvaluationContext
{
    public FraudEvaluationContext(
        TransactionEvent transaction,
        CustomerHistory history,
        CustomerBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(baseline);

        EnsureHistoryBelongsToTheCustomer(transaction, history);
        EnsureHistoryExcludesTheTransaction(transaction, history);

        Transaction = transaction;
        History = history;
        Baseline = baseline;
    }

    public TransactionEvent Transaction { get; }

    /// <summary>Individual transactions over a short window, for velocity and impossible travel.</summary>
    public CustomerHistory History { get; }

    /// <summary>A longer term aggregate of what is normal for this customer.</summary>
    public CustomerBaseline Baseline { get; }

    public static FraudEvaluationContext WithoutHistory(TransactionEvent transaction, TimeSpan lookback)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return new FraudEvaluationContext(
            transaction,
            CustomerHistory.Empty(lookback),
            CustomerBaseline.None);
    }

    // An enrichment query with a wrong or missing customer predicate would produce velocity and
    // escalation results that are wrong and entirely plausible, so nothing else would catch it.
    private static void EnsureHistoryBelongsToTheCustomer(
        TransactionEvent transaction,
        CustomerHistory history)
    {
        foreach (var previous in history.RecentTransactions)
        {
            if (previous.CustomerId != transaction.CustomerId)
            {
                throw new ArgumentException(
                    $"History for customer {transaction.CustomerId} contains a transaction belonging "
                        + $"to {previous.CustomerId}.",
                    nameof(history));
            }
        }
    }

    // A transaction appearing in its own history would make every count one too high, drifting the
    // velocity rule towards firing on ordinary transactions.
    private static void EnsureHistoryExcludesTheTransaction(
        TransactionEvent transaction,
        CustomerHistory history)
    {
        foreach (var previous in history.RecentTransactions)
        {
            if (previous.EventId == transaction.EventId)
            {
                throw new ArgumentException(
                    "History must not contain the transaction being evaluated.",
                    nameof(history));
            }
        }
    }
}
