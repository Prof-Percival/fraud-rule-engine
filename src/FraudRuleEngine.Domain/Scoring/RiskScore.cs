using System.Globalization;

namespace FraudRuleEngine.Domain.Scoring;

/// <summary>
/// How suspicious a transaction is, on a fixed scale from zero to one hundred.
/// </summary>
/// <remarks>
/// A bounded scale rather than an open ended total, so a score means the same thing in six months as
/// it does today. An unbounded sum would drift upward every time a rule was added, quietly moving
/// every threshold with it and making a stored score from last year incomparable with a fresh one.
/// </remarks>
public readonly record struct RiskScore : IComparable<RiskScore>, IComparable
{
    public const int Minimum = 0;

    public const int Maximum = 100;

    /// <exception cref="ArgumentOutOfRangeException">The value is outside zero to one hundred.</exception>
    public RiskScore(int value)
    {
        if (value is < Minimum or > Maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"A risk score must be between {Minimum} and {Maximum}.");
        }

        Value = value;
    }

    public int Value { get; }

    public static RiskScore Zero { get; } = new(Minimum);

    /// <summary>
    /// Clamps to the scale rather than throwing, for use where a total may legitimately overshoot.
    /// </summary>
    public static RiskScore FromTotal(int total) => new(Math.Clamp(total, Minimum, Maximum));

    public static bool operator <(RiskScore left, RiskScore right) => left.Value < right.Value;

    public static bool operator <=(RiskScore left, RiskScore right) => left.Value <= right.Value;

    public static bool operator >(RiskScore left, RiskScore right) => left.Value > right.Value;

    public static bool operator >=(RiskScore left, RiskScore right) => left.Value >= right.Value;

    public int CompareTo(RiskScore other) => Value.CompareTo(other.Value);

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        RiskScore other => CompareTo(other),
        _ => throw new ArgumentException($"Object must be of type {nameof(RiskScore)}.", nameof(obj)),
    };

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
