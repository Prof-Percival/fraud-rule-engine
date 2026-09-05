using System.Net.Http.Json;
using System.Text.Json;

namespace FraudRuleEngine.IntegrationTests;

/// <summary>
/// What survives the write, in the detail an analyst would need months later.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedFraudEngine.Name)]
public sealed class PersistenceTests(FraudEngineFixture fixture)
{
    /// <summary>Over the ZAR high value threshold and in a high risk category, so two rules fire.</summary>
    private static Dictionary<string, object?> ASuspiciousTransaction() =>
        ATransaction.Valid(
            customerId: ATransaction.NewCustomerId(),
            amount: 30_000m,
            category: "Gambling",
            channel: "Ecommerce");

    [Fact]
    public async Task Records_the_outcome_of_every_rule_that_is_registered()
    {
        var registered = await RegisteredRuleIds();

        var assessment = await Evaluate(ASuspiciousTransaction());
        var reported = RuleIds(assessment);

        // Compared against what the service says it is running, rather than a number written here that
        // would quietly stop meaning anything the moment a rule is added.
        reported.Order().ShouldBe(registered.Order());

        var stored = await Get($"/api/v1/assessments/{assessment.GetProperty("assessmentId").GetGuid()}");
        RuleIds(stored).Order().ShouldBe(registered.Order());
    }

    [Fact]
    public async Task Keeps_the_reason_and_severity_of_a_rule_that_fired()
    {
        var assessment = await Evaluate(ASuspiciousTransaction());
        var id = assessment.GetProperty("assessmentId").GetGuid();

        var reported = Outcome(assessment, "HighValueTransaction");
        reported.GetProperty("triggered").GetBoolean().ShouldBeTrue();
        reported.GetProperty("severity").GetString().ShouldBe("High");
        reported.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();

        var stored = Outcome(await Get($"/api/v1/assessments/{id}"), "HighValueTransaction");

        // The evidence is the point of storing it, so it has to come back unchanged.
        stored.GetProperty("triggered").GetBoolean().ShouldBeTrue();
        stored.GetProperty("severity").GetString().ShouldBe("High");
        stored.GetProperty("reason").GetString().ShouldBe(reported.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Explains_a_rule_that_stayed_quiet()
    {
        var assessment = await Evaluate(ASuspiciousTransaction());

        var quiet = Outcome(assessment, "DeniedMerchant");
        quiet.GetProperty("triggered").GetBoolean().ShouldBeFalse();
        quiet.GetProperty("severity").GetString().ShouldBe("None");

        // Why a rule did not fire is a question that gets asked, so a clear carries a reason too.
        quiet.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Keeps_the_outcomes_in_the_order_they_were_reported()
    {
        var assessment = await Evaluate(ASuspiciousTransaction());
        var stored = await Get($"/api/v1/assessments/{assessment.GetProperty("assessmentId").GetGuid()}");

        // Ordering is stored rather than incidental, so the rules that fired stay at the top of the list
        // where someone reading it will see them first.
        RuleIds(stored).ShouldBe(RuleIds(assessment));
    }

    [Fact]
    public async Task Stamps_the_rule_set_version_that_produced_the_verdict()
    {
        var published = (await Get("/api/v1/rules")).GetProperty("ruleSetVersion").GetString();

        var assessment = await Evaluate(ASuspiciousTransaction());
        assessment.GetProperty("ruleSetVersion").GetString().ShouldBe(published);

        var stored = await Get($"/api/v1/assessments/{assessment.GetProperty("assessmentId").GetGuid()}");

        // Without this a verdict cannot be explained after the rules change, which is the case where
        // explaining it actually matters.
        stored.GetProperty("ruleSetVersion").GetString().ShouldBe(published);
    }

    [Fact]
    public async Task Scores_a_verdict_from_the_weights_it_publishes()
    {
        var scoring = (await Get("/api/v1/rules")).GetProperty("scoring");
        var high = scoring.GetProperty("highSeverityWeight").GetInt32();
        var low = scoring.GetProperty("lowSeverityWeight").GetInt32();
        var reviewAt = scoring.GetProperty("reviewThreshold").GetInt32();
        var declineAt = scoring.GetProperty("declineThreshold").GetInt32();

        var assessment = await Evaluate(ASuspiciousTransaction());
        var score = assessment.GetProperty("riskScore").GetInt32();

        // One high and one low rule fired, so the score is those two weights and nothing else. Checked
        // against the published numbers, so configuring different ones cannot leave this passing while
        // the documented behaviour changes underneath it.
        score.ShouldBe(high + low);
        score.ShouldBeGreaterThanOrEqualTo(reviewAt);
        score.ShouldBeLessThan(declineAt);
        assessment.GetProperty("decision").GetString().ShouldBe("Review");
    }

    private static List<string> RuleIds(JsonElement assessment) =>
        [.. assessment.GetProperty("ruleOutcomes").EnumerateArray()
            .Select(outcome => outcome.GetProperty("ruleId").GetString()!)];

    private static JsonElement Outcome(JsonElement assessment, string ruleId) =>
        assessment.GetProperty("ruleOutcomes").EnumerateArray()
            .Single(outcome => outcome.GetProperty("ruleId").GetString() == ruleId);

    private async Task<List<string>> RegisteredRuleIds() =>
        [.. (await Get("/api/v1/rules")).GetProperty("rules").EnumerateArray()
            .Select(rule => rule.GetProperty("id").GetString()!)];

    private Task<JsonElement> Get(string route) =>
        fixture.Client.GetFromJsonAsync<JsonElement>(route, TestContext.Current.CancellationToken);

    private async Task<JsonElement> Evaluate(Dictionary<string, object?> payload)
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/transactions/evaluate", payload, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
    }
}
