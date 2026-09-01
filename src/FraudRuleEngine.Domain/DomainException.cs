namespace FraudRuleEngine.Domain;

/// <summary>
/// Base type for errors that represent a broken domain rule rather than a bug or an
/// infrastructure failure.
/// </summary>
/// <remarks>
/// Earns its keep at the API boundary: a <see cref="DomainException"/> maps to a 4xx problem response,
/// anything else is a 500 and a page for somebody. One mapping rule instead of a growing list of
/// individual exception types.
/// </remarks>
public abstract class DomainException : Exception
{
    protected DomainException()
    {
    }

    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
