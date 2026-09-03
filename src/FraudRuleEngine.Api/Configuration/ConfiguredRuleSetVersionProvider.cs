using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FraudRuleEngine.Application.Abstractions;
using FraudRuleEngine.Domain.Assessments;
using Microsoft.Extensions.Options;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Derives the rule set version from the configuration in force.
/// </summary>
/// <remarks>
/// The version is the operator label with a fingerprint of the effective values appended. The label
/// alone could drift: change a threshold, forget to bump the label, and old and new assessments would
/// share a version under which they were not scored. The fingerprint makes that impossible, since any
/// change to the numbers changes it. Reading the monitor's current value means a reload is reflected
/// rather than the value the process booted with.
/// </remarks>
internal sealed class ConfiguredRuleSetVersionProvider : IRuleSetVersionProvider
{
    private readonly IOptionsMonitor<FraudRuleSetOptions> _options;

    public ConfiguredRuleSetVersionProvider(IOptionsMonitor<FraudRuleSetOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public RuleSetVersion Current
    {
        get
        {
            var current = _options.CurrentValue;
            return RuleSetVersion.From($"{current.Version}+{Fingerprint(current)}");
        }
    }

    private static string Fingerprint(FraudRuleSetOptions options)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(options)));
        return Convert.ToHexStringLower(bytes.AsSpan(0, 4));
    }

    /// <summary>
    /// A stable string over every value that changes a decision. Collections are ordered, so a reordering
    /// that changes nothing does not change the fingerprint.
    /// </summary>
    private static string Canonical(FraudRuleSetOptions options)
    {
        var builder = new StringBuilder();
        var scoring = options.Scoring;

        builder.Append(CultureInfo.InvariantCulture, $"s:{scoring.LowSeverityWeight},")
            .Append(CultureInfo.InvariantCulture, $"{scoring.MediumSeverityWeight},")
            .Append(CultureInfo.InvariantCulture, $"{scoring.HighSeverityWeight},")
            .Append(CultureInfo.InvariantCulture, $"{scoring.ReviewThreshold},")
            .Append(CultureInfo.InvariantCulture, $"{scoring.DeclineThreshold};");

        var rules = options.Rules;
        AppendThresholds(builder, "hv", rules.HighValueTransaction.Thresholds);
        AppendThresholds(builder, "ftm", rules.FirstTimeMerchantHighValue.Thresholds);
        builder.Append(CultureInfo.InvariantCulture, $"ftmb:{rules.FirstTimeMerchantHighValue.MinimumBaselineTransactions};");

        var categories = rules.HighRiskCategory.Categories.Select(category => category.ToString()).Order(StringComparer.Ordinal);
        builder.Append(CultureInfo.InvariantCulture, $"cat:{string.Join(',', categories)};");

        var merchants = rules.DeniedMerchant.MerchantIds.Order(StringComparer.Ordinal);
        builder.Append(CultureInfo.InvariantCulture, $"deny:{string.Join(',', merchants)};");

        builder.Append(CultureInfo.InvariantCulture, $"uh:{rules.UnusualHour.WindowStart:HH:mm}-{rules.UnusualHour.WindowEnd:HH:mm};")
            .Append(CultureInfo.InvariantCulture, $"ae:{rules.AmountEscalation.Multiple},{rules.AmountEscalation.MinimumBaselineTransactions};")
            .Append(CultureInfo.InvariantCulture, $"tv:{rules.TransactionVelocity.Threshold},{rules.TransactionVelocity.Window};")
            .Append(CultureInfo.InvariantCulture, $"it:{rules.ImpossibleTravel.MaximumSpeedKilometresPerHour},{rules.ImpossibleTravel.Window};");

        return builder.ToString();
    }

    private static void AppendThresholds(
        StringBuilder builder,
        string prefix,
        Dictionary<Domain.Transactions.Currency, decimal> thresholds)
    {
        var ordered = thresholds
            .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
            .Select(entry => string.Create(CultureInfo.InvariantCulture, $"{entry.Key}={entry.Value}"));

        builder.Append(CultureInfo.InvariantCulture, $"{prefix}:{string.Join(',', ordered)};");
    }
}
