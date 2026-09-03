namespace FraudRuleEngine.Api.Middleware;

/// <summary>
/// Source generated log messages.
/// </summary>
/// <remarks>
/// The generator emits a strongly typed method per message that checks whether the level is enabled
/// before touching its arguments, so nothing is allocated or formatted when the level is off. The
/// extension methods cannot do that: they box every argument into an object array at the call site
/// whether or not anything will read it.
/// </remarks>
internal static partial class Log
{
    /// <remarks>
    /// Information, not error. A rejected request is the system working as intended, and logging it at
    /// error level would bury the failures that actually need somebody.
    /// </remarks>
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Rejected a transaction that broke a domain rule: {Reason}")]
    internal static partial void RejectedTransaction(ILogger logger, string reason);

    /// <remarks>
    /// Warning rather than error: the batch as a whole succeeded and the caller was told which item
    /// failed, so this needs looking at but nothing is broken.
    /// </remarks>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Batch item {Index} ({EventId}) could not be assessed")]
    internal static partial void BatchItemFailed(
        ILogger logger,
        int index,
        string eventId,
        Exception exception);
}
