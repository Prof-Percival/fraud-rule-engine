using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Adds the health probes to the OpenAPI document.
/// </summary>
/// <remarks>
/// They are mapped as a middleware pipeline rather than a route handler, so the description layer has no
/// delegate to inspect and leaves them out. Describing them here keeps the runtime behaviour untouched.
/// </remarks>
internal sealed class HealthProbeDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Paths ??= [];

        document.Paths.Add("/health/live", Probe(
            document,
            "Whether the process is alive.",
            "Runs no checks, so a dependency being briefly away does not report the process as dead."));

        document.Paths.Add("/health/ready", Probe(
            document,
            "Whether the service can take traffic.",
            "Includes the database. Returns 503 when it cannot be reached, so the instance leaves rotation."));

        return Task.CompletedTask;
    }

    private static OpenApiPathItem Probe(OpenApiDocument document, string summary, string description) => new()
    {
        Operations = new Dictionary<HttpMethod, OpenApiOperation>
        {
            [HttpMethod.Get] = new OpenApiOperation
            {
                Tags = new HashSet<OpenApiTagReference> { new("Health", document) },
                Summary = summary,
                Description = $"{description} Unauthenticated, so a probe needs no credential.",
                Responses = new OpenApiResponses
                {
                    ["200"] = new OpenApiResponse { Description = "Healthy." },
                    ["503"] = new OpenApiResponse { Description = "Unhealthy." },
                },
            },
        },
    };
}
