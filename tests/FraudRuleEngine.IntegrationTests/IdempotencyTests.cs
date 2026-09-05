using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FraudRuleEngine.IntegrationTests;

/// <summary>
/// Repeat delivery of the same event, which a queue will do sooner or later.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedFraudEngine.Name)]
public sealed class IdempotencyTests(FraudEngineFixture fixture)
{
    [Fact]
    public async Task Returns_the_first_verdict_when_the_same_event_is_delivered_twice()
    {
        var payload = ATransaction.Valid(customerId: ATransaction.NewCustomerId(), amount: 1_450m);

        var first = await Evaluate(payload);
        var second = await Evaluate(payload);

        // Same row, and the same moment of judgement: a re-evaluation would carry a later timestamp.
        second.GetProperty("assessmentId").GetGuid()
            .ShouldBe(first.GetProperty("assessmentId").GetGuid());
        second.GetProperty("evaluatedAt").GetDateTimeOffset()
            .ShouldBe(first.GetProperty("evaluatedAt").GetDateTimeOffset());
        second.GetProperty("riskScore").GetInt32().ShouldBe(first.GetProperty("riskScore").GetInt32());
        second.GetProperty("decision").GetString().ShouldBe(first.GetProperty("decision").GetString());
    }

    [Fact]
    public async Task Ignores_a_changed_amount_on_a_repeat_delivery()
    {
        var eventId = $"it-{Guid.NewGuid():N}";
        var customerId = ATransaction.NewCustomerId();

        var modest = ATransaction.Valid(eventId: eventId, customerId: customerId, amount: 210m);
        var enormous = ATransaction.Valid(eventId: eventId, customerId: customerId, amount: 480_000m);

        var original = await Evaluate(modest);
        var replay = await Evaluate(enormous);

        replay.GetProperty("assessmentId").GetGuid()
            .ShouldBe(original.GetProperty("assessmentId").GetGuid());
        replay.GetProperty("riskScore").GetInt32().ShouldBe(original.GetProperty("riskScore").GetInt32());

        // The assertion above only means something if the larger amount would have scored differently,
        // so establish that under an event id of its own.
        var scoredOnItsOwn = await Evaluate(
            ATransaction.Valid(customerId: ATransaction.NewCustomerId(), amount: 480_000m));

        scoredOnItsOwn.GetProperty("riskScore").GetInt32()
            .ShouldBeGreaterThan(original.GetProperty("riskScore").GetInt32());
    }

    [Fact]
    public async Task Creates_one_assessment_when_eight_copies_arrive_at_once()
    {
        var customerId = ATransaction.NewCustomerId();
        var payload = ATransaction.Valid(customerId: customerId, amount: 3_300m);

        var deliveries = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            fixture.Client.PostAsJsonAsync(
                "/api/v1/transactions/evaluate", payload, TestContext.Current.CancellationToken)));

        try
        {
            foreach (var delivery in deliveries)
            {
                delivery.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            var identifiers = new List<Guid>();
            foreach (var delivery in deliveries)
            {
                var body = await delivery.Content.ReadFromJsonAsync<JsonElement>(
                    TestContext.Current.CancellationToken);
                identifiers.Add(body.GetProperty("assessmentId").GetGuid());
            }

            // The loser of the race must report the winner's verdict, not its own discarded one.
            identifiers.Distinct().Count().ShouldBe(1);
        }
        finally
        {
            foreach (var delivery in deliveries)
            {
                delivery.Dispose();
            }
        }

        var page = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/customers/{customerId}/assessments", TestContext.Current.CancellationToken);

        page.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Treats_a_repeated_event_inside_a_batch_as_one_assessment()
    {
        var customerId = ATransaction.NewCustomerId();
        var payload = ATransaction.Valid(customerId: customerId, amount: 890m);

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/transactions/batch",
            new { transactions = new[] { payload, payload } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        // A duplicate is not a rejection. Both items are accepted and both name the same assessment.
        body.GetProperty("accepted").GetInt32().ShouldBe(2);
        body.GetProperty("rejected").GetInt32().ShouldBe(0);

        var results = body.GetProperty("results").EnumerateArray().ToList();
        results
            .Select(result => result.GetProperty("assessment").GetProperty("assessmentId").GetGuid())
            .Distinct()
            .Count()
            .ShouldBe(1);

        var page = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/customers/{customerId}/assessments", TestContext.Current.CancellationToken);

        page.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    private async Task<JsonElement> Evaluate(Dictionary<string, object?> payload)
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/transactions/evaluate", payload, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
    }
}
