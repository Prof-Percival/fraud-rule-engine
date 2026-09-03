using FraudRuleEngine.Application.Abstractions;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Transactions;
using FraudRuleEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FraudRuleEngine.Infrastructure.Enrichment;

/// <summary>
/// Loads the customer context for one transaction in two queries.
/// </summary>
/// <remarks>
/// Two, not one, because the two halves are different shapes. The recent window needs individual rows
/// for velocity and impossible travel. The baseline is a grouped aggregate over months, and loading
/// those rows to average them in memory would make the cost scale with how active the customer is.
/// </remarks>
internal sealed class CustomerContextSource : ICustomerContextSource
{
    /// <summary>
    /// How much recent history to load.
    /// </summary>
    /// <remarks>
    /// Must be at least the widest window any enabled rule asks for, currently twelve hours for
    /// impossible travel. <see cref="CustomerHistory"/> throws rather than under reporting if a rule
    /// asks for more, so getting this wrong fails loudly instead of quietly weakening detection.
    /// </remarks>
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromHours(24);

    /// <summary>The period the baseline aggregate covers.</summary>
    private static readonly TimeSpan BaselinePeriod = TimeSpan.FromDays(90);

    private readonly FraudEngineDbContext _dbContext;

    public CustomerContextSource(FraudEngineDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<FraudEvaluationContext> LoadAsync(
        TransactionEvent transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var customerId = transaction.CustomerId.Value;
        var reference = transaction.OccurredAtUtc;

        // Bounded on both sides. Without the upper bound a replayed event would see transactions that
        // happened after it, and velocity would depend on when the evaluation ran rather than on what
        // the customer did.
        var recent = await _dbContext.Transactions
            .AsNoTracking()
            .Where(stored => stored.CustomerId == customerId
                && stored.EventId != transaction.EventId.Value
                && stored.OccurredAtUtc >= reference - HistoryWindow
                && stored.OccurredAtUtc <= reference)
            .OrderByDescending(stored => stored.OccurredAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var history = new CustomerHistory(
            HistoryWindow,
            recent.Select(StoredRecordMapper.ToDomain));

        var baseline = await LoadBaselineAsync(transaction, reference, cancellationToken)
            .ConfigureAwait(false);

        return new FraudEvaluationContext(transaction, history, baseline);
    }

    private async Task<CustomerBaseline> LoadBaselineAsync(
        TransactionEvent transaction,
        DateTimeOffset reference,
        CancellationToken cancellationToken)
    {
        var customerId = transaction.CustomerId.Value;
        var currency = transaction.Amount.Currency.ToString();
        var earliest = reference - BaselinePeriod;

        var inPeriod = _dbContext.Transactions
            .AsNoTracking()
            .Where(stored => stored.CustomerId == customerId
                && stored.EventId != transaction.EventId.Value
                && stored.OccurredAtUtc >= earliest
                && stored.OccurredAtUtc <= reference);

        // Averaged only over transactions in the same currency as the one being judged. A customer
        // spending in rand and dollars has no single meaningful average, and the escalation rule would
        // refuse a mismatched one anyway.
        var summary = await inPeriod
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                AverageInCurrency = group
                    .Where(stored => stored.Currency == currency)
                    .Average(stored => (decimal?)stored.Amount),
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (summary is null || summary.Count == 0)
        {
            return CustomerBaseline.None;
        }

        var merchants = await inPeriod
            .Select(stored => stored.MerchantId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Rounded to the scale Money accepts. An average over arbitrary amounts routinely produces more
        // decimal places than a monetary value is allowed to carry.
        Money? average = summary.AverageInCurrency is { } value
            ? new Money(
                Math.Round(value, Money.MaximumScale, MidpointRounding.ToEven),
                transaction.Amount.Currency)
            : null;

        return CustomerBaseline.Over(
            BaselinePeriod,
            summary.Count,
            average,
            merchants.Select(MerchantId.From));
    }
}
