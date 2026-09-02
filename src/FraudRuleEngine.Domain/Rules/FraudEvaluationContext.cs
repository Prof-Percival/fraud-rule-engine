using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Everything a rule is allowed to look at: the transaction being judged, plus the customer context
/// loaded for it.
/// </summary>
/// <remarks>
/// Rules perform no IO, so anything beyond the transaction has to be fetched before evaluation starts.
/// This is the container for that, assembled once per transaction and handed to every rule.
///
/// <para>
/// The alternative was letting a rule take a repository and query for itself. That was rejected: eight
/// rules would mean eight round trips per transaction, several fetching overlapping data, and every
/// rule test would need a mocked repository. See docs/adr/0003-rules-as-pure-functions.md.
/// </para>
///
/// <para>
/// It follows that this type over fetches, since it is built without knowing which rules will fire. A
/// transaction caught by the amount threshold still pays for the history load. That is accepted while
/// the enrichment is one indexed query over a bounded window, and the ADR records what would be done
/// about it if it stopped being.
/// </para>
/// </remarks>
public sealed record FraudEvaluationContext
{
    /// <summary>Creates a context for evaluating one transaction.</summary>
    /// <exception cref="ArgumentException">
    /// The history contains the transaction under evaluation, or a transaction belonging to a different
    /// customer.
    /// </exception>
    public FraudEvaluationContext(TransactionEvent transaction, CustomerHistory history)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(history);

        EnsureHistoryBelongsToTheCustomer(transaction, history);
        EnsureHistoryExcludesTheTransaction(transaction, history);

        Transaction = transaction;
        History = history;
    }

    /// <summary>The transaction being judged.</summary>
    public TransactionEvent Transaction { get; }

    /// <summary>What is known about the customer's recent behaviour.</summary>
    public CustomerHistory History { get; }

    /// <summary>
    /// A context for a transaction with no customer history, covering the given window.
    /// </summary>
    public static FraudEvaluationContext WithoutHistory(TransactionEvent transaction, TimeSpan lookback)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return new FraudEvaluationContext(transaction, CustomerHistory.Empty(lookback));
    }

    private static void EnsureHistoryBelongsToTheCustomer(
        TransactionEvent transaction,
        CustomerHistory history)
    {
        // An enrichment bug that mixed another customer's transactions in would produce velocity and
        // escalation results that are wrong but entirely plausible looking, so it would not be noticed.
        // Cheap to check, and the check is the only thing that would ever catch it.
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

    private static void EnsureHistoryExcludesTheTransaction(
        TransactionEvent transaction,
        CustomerHistory history)
    {
        // If the transaction being judged also appeared in its own history, every count would be one
        // too high and the velocity rule would drift towards firing on transactions that are fine.
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
