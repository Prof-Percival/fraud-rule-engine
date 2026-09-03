namespace FraudRuleEngine.Domain.Rules;

/// <summary>
/// Flags a customer making an unusual number of transactions in a short period.
/// </summary>
/// <remarks>
/// The signature of card testing: a rapid sequence to find out whether stolen details are live and what
/// will go through. Any one of those transactions is unremarkable, which is why a rule looking at a
/// single transaction cannot catch it.
/// </remarks>
public sealed class TransactionVelocityRule : IFraudRule
{
    private readonly int _threshold;
    private readonly TimeSpan _window;

    public TransactionVelocityRule(int threshold, TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threshold, 2);

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(window),
                window,
                "The window must be positive.");
        }

        _threshold = threshold;
        _window = window;
    }

    public RuleId Id { get; } = RuleId.From("TransactionVelocity");

    public RuleOutcome Evaluate(FraudEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var transaction = context.Transaction;

        // Measured back from the transaction, not from now. Using the current time would make an event
        // that took hours to arrive score differently from one handled live.
        var precedingInWindow = context.History.Within(_window, transaction.OccurredAtUtc);
        var countInWindow = precedingInWindow.Count + 1;

        if (countInWindow < _threshold)
        {
            return RuleOutcome.Clear(
                Id,
                $"{countInWindow} transaction(s) in the {Describe(_window)} to "
                    + $"{transaction.OccurredAtUtc:u}, below the threshold of {_threshold}.");
        }

        return RuleOutcome.Triggered(
            Id,
            RuleSeverity.Medium,
            $"{countInWindow} transactions in the {Describe(_window)} to "
                + $"{transaction.OccurredAtUtc:u}, at or above the threshold of {_threshold}.");
    }

    private static string Describe(TimeSpan window) =>
        window.TotalMinutes < 60
            ? $"{window.TotalMinutes:F0} minutes"
            : $"{window.TotalHours:F0} hours";
}
