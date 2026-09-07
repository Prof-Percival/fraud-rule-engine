using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FraudRuleEngine.IntegrationTests;

/// <summary>
/// Who gets in, and how much they may ask for.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedFraudEngine.Name)]
public sealed class SecurityTests(FraudEngineFixture fixture)
{
    private const string KeyAlpha = "rate-limit-key-alpha-01";
    private const string KeyBravo = "rate-limit-key-bravo-01";

    [Theory]
    [InlineData("/api/v1/rules")]
    [InlineData("/api/v1/assessments")]
    [InlineData("/api/v1/assessments/summary")]
    [InlineData("/api/v1/customers/CUST-0000000000000001/assessments")]
    public async Task Refuses_a_read_with_no_key(string route)
    {
        var response = await fixture.Anonymous.GetAsync(
            new Uri(route, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refuses_a_submission_with_no_key()
    {
        // The write path is the one worth being sure about.
        var response = await fixture.Anonymous.PostAsJsonAsync(
            "/api/v1/transactions/evaluate",
            ATransaction.Valid(),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refuses_a_key_it_does_not_know()
    {
        using var response = await fixture.Anonymous.SendAsync(
            WithKey("/api/v1/rules", "a-key-that-was-never-issued-0001"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Never_repeats_the_key_it_rejected()
    {
        const string Rejected = "a-key-that-was-never-issued-0002";

        using var response = await fixture.Anonymous.SendAsync(
            WithKey("/api/v1/rules", Rejected), TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // A refusal that quotes the key back puts it wherever the response is captured.
        body.ShouldNotContain(Rejected);
        response.Headers.ToString().ShouldNotContain(Rejected);
    }

    [Fact]
    public async Task Accepts_a_key_it_knows()
    {
        var response = await fixture.Client.GetAsync(
            new Uri("/api/v1/rules", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Leaves_the_health_probes_open(string route)
    {
        // An orchestrator has no key, so a probe behind authentication would report a healthy service
        // as unreachable.
        var response = await fixture.Anonymous.GetAsync(
            new Uri(route, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refuses_once_the_allowance_is_spent()
    {
        const int Allowance = 3;

        await using var host = HostWithAllowance(Allowance);
        using var client = host.ClientWithKey(KeyAlpha);

        for (var spent = 0; spent < Allowance; spent++)
        {
            using var allowed = await client.GetAsync(
                new Uri("/api/v1/rules", UriKind.Relative), TestContext.Current.CancellationToken);

            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var refused = await client.GetAsync(
            new Uri("/api/v1/rules", UriKind.Relative), TestContext.Current.CancellationToken);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Told when to come back, rather than left to guess.
        refused.Headers.RetryAfter.ShouldNotBeNull();

        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("status").GetInt32().ShouldBe(429);
        problem.GetProperty("title").GetString().ShouldBe("Too many requests.");
    }

    [Fact]
    public async Task Spends_one_client_allowance_without_touching_another()
    {
        const int Allowance = 2;

        await using var host = HostWithAllowance(Allowance);
        using var alpha = host.ClientWithKey(KeyAlpha);
        using var bravo = host.ClientWithKey(KeyBravo);

        for (var spent = 0; spent < Allowance; spent++)
        {
            using var allowed = await alpha.GetAsync(
                new Uri("/api/v1/rules", UriKind.Relative), TestContext.Current.CancellationToken);

            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var exhausted = await alpha.GetAsync(
            new Uri("/api/v1/rules", UriKind.Relative), TestContext.Current.CancellationToken);

        exhausted.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // The partition is the client the key belongs to, so a noisy caller cannot spend anyone else's
        // window. This only holds while authentication runs before the limiter.
        using var unaffected = await bravo.GetAsync(
            new Uri("/api/v1/rules", UriKind.Relative), TestContext.Current.CancellationToken);

        unaffected.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Gives_each_client_the_allowance_it_was_configured_with()
    {
        const int Alpha = 2;
        const int Bravo = 6;

        await using var host = new FraudEngineHost(
            fixture.ConnectionString,
            requestsPerWindow: 1,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [KeyAlpha] = "rate-limit-alpha",
                [KeyBravo] = "rate-limit-bravo",
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["rate-limit-alpha"] = Alpha,
                ["rate-limit-bravo"] = Bravo,
            });

        // The default is one request, so neither client is running on it. Throttling a single caller has
        // to be a property of that caller rather than something the whole service shares.
        (await Allowed(host, KeyAlpha, attempts: Alpha + 2)).ShouldBe(Alpha);
        (await Allowed(host, KeyBravo, attempts: Bravo)).ShouldBe(Bravo);
    }

    private static async Task<int> Allowed(FraudEngineHost host, string apiKey, int attempts)
    {
        using var client = host.ClientWithKey(apiKey);
        var allowed = 0;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            using var response = await client.GetAsync(
                new Uri("/api/v1/rules", UriKind.Relative), TestContext.Current.CancellationToken);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                allowed++;
            }
        }

        return allowed;
    }

    private static HttpRequestMessage WithKey(string route, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));
        request.Headers.Add("X-Api-Key", apiKey);

        return request;
    }

    private FraudEngineHost HostWithAllowance(int requestsPerWindow) =>
        new(
            fixture.ConnectionString,
            requestsPerWindow,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [KeyAlpha] = "rate-limit-alpha",
                [KeyBravo] = "rate-limit-bravo",
            });
}
