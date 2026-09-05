using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FraudRuleEngine.IntegrationTests;

/// <summary>
/// Keyset paging over more records than fit on one page.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedFraudEngine.Name)]
public sealed class PaginationTests(FraudEngineFixture fixture)
{
    [Fact]
    public async Task Walks_every_assessment_once_across_a_page_boundary()
    {
        var customerId = ATransaction.NewCustomerId();
        var submitted = await Submit(customerId, count: 7);

        const int PageSize = 3;
        var seen = new List<Guid>();
        var timestamps = new List<DateTimeOffset>();
        var pageSizes = new List<int>();
        string? cursor = null;

        do
        {
            var query = $"/api/v1/customers/{customerId}/assessments?pageSize={PageSize}"
                + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");

            var page = await fixture.Client.GetFromJsonAsync<JsonElement>(
                query, TestContext.Current.CancellationToken);

            var items = page.GetProperty("items").EnumerateArray().ToList();
            pageSizes.Add(items.Count);

            foreach (var item in items)
            {
                seen.Add(item.GetProperty("id").GetGuid());
                timestamps.Add(item.GetProperty("evaluatedAtUtc").GetDateTimeOffset());
            }

            cursor = page.GetProperty("nextCursor").GetString();

            // hasMore is derived from the cursor, so the two must never disagree.
            page.GetProperty("hasMore").GetBoolean().ShouldBe(cursor is not null);

            // A cursor that stopped advancing would loop here rather than fail an assertion.
            pageSizes.Count.ShouldBeLessThanOrEqualTo(5);
        }
        while (cursor is not null);

        pageSizes.ShouldBe([3, 3, 1]);
        seen.Count.ShouldBe(7);
        seen.Distinct().Count().ShouldBe(7);
        seen.Order().ShouldBe(submitted.Order());

        // Newest first. Equal timestamps are allowed, which is why the cursor carries the id as well.
        timestamps.ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task Refuses_a_cursor_it_did_not_issue()
    {
        var customerId = ATransaction.NewCustomerId();

        var response = await fixture.Client.GetAsync(
            new Uri($"/api/v1/customers/{customerId}/assessments?cursor=not-a-cursor", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("errors").GetProperty("cursor").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Refuses_a_cursor_it_did_not_issue_when_searching()
    {
        var response = await fixture.Client.GetAsync(
            new Uri("/api/v1/assessments?cursor=%2F%2Fnonsense", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("errors").GetProperty("cursor").GetArrayLength().ShouldBe(1);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(5000, 200)]
    public async Task Brings_an_impossible_page_size_into_range(int asked, int expected)
    {
        var customerId = ATransaction.NewCustomerId();

        var page = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/customers/{customerId}/assessments?pageSize={asked.ToString(CultureInfo.InvariantCulture)}",
            TestContext.Current.CancellationToken);

        // Reported back as the size actually used, so a caller is not left guessing.
        page.GetProperty("pageSize").GetInt32().ShouldBe(expected);
    }

    [Fact]
    public async Task Leaves_rule_outcomes_off_the_list_and_on_the_detail()
    {
        var customerId = ATransaction.NewCustomerId();
        var submitted = await Submit(customerId, count: 1);

        var page = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/customers/{customerId}/assessments", TestContext.Current.CancellationToken);

        var listed = page.GetProperty("items").EnumerateArray().Single();
        listed.GetProperty("ruleOutcomes").GetArrayLength().ShouldBe(0);

        var detail = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/assessments/{submitted[0]}", TestContext.Current.CancellationToken);

        detail.GetProperty("ruleOutcomes").GetArrayLength().ShouldBe(8);
    }

    [Fact]
    public async Task Returns_an_empty_page_for_a_customer_it_has_never_seen()
    {
        var page = await fixture.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/customers/{ATransaction.NewCustomerId()}/assessments",
            TestContext.Current.CancellationToken);

        page.GetProperty("items").GetArrayLength().ShouldBe(0);
        page.GetProperty("hasMore").GetBoolean().ShouldBeFalse();
        page.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>Submits sequentially, so the stored order is the order they were sent.</summary>
    private async Task<List<Guid>> Submit(string customerId, int count)
    {
        var identifiers = new List<Guid>(count);

        for (var index = 0; index < count; index++)
        {
            using var response = await fixture.Client.PostAsJsonAsync(
                "/api/v1/transactions/evaluate",
                ATransaction.Valid(customerId: customerId, amount: 100m + index),
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(
                TestContext.Current.CancellationToken);

            identifiers.Add(body.GetProperty("assessmentId").GetGuid());
        }

        return identifiers;
    }
}
