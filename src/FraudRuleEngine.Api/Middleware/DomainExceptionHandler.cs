using FraudRuleEngine.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FraudRuleEngine.Api.Middleware;

/// <summary>
/// Turns a broken domain rule into a 400 problem response, and leaves everything else alone.
/// </summary>
/// <remarks>
/// One handler rather than try/catch in every endpoint, so the mapping is a single rule instead of a
/// habit each endpoint has to remember. <see cref="DomainException"/> exists to make that rule
/// expressible: it means the caller sent something the domain refuses, which is a 400. Anything else is
/// a fault on our side, and returning it unhandled is correct, because a 500 and an alert is what should
/// happen when the cause is unknown.
/// </remarks>
internal sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<DomainExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(problemDetailsService);
        ArgumentNullException.ThrowIfNull(logger);

        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not DomainException domainException)
        {
            return false;
        }

        Log.RejectedTransaction(_logger, domainException.Message);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Title = "The transaction could not be accepted.",
                Detail = domainException.Message,
                Status = StatusCodes.Status400BadRequest,
                Type = "https://datatracker.ietf.org/doc/html/rfc9457",
            },
        }).ConfigureAwait(false);
    }
}
