using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FraudRuleEngine.IntegrationTests;

/// <summary>
/// The round trip: submit a transaction, then read the assessment back out of the database.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedFraudEngine.Name)]
public sealed class EvaluateAndRetrieveTests(FraudEngineFixture fixture)
{
    [Fact]
    public async Task Evaluates_persists_and_returns_the_assessment()
    {
        var transaction = ATransaction.Valid(amount: 30_000m, category: "Retail");

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/transactions/evaluate", transaction, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var assessment = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        assessment.GetProperty("eventId").GetString().ShouldBe(transaction["eventId"]!.ToString());
        assessment.GetProperty("riskScore").GetInt32().ShouldBeGreaterThan(0);
        assessment.GetProperty("decision").GetString().ShouldBeOneOf("Approve", "Review", "Decline");
        assessment.GetProperty("ruleSetVersion").GetString().ShouldNotBeNullOrWhiteSpace();

        var id = assessment.GetProperty("assessmentId").GetGuid();

        // Read it back, which is the half that proves it was actually stored rather than only returned.
        var stored = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/assessments/{id}", TestContext.Current.CancellationToken);

        // The read model names it "id" where the evaluate response names it "assessmentId".
        stored.GetProperty("id").GetGuid().ShouldBe(id);

        // Exactly, not approximately. The response must not advertise a precision the database cannot
        // keep, or reading an assessment back contradicts the answer already given for it.
        stored.GetProperty("evaluatedAtUtc").GetDateTimeOffset()
            .ShouldBe(assessment.GetProperty("evaluatedAt").GetDateTimeOffset());
        stored.GetProperty("riskScore").GetInt32().ShouldBe(assessment.GetProperty("riskScore").GetInt32());
        stored.GetProperty("decision").GetString().ShouldBe(assessment.GetProperty("decision").GetString());
        stored.GetProperty("ruleSetVersion").GetString()
            .ShouldBe(assessment.GetProperty("ruleSetVersion").GetString());
    }

    [Fact]
    public async Task Stores_every_rule_that_ran_not_only_the_ones_that_fired()
    {
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/transactions/evaluate",
            ATransaction.Valid(amount: 120m),
            TestContext.Current.CancellationToken);

        var assessment = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        var outcomes = assessment.GetProperty("ruleOutcomes").EnumerateArray().ToList();

        // Eight rules run whatever the transaction looks like, and the clear ones carry a reason too,
        // because "which rules did not fire" is a question an analyst asks.
        outcomes.Count.ShouldBe(8);
        outcomes.ShouldAllBe(outcome => !string.IsNullOrWhiteSpace(outcome.GetProperty("reason").GetString()));

        var id = assessment.GetProperty("assessmentId").GetGuid();
        var stored = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/assessments/{id}", TestContext.Current.CancellationToken);

        stored.GetProperty("ruleOutcomes").EnumerateArray().Count().ShouldBe(8);
    }

    [Fact]
    public async Task Refuses_a_payload_that_is_missing_everything()
    {
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/transactions/evaluate",
            new Dictionary<string, object?>(StringComparer.Ordinal),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        // Every problem at once rather than one per round trip.
        problem.GetProperty("errors").EnumerateObject().Count().ShouldBeGreaterThan(5);
    }
}
