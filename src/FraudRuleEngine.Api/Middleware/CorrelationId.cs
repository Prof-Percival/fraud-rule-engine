using System.Diagnostics;

namespace FraudRuleEngine.Api.Middleware;

/// <summary>
/// The id that ties a request's logs, trace and response header together.
/// </summary>
/// <remarks>
/// Prefers the trace id of the current activity, which is also what the traces are keyed on, so a log
/// line and its trace share one id. Falls back to the framework's request identifier when no activity is
/// running, which happens when tracing is not listening.
/// </remarks>
internal static class CorrelationId
{
    public static string For(HttpContext httpContext) =>
        Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
}
