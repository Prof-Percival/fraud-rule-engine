using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Application.Abstractions;

/// <summary>
/// Loads everything the rules need about a customer, in one pass, before evaluation starts.
/// </summary>
/// <remarks>
/// This is the enrichment step that exists because rules do no IO. It returns a complete
/// <see cref="FraudEvaluationContext"/>, so no rule ever has to ask for anything.
///
/// <para>
/// Returning the assembled context rather than its parts is deliberate. The context validates that the
/// history belongs to the right customer and excludes the transaction being judged, so building it here
/// means those checks run against whatever the database actually returned.
/// </para>
///
/// <para>
/// The implementation has to load at least the widest window any enabled rule asks for, currently
/// twelve hours for impossible travel. <see cref="CustomerHistory"/> throws rather than under report if
/// a rule asks for more than was loaded.
/// </para>
/// </remarks>
public interface ICustomerContextSource
{
    Task<FraudEvaluationContext> LoadAsync(
        TransactionEvent transaction,
        CancellationToken cancellationToken);
}
