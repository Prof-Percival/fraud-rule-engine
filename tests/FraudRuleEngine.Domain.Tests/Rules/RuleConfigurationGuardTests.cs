using System.Collections.Frozen;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Rules;

/// <summary>
/// The rules refuse configuration that would leave them unable to fire or scoring nonsense. Options
/// validation catches most of this at startup; these guards make it impossible however a rule is built.
/// </summary>
public sealed class RuleConfigurationGuardTests
{
    [Fact]
    public void High_value_needs_at_least_one_threshold() =>
        Should.Throw<ArgumentException>(
            () => new HighValueTransactionRule(new Dictionary<Currency, Money>()));

    [Fact]
    public void High_risk_category_needs_at_least_one_category() =>
        Should.Throw<ArgumentException>(
            () => new HighRiskCategoryRule(FrozenSet<TransactionCategory>.Empty));

    [Fact]
    public void Unusual_hour_refuses_a_window_that_does_not_move_forward() =>
        Should.Throw<ArgumentException>(
            () => new UnusualHourRule(new TimeOnly(5, 0), new TimeOnly(1, 0)));

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void Amount_escalation_refuses_a_multiple_that_does_not_exceed_one(int multiple) =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => new AmountEscalationRule(multiple, minimumBaselineTransactions: 10));

    [Fact]
    public void Velocity_refuses_a_threshold_below_two() =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => new TransactionVelocityRule(threshold: 1, window: TimeSpan.FromMinutes(15)));

    [Fact]
    public void Velocity_refuses_a_window_that_is_not_positive() =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => new TransactionVelocityRule(threshold: 5, window: TimeSpan.Zero));

    [Fact]
    public void Impossible_travel_refuses_a_speed_that_is_not_positive() =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => new ImpossibleTravelRule(maximumSpeedKilometresPerHour: 0, window: TimeSpan.FromHours(12)));
}
