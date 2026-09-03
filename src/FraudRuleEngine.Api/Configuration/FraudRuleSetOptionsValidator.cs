using FraudRuleEngine.Domain.Assessments;
using FraudRuleEngine.Domain.Scoring;
using FraudRuleEngine.Domain.Transactions;
using Microsoft.Extensions.Options;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Checks the rule set at startup so a bad threshold fails the boot, where a deployment notices, rather
/// than silently disabling a rule or scoring nonsense in production.
/// </summary>
internal sealed class FraudRuleSetOptionsValidator : IValidateOptions<FraudRuleSetOptions>
{
    public ValidateOptionsResult Validate(string? name, FraudRuleSetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Version))
        {
            errors.Add("RuleSet:Version is required, so every assessment records which rule set produced it.");
        }
        else if (options.Version.Length > RuleSetVersion.MaximumLength)
        {
            errors.Add($"RuleSet:Version must be at most {RuleSetVersion.MaximumLength} characters.");
        }

        ValidateScoring(options.Scoring, errors);
        ValidateRules(options.Rules, errors);

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateScoring(ScoringOptions scoring, List<string> errors)
    {
        RequirePositive(scoring.LowSeverityWeight, "RuleSet:Scoring:LowSeverityWeight", errors);
        RequirePositive(scoring.MediumSeverityWeight, "RuleSet:Scoring:MediumSeverityWeight", errors);
        RequirePositive(scoring.HighSeverityWeight, "RuleSet:Scoring:HighSeverityWeight", errors);

        if (scoring.ReviewThreshold is <= 0 or > RiskScore.Maximum)
        {
            errors.Add($"RuleSet:Scoring:ReviewThreshold must be between 1 and {RiskScore.Maximum}.");
        }

        if (scoring.DeclineThreshold is <= 0 or > RiskScore.Maximum)
        {
            errors.Add($"RuleSet:Scoring:DeclineThreshold must be between 1 and {RiskScore.Maximum}.");
        }

        if (scoring.ReviewThreshold >= scoring.DeclineThreshold)
        {
            errors.Add("RuleSet:Scoring:ReviewThreshold must be below DeclineThreshold, or nothing reaches review.");
        }
    }

    private static void ValidateRules(RulesOptions rules, List<string> errors)
    {
        RequireThresholds(rules.HighValueTransaction.Thresholds, "RuleSet:Rules:HighValueTransaction", errors);

        RequireThresholds(
            rules.FirstTimeMerchantHighValue.Thresholds,
            "RuleSet:Rules:FirstTimeMerchantHighValue",
            errors);
        RequireAtLeast(
            rules.FirstTimeMerchantHighValue.MinimumBaselineTransactions,
            1,
            "RuleSet:Rules:FirstTimeMerchantHighValue:MinimumBaselineTransactions",
            errors);

        if (rules.HighRiskCategory.Categories.Count == 0)
        {
            errors.Add("RuleSet:Rules:HighRiskCategory:Categories needs at least one category.");
        }

        if (rules.HighRiskCategory.Categories.Contains(TransactionCategory.Unknown))
        {
            errors.Add("RuleSet:Rules:HighRiskCategory:Categories must not include Unknown, which carries no signal.");
        }

        foreach (var merchantId in rules.DeniedMerchant.MerchantIds)
        {
            if (string.IsNullOrWhiteSpace(merchantId))
            {
                errors.Add("RuleSet:Rules:DeniedMerchant:MerchantIds contains a blank entry.");
            }
        }

        if (rules.UnusualHour.WindowStart >= rules.UnusualHour.WindowEnd)
        {
            errors.Add("RuleSet:Rules:UnusualHour:WindowStart must be before WindowEnd.");
        }

        if (rules.AmountEscalation.Multiple <= 1m)
        {
            errors.Add("RuleSet:Rules:AmountEscalation:Multiple must be greater than one.");
        }

        RequireAtLeast(
            rules.AmountEscalation.MinimumBaselineTransactions,
            1,
            "RuleSet:Rules:AmountEscalation:MinimumBaselineTransactions",
            errors);

        RequireAtLeast(rules.TransactionVelocity.Threshold, 2, "RuleSet:Rules:TransactionVelocity:Threshold", errors);
        RequirePositiveWindow(rules.TransactionVelocity.Window, "RuleSet:Rules:TransactionVelocity:Window", errors);

        if (rules.ImpossibleTravel.MaximumSpeedKilometresPerHour <= 0)
        {
            errors.Add("RuleSet:Rules:ImpossibleTravel:MaximumSpeedKilometresPerHour must be positive.");
        }

        RequirePositiveWindow(rules.ImpossibleTravel.Window, "RuleSet:Rules:ImpossibleTravel:Window", errors);
    }

    private static void RequireThresholds(
        Dictionary<Currency, decimal> thresholds,
        string path,
        List<string> errors)
    {
        if (thresholds.Count == 0)
        {
            errors.Add($"{path}:Thresholds needs at least one currency, or the rule can never fire.");
            return;
        }

        foreach (var (currency, amount) in thresholds)
        {
            if (currency == Currency.None)
            {
                errors.Add($"{path}:Thresholds has an unrecognised currency.");
            }

            if (amount <= 0m)
            {
                errors.Add($"{path}:Thresholds for {currency} must be positive.");
            }
        }
    }

    private static void RequirePositive(int value, string path, List<string> errors)
    {
        if (value <= 0)
        {
            errors.Add($"{path} must be positive.");
        }
    }

    private static void RequireAtLeast(int value, int minimum, string path, List<string> errors)
    {
        if (value < minimum)
        {
            errors.Add($"{path} must be at least {minimum}.");
        }
    }

    private static void RequirePositiveWindow(TimeSpan window, string path, List<string> errors)
    {
        if (window <= TimeSpan.Zero)
        {
            errors.Add($"{path} must be a positive duration.");
        }
    }
}
