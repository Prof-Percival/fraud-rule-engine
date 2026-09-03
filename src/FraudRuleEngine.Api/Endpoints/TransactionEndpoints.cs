using FraudRuleEngine.Api.Contracts;
using FraudRuleEngine.Api.Middleware;
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

        group.MapPost("/batch", EvaluateBatchAsync)
            .WithName("EvaluateTransactionBatch")
            .WithSummary("Assess a set of transactions, reporting each one separately.")
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

    private static async Task<Results<Ok<BatchEvaluateResponse>, ValidationProblem>> EvaluateBatchAsync(
        BatchEvaluateRequest request,
        EvaluateTransactionHandler handler,
        ILogger<BatchEvaluateResponse> logger,
        CancellationToken cancellationToken)
    {
        // Only the envelope is rejected outright. An empty or oversized batch is a mistake about the
        // request itself, whereas a bad transaction inside a good batch is reported per item.
        if (request.Transactions is not { Count: > 0 })
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Transactions"] = ["At least one transaction is required."],
                },
                title: "The batch could not be accepted.");
        }

        if (request.Transactions.Count > BatchEvaluateRequest.MaximumItems)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Transactions"] =
                        [$"A batch may contain at most {BatchEvaluateRequest.MaximumItems} transactions."],
                },
                title: "The batch could not be accepted.");
        }

        var results = new List<BatchItemResult>(request.Transactions.Count);

        // Sequential, not parallel. Each item is two queries and two writes against one connection, and
        // fanning out would multiply database load rather than the work being waited on. It also keeps
        // velocity correct: items in one batch can be minutes apart for the same customer, and each has
        // to see the ones before it.
        for (var index = 0; index < request.Transactions.Count; index++)
        {
            var item = request.Transactions[index];

            if (!TransactionRequestMapper.TryMap(item, out var transaction, out var errors))
            {
                results.Add(new BatchItemResult
                {
                    Index = index,
                    EventId = item.EventId,
                    Accepted = false,
                    Errors = errors,
                });

                continue;
            }

            try
            {
                var assessment = await handler.HandleAsync(transaction, cancellationToken).ConfigureAwait(false);

                results.Add(new BatchItemResult
                {
                    Index = index,
                    EventId = item.EventId,
                    Accepted = true,
                    Assessment = FraudAssessmentResponse.From(assessment),
                });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Deliberately broad, and only here. One item failing must not abandon the rest of the
                // batch, which is the whole reason a caller batches. Cancellation is excluded because
                // that means the caller has gone and continuing would be work nobody is waiting for.
                Log.BatchItemFailed(logger, index, item.EventId ?? "(none)", exception);

                results.Add(new BatchItemResult
                {
                    Index = index,
                    EventId = item.EventId,
                    Accepted = false,
                    Error = "The transaction could not be assessed.",
                });
            }
        }

        var accepted = results.Count(result => result.Accepted);

        // 200 even when some items failed. The batch itself was processed, and a 4xx or 5xx would tell
        // the caller to retry the whole thing including the parts that succeeded.
        return TypedResults.Ok(new BatchEvaluateResponse
        {
            Submitted = results.Count,
            Accepted = accepted,
            Rejected = results.Count - accepted,
            Results = results,
        });
    }
}
