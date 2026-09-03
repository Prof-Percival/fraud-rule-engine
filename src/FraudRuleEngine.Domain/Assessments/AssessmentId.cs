namespace FraudRuleEngine.Domain.Assessments;

/// <summary>
/// Identifies a stored assessment.
/// </summary>
/// <remarks>
/// Generated here rather than by the database, so the assessment is complete before anything is
/// written and the identifier can be returned or logged without a round trip.
///
/// <para>
/// A version 7 UUID rather than version 4. Both are unique; version 7 puts a millisecond timestamp in
/// its leading bits, so values cluster by creation time. That matters for a primary key: random values
/// scatter inserts across the whole index and fragment it, while time clustered ones keep appending
/// near the same page.
/// </para>
///
/// <para>
/// Two limits worth knowing before relying on the ordering. Within a single millisecond the remaining
/// bits are random, so two identifiers created in the same millisecond have no defined order. And
/// .NET's <see cref="Guid"/> comparison does not compare bytes in canonical order, so sorting these in
/// memory by <see cref="Guid"/> will not reproduce creation order. PostgreSQL compares <c>uuid</c> by
/// its bytes, which is where the clustering benefit actually lands.
/// </para>
/// </remarks>
public readonly record struct AssessmentId
{
    private AssessmentId(Guid value) => Value = value;

    public Guid Value { get; }

    public bool IsInitialised => Value != Guid.Empty;

    public static AssessmentId New() => new(Guid.CreateVersion7());

    /// <summary>Rebuilds an identifier read back from storage.</summary>
    /// <exception cref="ArgumentException">The value is empty.</exception>
    public static AssessmentId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An assessment identifier must not be empty.", nameof(value));
        }

        return new AssessmentId(value);
    }

    public override string ToString() => Value.ToString();
}
