using FraudRuleEngine.Application.Abstractions;

namespace FraudRuleEngine.Application.Tests.Abstractions;

public sealed class AssessmentCursorTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 3, 14, 30, 15, 123, TimeSpan.Zero);

    [Fact]
    public void Round_trips_exactly()
    {
        var id = Guid.CreateVersion7();
        var original = AssessmentCursor.From(At, id);

        AssessmentCursor.TryDecode(original.Encode(), out var decoded).ShouldBeTrue();

        decoded.EvaluatedAtUtc.ShouldBe(At);
        decoded.AssessmentId.ShouldBe(id);
    }

    [Fact]
    public void Keeps_sub_millisecond_precision()
    {
        var precise = new DateTimeOffset(2026, 9, 3, 14, 30, 15, TimeSpan.Zero).AddTicks(1234567);

        AssessmentCursor.TryDecode(AssessmentCursor.From(precise, Guid.CreateVersion7()).Encode(), out var decoded)
            .ShouldBeTrue();

        decoded.EvaluatedAtUtc.UtcTicks.ShouldBe(precise.UtcTicks);
    }

    [Fact]
    public void Encodes_safely_for_a_query_string()
    {
        var encoded = AssessmentCursor.From(At, Guid.CreateVersion7()).Encode();

        encoded.ShouldNotContain("+");
        encoded.ShouldNotContain("/");
        encoded.ShouldNotContain("=");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64!!")]
    [InlineData("bm90LWEtY3Vyc29y")]
    [InlineData("MTIzNDU2Nzg5")]
    public void Rejects_anything_malformed(string? encoded)
    {
        AssessmentCursor.TryDecode(encoded, out _).ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_cursor_whose_ticks_are_out_of_range()
    {
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"99999999999999999999:{Guid.CreateVersion7():N}"))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        AssessmentCursor.TryDecode(payload, out _).ShouldBeFalse();
    }

    [Fact]
    public void Compares_by_value()
    {
        var id = Guid.CreateVersion7();

        AssessmentCursor.From(At, id).ShouldBe(AssessmentCursor.From(At, id));
        AssessmentCursor.From(At, id).ShouldNotBe(AssessmentCursor.From(At, Guid.CreateVersion7()));
    }
}
