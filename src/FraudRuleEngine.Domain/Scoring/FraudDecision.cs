namespace FraudRuleEngine.Domain.Scoring;

/// <summary>
/// What the engine concludes should happen to a transaction.
/// </summary>
/// <remarks>
/// Three states rather than a fraud flag, because that is how fraud actually works. Most suspicious
/// transactions go to a person, not straight to a block: the cost of a wrong decline is a customer
/// stranded with a card that does not work, which is a call to the contact centre and sometimes a
/// closed account. Modelling this as a boolean would be modelling the wrong problem.
/// </remarks>
public enum FraudDecision
{
    /// <summary>Not suspicious enough to act on. The overwhelming majority of traffic.</summary>
    Approve = 0,

    /// <summary>Suspicious enough that a person should look at it, but not to refuse outright.</summary>
    Review,

    /// <summary>Suspicious enough to refuse.</summary>
    Decline,
}
