using System.Diagnostics;

namespace FraudRuleEngine.Api.Middleware;

/// <summary>
/// The id tying a request's logs, trace and response header together.
/// </summary>
internal static class CorrelationId
{
    // The trace id, so a log line and its trace share one value; the request id when nothing is tracing.
    public static string For(HttpContext httpContext) =>
        Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
}
