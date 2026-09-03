using System.Globalization;

namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Flags a transaction far larger than the customer normally makes.
/// </summary>
/// <remarks>
/// A fixed threshold is either too low for a customer who regularly spends large amounts or too high to
/// catch anything on an account that normally spends very little. Judging against the customer's own
/// average is what catches ordinary traffic of a few hundred rand suddenly paying twenty thousand.
///
/// <para>
/// Deliberately conservative in both the history it demands and the multiple it uses, which pushes it
/// towards missing fraud rather than accusing people. That is the right direction for a rule based on
/// nothing more than a mean.
/// </para>
/// </remarks>
public sealed class AmountEscalationRule : IFraudRule
{
    private readonly decimal _multiple;
    private readonly int _minimumBaselineTransactions;

    public AmountEscalationRule(decimal multiple, int minimumBaselineTransactions)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(multiple, 1m);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumBaselineTransactions, 1);

        _multiple = multiple;
        _minimumBaselineTransactions = minimumBaselineTransactions;
    }

    /// <inheritdoc />
    public RuleId Id { get; } = RuleId.From("AmountEscalation");

    /// <inheritdoc />
    public RuleOutcome Evaluate(FraudEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var transaction = context.Transaction;
        var baseline = context.Baseline;

        if (baseline.TransactionCount < _minimumBaselineTransactions)
        {
            return RuleOutcome.Clear(
                Id,
                $"Only {baseline.TransactionCount} prior transaction(s) on record, fewer than the "
                    + $"{_minimumBaselineTransactions} needed before an average means anything.");
        }

        if (baseline.AverageAmount is not { } average)
        {
            return RuleOutcome.Clear(Id, "No average transaction amount is available for this customer.");
        }

        // A customer transacting in rand and dollars has no single meaningful average. Converting at some
        // ambient rate would produce a number nobody can reconcile.
        if (average.Currency != transaction.Amount.Currency)
        {
            return RuleOutcome.Clear(
                Id,
                $"Baseline average is in {average.Currency} but this transaction is in "
                    + $"{transaction.Amount.Currency}, so the two cannot be compared.");
        }

        // Any multiple of zero is zero, so every transaction would clear the bar. An average of zero means
        // something is wrong upstream, not that the customer is unusual.
        if (average.IsZero)
        {
            return RuleOutcome.Clear(
                Id,
                "Baseline average is zero, which gives nothing to compare against.");
        }

        var trigger = average * _multiple;

        if (transaction.Amount < trigger)
        {
            return RuleOutcome.Clear(
                Id,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{transaction.Amount} is below {_multiple:F0} times the customer's average of "
                        + $"{average}, which is {trigger}."));
        }

        return RuleOutcome.Triggered(
            Id,
            RuleSeverity.Medium,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{transaction.Amount} is at or above {_multiple:F0} times the customer's average of "
                    + $"{average} over {baseline.TransactionCount} transactions, which is {trigger}."));
    }
}
