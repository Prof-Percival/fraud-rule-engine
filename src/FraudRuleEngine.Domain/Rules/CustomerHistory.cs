using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// A customer's recent transactions, loaded once before evaluation because rules cannot query.
/// </summary>
/// <remarks>
/// <see cref="Lookback"/> is carried alongside the transactions so the payload describes itself. Without
/// it an empty list means either the customer did nothing or nothing was loaded, and a rule asking about
/// the last hour cannot tell whether an hour was fetched.
/// </remarks>
public sealed record CustomerHistory
{
    private readonly TransactionEvent[] _recentTransactions;

    public CustomerHistory(TimeSpan lookback, IEnumerable<TransactionEvent> recentTransactions)
    {
        ArgumentNullException.ThrowIfNull(recentTransactions);

        if (lookback <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lookback),
                lookback,
                "A history window must cover a positive period.");
        }

        Lookback = lookback;

        // Sorted here rather than in each rule, so two evaluations of one transaction cannot differ
        // because the database returned rows in a different order.
        _recentTransactions = [.. recentTransactions.OrderByDescending(transaction => transaction.OccurredAtUtc)];
    }

    public TimeSpan Lookback { get; }

    /// <summary>Most recent first. Never includes the transaction under evaluation.</summary>
    public IReadOnlyList<TransactionEvent> RecentTransactions => _recentTransactions;

    public bool IsEmpty => _recentTransactions.Length == 0;

    /// <summary>
    /// A window in which the customer did nothing. A real state, since a customer's first ever
    /// transaction has no history.
    /// </summary>
    public static CustomerHistory Empty(TimeSpan lookback) => new(lookback, []);

    /// <summary>
    /// The transactions within <paramref name="window"/> of <paramref name="reference"/>. Rules ask for
    /// their own window, because velocity cares about minutes while other rules care about hours.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="window"/> is wider than <see cref="Lookback"/>.
    /// </exception>
    public IReadOnlyList<TransactionEvent> Within(TimeSpan window, DateTimeOffset reference)
    {
        // Returning whatever happened to be loaded would let a rule silently under report: asking for an
        // hour when thirty minutes was fetched would show half the traffic and clear transactions that
        // should have been flagged, with no error and no alert.
        if (window > Lookback)
        {
            throw new ArgumentOutOfRangeException(
                nameof(window),
                window,
                $"History covers {Lookback} but {window} was requested. Enrichment must load at "
                    + "least the widest window any enabled rule asks for.");
        }

        var earliest = reference - window;

        return [.. _recentTransactions.Where(transaction =>
            transaction.OccurredAtUtc >= earliest && transaction.OccurredAtUtc <= reference)];
    }
}
