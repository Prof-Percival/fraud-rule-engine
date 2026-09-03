using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// The tunable rule set, bound from the <c>RuleSet</c> configuration section and validated at startup.
/// </summary>
public sealed class FraudRuleSetOptions
{
    public const string SectionName = "RuleSet";

    /// <summary>An operator meaningful label for this configuration, such as a release name.</summary>
    public string Version { get; set; } = string.Empty;

    public ScoringOptions Scoring { get; set; } = new();

    public RulesOptions Rules { get; set; } = new();
}

public sealed class ScoringOptions
{
    public int LowSeverityWeight { get; set; }

    public int MediumSeverityWeight { get; set; }

    public int HighSeverityWeight { get; set; }

    public int ReviewThreshold { get; set; }

    public int DeclineThreshold { get; set; }
}

public sealed class RulesOptions
{
    public CurrencyThresholdOptions HighValueTransaction { get; set; } = new();

    public FirstTimeMerchantHighValueOptions FirstTimeMerchantHighValue { get; set; } = new();

    public HighRiskCategoryOptions HighRiskCategory { get; set; } = new();

    public DeniedMerchantOptions DeniedMerchant { get; set; } = new();

    public UnusualHourOptions UnusualHour { get; set; } = new();

    public AmountEscalationOptions AmountEscalation { get; set; } = new();

    public TransactionVelocityOptions TransactionVelocity { get; set; } = new();

    public ImpossibleTravelOptions ImpossibleTravel { get; set; } = new();
}

public sealed class CurrencyThresholdOptions
{
    public Dictionary<Currency, decimal> Thresholds { get; } = new();
}

public sealed class FirstTimeMerchantHighValueOptions
{
    public Dictionary<Currency, decimal> Thresholds { get; } = new();

    public int MinimumBaselineTransactions { get; set; }
}

public sealed class HighRiskCategoryOptions
{
    public IList<TransactionCategory> Categories { get; } = [];
}

public sealed class DeniedMerchantOptions
{
    public IList<string> MerchantIds { get; } = [];
}

public sealed class UnusualHourOptions
{
    public TimeOnly WindowStart { get; set; }

    public TimeOnly WindowEnd { get; set; }
}

public sealed class AmountEscalationOptions
{
    public decimal Multiple { get; set; }

    public int MinimumBaselineTransactions { get; set; }
}

public sealed class TransactionVelocityOptions
{
    public int Threshold { get; set; }

    public TimeSpan Window { get; set; }
}

public sealed class ImpossibleTravelOptions
{
    public double MaximumSpeedKilometresPerHour { get; set; }

    public TimeSpan Window { get; set; }
}
