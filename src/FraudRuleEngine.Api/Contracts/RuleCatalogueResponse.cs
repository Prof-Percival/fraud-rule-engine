namespace FraudRuleEngine.Api.Contracts;

/// <summary>Which rules are live and the numbers they are running on.</summary>
public sealed record RuleCatalogueResponse
{
    /// <summary>Matches the version stamped on assessments, so a verdict can be tied to these numbers.</summary>
    public required string RuleSetVersion { get; init; }

    public required ScoringResponse Scoring { get; init; }

    public required IReadOnlyList<RuleResponse> Rules { get; init; }
}

public sealed record ScoringResponse
{
    public required int LowSeverityWeight { get; init; }

    public required int MediumSeverityWeight { get; init; }

    public required int HighSeverityWeight { get; init; }

    /// <summary>At or above this a transaction goes to a person.</summary>
    public required int ReviewThreshold { get; init; }

    /// <summary>At or above this it is refused.</summary>
    public required int DeclineThreshold { get; init; }
}

public sealed record RuleResponse
{
    public required string Id { get; init; }

    /// <summary>
    /// The rule's settings as text, keyed by name. Text because the settings differ in shape between
    /// rules, being amounts per currency in one and a count and a window in another.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Settings { get; init; }
}
