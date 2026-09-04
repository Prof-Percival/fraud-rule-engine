using FraudRuleEngine.Application.Abstractions;
using FraudRuleEngine.Domain.Rules;
using FraudRuleEngine.Domain.Scoring;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FraudRuleEngine.Api.Endpoints;

internal static class AssessmentEndpoints
{
    internal static IEndpointRouteBuilder MapAssessmentEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var assessments = routes.MapGroup("/api/v1/assessments").WithTags("Assessments");

        assessments.MapGet("/", SearchAsync)
            .WithName("SearchAssessments")
            .WithSummary("Filter and page stored assessments.");

        // Before the {id} route, or "summary" is parsed as an identifier and the route never matches.
        assessments.MapGet("/summary", SummariseAsync)
            .WithName("SummariseAssessments")
            .WithSummary("Counts by decision and the rules firing most often.");

        assessments.MapGet("/{id:guid}", FindAsync)
            .WithName("GetAssessment")
            .WithSummary("One assessment with every rule outcome.");

        routes.MapGet("/api/v1/customers/{customerId}/assessments", ForCustomerAsync)
            .WithTags("Assessments")
            .WithName("GetCustomerAssessments")
            .WithSummary("Assessments for one customer, newest first.");

        routes.MapGet("/api/v1/rules", ListRules)
            .WithTags("Rules")
            .WithName("ListRules")
            .WithSummary("Which rules are live.");

        return routes;
    }

    private static async Task<Results<Ok<AssessmentPage>, ValidationProblem>> SearchAsync(
        IAssessmentQueries queries,
        CancellationToken cancellationToken,
        string? customerId = null,
        FraudDecision? decision = null,
        int? minScore = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? cursor = null,
        int pageSize = AssessmentQuery.DefaultPageSize)
    {
        AssessmentCursor? after = null;

        if (cursor is not null)
        {
            if (!AssessmentCursor.TryDecode(cursor, out var decoded))
            {
                return TypedResults.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["cursor"] = ["Not a valid cursor. Use the nextCursor from a previous response."],
                    },
                    title: "The query could not be accepted.");
            }

            after = decoded;
        }

        var result = await queries.SearchAsync(
            new AssessmentQuery
            {
                CustomerId = customerId,
                Decision = decision,
                MinimumRiskScore = minScore,
                From = from,
                To = to,
                After = after,
                PageSize = pageSize,
            },
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<AssessmentSummary>> SummariseAsync(
        IAssessmentQueries queries,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await queries.SummariseAsync(cancellationToken).ConfigureAwait(false));

    private static async Task<Results<Ok<AssessmentView>, NotFound>> FindAsync(
        Guid id,
        IAssessmentQueries queries,
        CancellationToken cancellationToken)
    {
        var assessment = await queries.FindAsync(id, cancellationToken).ConfigureAwait(false);

        return assessment is null ? TypedResults.NotFound() : TypedResults.Ok(assessment);
    }

    private static async Task<Ok<AssessmentPage>> ForCustomerAsync(
        string customerId,
        IAssessmentQueries queries,
        CancellationToken cancellationToken,
        string? cursor = null,
        int pageSize = AssessmentQuery.DefaultPageSize)
    {
        AssessmentCursor? after = AssessmentCursor.TryDecode(cursor, out var decoded) ? decoded : null;

        // No 404 for a customer with no assessments. An empty page is the correct answer: this service
        // has no customer records of its own, so it cannot tell an unknown customer from a quiet one.
        var result = await queries.SearchAsync(
            new AssessmentQuery { CustomerId = customerId, After = after, PageSize = pageSize },
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(result);
    }

    /// <summary>
    /// Lists the registered rules.
    /// </summary>
    /// <remarks>
    /// Exists so somebody operating this can see which rules are live without reading the source or the
    /// database. Thresholds are not exposed yet because they are still compiled in; once they are
    /// configuration this is where they belong.
    /// </remarks>
    private static Ok<IReadOnlyList<string>> ListRules(IReadOnlyList<IFraudRule> rules) =>
        TypedResults.Ok<IReadOnlyList<string>>(
            [.. rules.Select(rule => rule.Id.Value).OrderBy(id => id, StringComparer.Ordinal)]);
}
