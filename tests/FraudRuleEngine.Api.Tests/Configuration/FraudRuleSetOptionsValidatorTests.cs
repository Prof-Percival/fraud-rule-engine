using FraudRuleEngine.Api.Configuration;
using FraudRuleEngine.Domain.Transactions;
using Microsoft.Extensions.Options;

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

    private static FraudRuleSetOptions Valid()
    {
        var options = new FraudRuleSetOptions
        {
            Version = "test",
            Scoring = new ScoringOptions
            {
                LowSeverityWeight = 10,
                MediumSeverityWeight = 25,
                HighSeverityWeight = 45,
                ReviewThreshold = 40,
                DeclineThreshold = 75,
            },
        };

        options.Rules.HighValueTransaction.Thresholds[Currency.Zar] = 25_000m;
        options.Rules.FirstTimeMerchantHighValue.Thresholds[Currency.Zar] = 5_000m;
        options.Rules.FirstTimeMerchantHighValue.MinimumBaselineTransactions = 10;
        options.Rules.HighRiskCategory.Categories.Add(TransactionCategory.Gambling);
        options.Rules.DeniedMerchant.MerchantIds.Add("MERCH-DENY-0001");
        options.Rules.UnusualHour.WindowStart = new TimeOnly(1, 0);
        options.Rules.UnusualHour.WindowEnd = new TimeOnly(5, 0);
        options.Rules.AmountEscalation.Multiple = 5m;
        options.Rules.AmountEscalation.MinimumBaselineTransactions = 10;
        options.Rules.TransactionVelocity.Threshold = 5;
        options.Rules.TransactionVelocity.Window = TimeSpan.FromMinutes(15);
        options.Rules.ImpossibleTravel.MaximumSpeedKilometresPerHour = 1_000;
        options.Rules.ImpossibleTravel.Window = TimeSpan.FromHours(12);

        return options;
    }
}
