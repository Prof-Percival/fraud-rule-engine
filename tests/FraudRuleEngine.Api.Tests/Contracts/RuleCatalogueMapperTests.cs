using FraudRuleEngine.Api.Configuration;
using FraudRuleEngine.Api.Contracts;
using FraudRuleEngine.Api.Tests.TestSupport;
using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Rules;

namespace FraudRuleEngine.Api.Tests.Contracts;

public sealed class RuleCatalogueMapperTests
{
    private static readonly RuleSetVersion Version = RuleSetVersion.From("test+abcdef12");

    [Fact]
    public void Reports_every_registered_rule()
    {
        var options = RuleSetSamples.Valid();

        var response = RuleCatalogueMapper.Map(FraudRuleSetFactory.Rules(options.Rules), options, Version);

        response.Rules.Count.ShouldBe(8);
        response.Rules.Select(rule => rule.Id).ShouldBeUnique();
    }

    [Fact]
    public void Reports_the_rules_that_run_rather_than_the_configuration()
    {
        // Identity comes from the registered rules, so a catalogue cannot claim a rule that is not there.
        var options = RuleSetSamples.Valid();
        var onlyOne = FraudRuleSetFactory.Rules(options.Rules).Take(1).ToArray();

        var response = RuleCatalogueMapper.Map(onlyOne, options, Version);

        response.Rules.Count.ShouldBe(1);
    }

    [Fact]
    public void Carries_the_version_so_a_verdict_can_be_tied_to_these_numbers()
    {
        var options = RuleSetSamples.Valid();

        RuleCatalogueMapper.Map(FraudRuleSetFactory.Rules(options.Rules), options, Version)
            .RuleSetVersion.ShouldBe("test+abcdef12");
    }

    [Fact]
    public void Reports_the_scoring_in_force()
    {
        var options = RuleSetSamples.Valid();
        options.Scoring.ReviewThreshold = 45;

        var scoring = RuleCatalogueMapper.Map(FraudRuleSetFactory.Rules(options.Rules), options, Version).Scoring;

        scoring.ReviewThreshold.ShouldBe(45);
        scoring.DeclineThreshold.ShouldBe(options.Scoring.DeclineThreshold);
        scoring.HighSeverityWeight.ShouldBe(options.Scoring.HighSeverityWeight);
    }

    [Fact]
    public void Reports_a_changed_threshold()
    {
        var options = RuleSetSamples.Valid();
        options.Rules.TransactionVelocity.Threshold = 7;

        var response = RuleCatalogueMapper.Map(FraudRuleSetFactory.Rules(options.Rules), options, Version);

        Settings(response, "TransactionVelocity")["Threshold"].ShouldBe("7");
    }

    [Fact]
    public void Reports_amounts_per_currency()
    {
        var options = RuleSetSamples.Valid();

        Settings(RuleCatalogueMapper.Map(FraudRuleSetFactory.Rules(options.Rules), options, Version),
                "HighValueTransaction")["Thresholds"]
            .ShouldContain("ZAR 25000");
    }

    [Fact]
    public void Counts_the_deny_list_rather_than_naming_it()
    {
        // Naming who is blocked would tell any key holder which merchants to avoid.
        var options = RuleSetSamples.Valid();
        options.Rules.DeniedMerchant.MerchantIds.Add("MERCH-DENY-0002");

        var settings = Settings(
            RuleCatalogueMapper.Map(FraudRuleSetFactory.Rules(options.Rules), options, Version),
            "DeniedMerchant");

        settings["MerchantCount"].ShouldBe("2");
        settings.Values.ShouldNotContain(value => value.Contains("MERCH-DENY", StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, string> Settings(RuleCatalogueResponse response, string id) =>
        response.Rules.Single(rule => rule.Id == id).Settings;
}
