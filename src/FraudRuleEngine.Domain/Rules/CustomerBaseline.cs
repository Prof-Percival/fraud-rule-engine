using System.Collections.Frozen;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// A summary of a customer's longer term behaviour, for judging whether a transaction is normal for them.
/// </summary>
/// <remarks>
/// Separate from <see cref="CustomerHistory"/> because the two answer different questions over different
/// periods, and because this is an aggregate rather than a list of rows. Loading ninety days of
/// transactions per evaluation to average them would make the cost grow with how active the customer is.
///
/// <para>
/// The average is denominated in one currency. A customer transacting in rand and dollars has no single
/// meaningful average, so a rule comparing against it must check the currency and decline when it differs.
/// </para>
/// </remarks>
public sealed record CustomerBaseline
{
    private readonly FrozenSet<MerchantId> _knownMerchants;

    private CustomerBaseline(
        TimeSpan period,
        int transactionCount,
        Money? averageAmount,
        FrozenSet<MerchantId> knownMerchants)
    {
        Period = period;
        TransactionCount = transactionCount;
        AverageAmount = averageAmount;
        _knownMerchants = knownMerchants;
    }

    public TimeSpan Period { get; }

    /// <summary>
    /// Carried so rules can refuse to draw conclusions from too little data. An average over two
    /// transactions is not a baseline.
    /// </summary>
    public int TransactionCount { get; }

    /// <summary>Null when there is nothing to average.</summary>
    public Money? AverageAmount { get; }

    public IReadOnlySet<MerchantId> KnownMerchants => _knownMerchants;

    public bool HasHistory => TransactionCount > 0;

    /// <summary>
    /// A customer nobody has seen before. Rules have to tell this apart from "unlike their normal
    /// behaviour", because having no normal behaviour yet is not evidence of anything.
    /// </summary>
    public static CustomerBaseline None { get; } =
        new(TimeSpan.Zero, 0, averageAmount: null, FrozenSet<MerchantId>.Empty);

    public static CustomerBaseline Over(
        TimeSpan period,
        int transactionCount,
        Money? averageAmount,
        IEnumerable<MerchantId> knownMerchants)
    {
        ArgumentNullException.ThrowIfNull(knownMerchants);
        ArgumentOutOfRangeException.ThrowIfNegative(transactionCount);

        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(period),
                period,
                "A baseline must cover a positive period.");
        }

        return new CustomerBaseline(
            period,
            transactionCount,
            averageAmount,
            knownMerchants.ToFrozenSet());
    }

    public bool HasUsed(MerchantId merchant) => _knownMerchants.Contains(merchant);
}
