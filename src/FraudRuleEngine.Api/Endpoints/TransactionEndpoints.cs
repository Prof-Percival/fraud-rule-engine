using FraudRuleEngine.Api.Contracts;
using FraudRuleEngine.Application.Evaluation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FraudRuleEngine.Api.Endpoints;

internal static class TransactionEndpoints
{
    internal static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        // Versioned from the first endpoint. Cheap now, and retrofitting a prefix onto a published API
        // means either breaking callers or serving two shapes.
        var group = routes.MapGroup("/api/v1/transactions")
            .WithTags("Transactions");

        group.MapPost("/evaluate", EvaluateAsync)
            .WithName("EvaluateTransaction")
            .WithSummary("Assess one transaction for fraud.")
            .ProducesValidationProblem();

        return routes;
    }

    private static async Task<Results<Ok<FraudAssessmentResponse>, ValidationProblem>> EvaluateAsync(
        EvaluateTransactionRequest request,
        EvaluateTransactionHandler handler,
        CancellationToken cancellationToken)
    {
        if (!TransactionRequestMapper.TryMap(request, out var transaction, out var errors))
        {
            // 400 with every problem listed, rather than failing on the first. Fixing a payload one
            // field per round trip is a poor experience, and worse for a batch.
            return TypedResults.ValidationProblem(
                errors.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
                title: "The transaction could not be accepted.");
        }

        var assessment = await handler.HandleAsync(transaction, cancellationToken).ConfigureAwait(false);

        // 200 rather than 201. Nothing addressable is created from the caller's point of view: they get
        // the verdict in the response, and asking them to follow a Location header to read it back would
        // be ceremony for its own sake.
        return TypedResults.Ok(FraudAssessmentResponse.From(assessment));
    }
}
