using FraudRuleEngine.Domain.Scoring;

namespace FraudRuleEngine.Domain.Tests.Scoring;

public sealed class RiskScoreTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void Accepts_a_value_on_the_scale(int value)
    {
        new RiskScore(value).Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void Rejects_a_value_off_the_scale(int value)
    {
        var act = () => { _ = new RiskScore(value); };

        act.ShouldThrow<ArgumentOutOfRangeException>()
            .ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData(-40, 0)]
    [InlineData(0, 0)]
    [InlineData(85, 85)]
    [InlineData(180, 100)]
    public void Clamps_a_total_rather_than_rejecting_it(int total, int expected)
    {
        // The constructor is strict because an out of range score means a bug. FromTotal is lenient
        // because a running total legitimately overshoots when several rules fire.
        RiskScore.FromTotal(total).Value.ShouldBe(expected);
    }

    [Fact]
    public void Compares_and_sorts_by_value()
    {
        var low = new RiskScore(10);
        var high = new RiskScore(90);
        var alsoLow = new RiskScore(10);

        (high > low).ShouldBeTrue();
        (low < high).ShouldBeTrue();
        (low >= alsoLow).ShouldBeTrue();
        (low <= alsoLow).ShouldBeTrue();
        (low > alsoLow).ShouldBeFalse();

        RiskScore[] scores = [high, low, new RiskScore(50)];
        Array.Sort(scores);
        scores.Select(score => score.Value).ShouldBe([10, 50, 90]);
    }

    [Fact]
    public void Compares_by_value_for_equality()
    {
        new RiskScore(42).ShouldBe(new RiskScore(42));
        new RiskScore(42).ShouldNotBe(new RiskScore(43));
    }

    [Fact]
    public void A_defaulted_score_is_zero_and_usable()
    {
        // Unlike the other value types here, the default is meaningful: no signal is genuinely a score
        // of zero, so there is nothing to guard against.
        default(RiskScore).Value.ShouldBe(0);
        default(RiskScore).ShouldBe(RiskScore.Zero);
    }

    [Fact]
    public void CompareTo_object_orders_null_first_and_rejects_other_types()
    {
        var score = new RiskScore(50);

        score.CompareTo(null).ShouldBe(1);
        score.CompareTo((object)new RiskScore(10)).ShouldBeGreaterThan(0);
        Should.Throw<ArgumentException>(() => score.CompareTo("not a score"));
    }

    [Fact]
    public void Formats_as_a_plain_number()
    {
        new RiskScore(75).ToString().ShouldBe("75");
    }
}
