using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace FraudRuleEngine.Application.Abstractions;

/// <summary>
/// Marks the position of the last row a caller received.
/// </summary>
/// <remarks>
/// The identifier is part of the position, not decoration. Assessments can share a timestamp, and a page
/// boundary landing inside such a group is where rows get repeated or skipped.
/// </remarks>
public readonly record struct AssessmentCursor
{
    private AssessmentCursor(DateTimeOffset evaluatedAtUtc, Guid assessmentId)
    {
        EvaluatedAtUtc = evaluatedAtUtc;
        AssessmentId = assessmentId;
    }

    public DateTimeOffset EvaluatedAtUtc { get; }

    public Guid AssessmentId { get; }

    public static AssessmentCursor From(DateTimeOffset evaluatedAtUtc, Guid assessmentId) =>
        new(evaluatedAtUtc, assessmentId);

    /// <summary>
    /// Encodes the cursor for a query string.
    /// </summary>
    /// <remarks>
    /// Ticks rather than a formatted date, because a cursor that rounds lands between rows. Base64url
    /// rather than base64, because the standard alphabet needs escaping in a URL.
    /// </remarks>
    public string Encode()
    {
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{EvaluatedAtUtc.UtcTicks}:{AssessmentId:N}");

        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>
    /// Reads a cursor a caller sent back.
    /// </summary>
    /// <returns>False for anything malformed, so a mangled cursor is a 400 rather than a 500.</returns>
    public static bool TryDecode(string? encoded, out AssessmentCursor cursor)
    {
        cursor = default;

        if (string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded));
            var separator = payload.IndexOf(':', StringComparison.Ordinal);

            if (separator <= 0)
            {
                return false;
            }

            if (!long.TryParse(
                    payload.AsSpan(0, separator),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var ticks)
                || !Guid.TryParseExact(payload.AsSpan(separator + 1), "N", out var id))
            {
                return false;
            }

            if (ticks < 0 || ticks > DateTimeOffset.MaxValue.UtcTicks)
            {
                return false;
            }

            cursor = new AssessmentCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
