using FraudRuleEngine.Api.Configuration;
using FraudRuleEngine.Api.Tests.TestSupport;
using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Transactions;
using Microsoft.Extensions.Options;

namespace FraudRuleEngine.Api.Tests.Configuration;

public sealed class ConfiguredRuleSetVersionProviderTests
{
    [Fact]
    public void Keeps_the_operator_label()
    {
        var version = Version(RuleSetSamples.Valid());

        version.Value.ShouldStartWith("test+");
    }

    [Fact]
    public void Is_stable_for_the_same_configuration()
    {
        Version(RuleSetSamples.Valid()).ShouldBe(Version(RuleSetSamples.Valid()));
    }

    [Fact]
    public void Changes_when_a_threshold_changes_even_if_the_label_does_not()
    {
        var original = Version(RuleSetSamples.Valid());

        var tweaked = RuleSetSamples.Valid();
        tweaked.Rules.HighValueTransaction.Thresholds[Currency.Zar] = 26_000m;

        var changed = Version(tweaked);

        changed.ShouldNotBe(original);
        changed.Value.ShouldStartWith("test+");
    }

    [Fact]
    public void Stays_within_the_length_a_version_allows()
    {
        var options = RuleSetSamples.Valid();
        options.Version = new string('v', FraudRuleSetOptions.MaximumVersionLabelLength);

        Version(options).Value.Length.ShouldBeLessThanOrEqualTo(RuleSetVersion.MaximumLength);
    }

    private static RuleSetVersion Version(FraudRuleSetOptions options) =>
        new ConfiguredRuleSetVersionProvider(new StaticMonitor(options)).Current;

    private sealed class StaticMonitor(FraudRuleSetOptions value) : IOptionsMonitor<FraudRuleSetOptions>
    {
        public FraudRuleSetOptions CurrentValue { get; } = value;

        public FraudRuleSetOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<FraudRuleSetOptions, string?> listener) => null;
    }
}
