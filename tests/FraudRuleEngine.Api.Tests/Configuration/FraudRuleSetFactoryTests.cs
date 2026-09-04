using FraudRuleEngine.Api.Configuration;
using FraudRuleEngine.Api.Tests.TestSupport;

namespace FraudRuleEngine.Api.Tests.Configuration;

public sealed class FraudRuleSetFactoryTests
{
    [Fact]
    public void Builds_every_rule_in_the_set()
    {
        // The rules endpoint reports this same collection, so an empty or short list here means the
        // service would understate what it is running.
        FraudRuleSetFactory.Rules(RuleSetSamples.Valid().Rules).Count.ShouldBe(8);
    }

    [Fact]
    public void Gives_every_rule_a_usable_identifier()
    {
        var rules = FraudRuleSetFactory.Rules(RuleSetSamples.Valid().Rules);

        rules.ShouldAllBe(rule => rule.Id.IsInitialised);
        rules.Select(rule => rule.Id).Distinct().Count().ShouldBe(rules.Count);
    }
}
