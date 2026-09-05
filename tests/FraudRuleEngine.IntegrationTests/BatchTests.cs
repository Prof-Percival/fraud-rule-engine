using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FraudRuleEngine.IntegrationTests;

/// <summary>
/// Submitting many at once, where one bad item must not cost the rest.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedFraudEngine.Name)]
public sealed class BatchTests(FraudEngineFixture fixture)
{
    [Fact]
    public async Task Keeps_the_good_items_when_one_is_malformed()
    {
        var customerId = ATransaction.NewCustomerId();

        var malformed = ATransaction.Valid(customerId: customerId);
        malformed["amount"] = -1m;

        var submitted = new[]
        {
            ATransaction.Valid(customerId: customerId, amount: 120m),
            malformed,
            ATransaction.Valid(customerId: customerId, amount: 340m),
        };

        var body = await Submit(submitted);

        body.GetProperty("submitted").GetInt32().ShouldBe(3);
        body.GetProperty("accepted").GetInt32().ShouldBe(2);
        body.GetProperty("rejected").GetInt32().ShouldBe(1);

        var results = body.GetProperty("results").EnumerateArray().ToList();

        var refused = results.Single(result => !result.GetProperty("accepted").GetBoolean());
        refused.GetProperty("index").GetInt32().ShouldBe(1);
        refused.GetProperty("errors").EnumerateObject().Count().ShouldBeGreaterThan(0);

        // The two that were fine are readable afterwards, which is what makes them accepted rather than
        // merely acknowledged.
        foreach (var accepted in results.Where(result => result.GetProperty("accepted").GetBoolean()))
        {
            var id = accepted.GetProperty("assessment").GetProperty("assessmentId").GetGuid();

            using var stored = await fixture.Client.GetAsync(
                new Uri($"/api/v1/assessments/{id}", UriKind.Relative),
                TestContext.Current.CancellationToken);

            stored.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Names_the_position_and_event_of_every_item()
    {
        var customerId = ATransaction.NewCustomerId();
        var submitted = new[]
        {
            ATransaction.Valid(customerId: customerId, amount: 60m),
            ATransaction.Valid(customerId: customerId, amount: 70m),
        };

        var results = (await Submit(submitted)).GetProperty("results").EnumerateArray().ToList();

        // Position and event id on every result, so a caller can line the answers up with what it sent
        // rather than guessing from the order.
        for (var index = 0; index < submitted.Length; index++)
        {
            results[index].GetProperty("index").GetInt32().ShouldBe(index);
            results[index].GetProperty("eventId").GetString()
                .ShouldBe(submitted[index]["eventId"]!.ToString());
        }
    }

    [Fact]
    public async Task Lets_a_later_item_see_the_ones_before_it()
    {
        var customerId = ATransaction.NewCustomerId();
        var occurredAt = new DateTimeOffset(2026, 8, 20, 14, 0, 0, TimeSpan.FromHours(2));

        // Seven inside the velocity window, which is more than the rule allows, so the rule can only fire
        // if the items are assessed in order against the ones already written.
        var submitted = Enumerable.Range(0, 7)
            .Select(minute => ATransaction.Valid(
                customerId: customerId,
                amount: 200m + minute,
                occurredAt: occurredAt.AddMinutes(minute)))
            .ToArray();

        var results = (await Submit(submitted)).GetProperty("results").EnumerateArray().ToList();

        results.ShouldAllBe(result => result.GetProperty("accepted").GetBoolean());

        Velocity(results[0]).ShouldBeFalse();
        Velocity(results[^1]).ShouldBeTrue();
    }

    [Fact]
    public async Task Refuses_a_batch_with_nothing_in_it()
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/transactions/batch",
            new { transactions = Array.Empty<object>() },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("errors").GetProperty("Transactions").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Refuses_a_batch_larger_than_it_will_take()
    {
        var customerId = ATransaction.NewCustomerId();
        var tooMany = Enumerable.Range(0, 501)
            .Select(_ => ATransaction.Valid(customerId: customerId))
            .ToArray();

        using var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/transactions/batch",
            new { transactions = tooMany },
            TestContext.Current.CancellationToken);

        // Refused whole, before any of it is assessed, rather than partly applied.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("errors").GetProperty("Transactions").GetArrayLength().ShouldBe(1);
    }

    private static bool Velocity(JsonElement result) =>
        result.GetProperty("assessment").GetProperty("ruleOutcomes").EnumerateArray()
            .Single(outcome => outcome.GetProperty("ruleId").GetString() == "TransactionVelocity")
            .GetProperty("triggered").GetBoolean();

    private async Task<JsonElement> Submit(IReadOnlyCollection<Dictionary<string, object?>> transactions)
    {
        using var response = await fixture.Client.PostAsJsonAsync(
            "/api/v1/transactions/batch",
            new { transactions },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
    }
}
