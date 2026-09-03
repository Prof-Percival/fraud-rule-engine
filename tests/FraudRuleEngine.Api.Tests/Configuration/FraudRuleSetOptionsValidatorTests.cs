using FraudRuleEngine.Api.Configuration;
using FraudRuleEngine.Api.Tests.TestSupport;
using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Api.Tests.Configuration;

public sealed class FraudRuleSetOptionsValidatorTests
{
    private readonly FraudRuleSetOptionsValidator _validator = new();

    [Fact]
    public void Accepts_the_shipped_configuration()
    {
        _validator.Validate(null, Valid()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_a_missing_version()
    {
        var options = Valid();
        options.Version = "";

        var result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Version");
    }

    [Fact]
    public void Rejects_a_review_threshold_not_below_decline()
    {
        var options = Valid();
        options.Scoring.ReviewThreshold = options.Scoring.DeclineThreshold;

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Rejects_a_non_positive_weight(int weight)
    {
        var options = Valid();
        options.Scoring.MediumSeverityWeight = weight;

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_a_rule_with_no_currency_thresholds()
    {
        var options = Valid();
        options.Rules.HighValueTransaction.Thresholds.Clear();

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_the_unknown_category_on_the_watchlist()
    {
        var options = Valid();
        options.Rules.HighRiskCategory.Categories.Add(TransactionCategory.Unknown);

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Collects_every_problem_rather_than_stopping_at_the_first()
    {
        var options = Valid();
        options.Version = "";
        options.Scoring.LowSeverityWeight = 0;
        options.Rules.AmountEscalation.Multiple = 1m;

        var result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        (result.Failures?.Count() ?? 0).ShouldBeGreaterThanOrEqualTo(3);
    }

    private static FraudRuleSetOptions Valid() => RuleSetSamples.Valid();
}
