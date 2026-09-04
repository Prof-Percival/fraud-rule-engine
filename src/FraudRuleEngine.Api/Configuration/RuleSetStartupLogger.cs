using System.Text.Json;
using System.Text.Json.Serialization;
using FraudRuleEngine.Api.Middleware;
using FraudRuleEngine.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Records the effective rule set and its version once at startup, so an assessment's version can later
/// be traced back to the numbers it was scored under.
/// </summary>
internal sealed class RuleSetStartupLogger : IHostedService
{
    // Enums by name, so the record reads as Gambling rather than 11 and survives the enum being reordered.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IOptions<FraudRuleSetOptions> _options;
    private readonly IRuleSetVersionProvider _version;
    private readonly ILogger<RuleSetStartupLogger> _logger;

    public RuleSetStartupLogger(
        IOptions<FraudRuleSetOptions> options,
        IRuleSetVersionProvider version,
        ILogger<RuleSetStartupLogger> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _version = version;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var configuration = JsonSerializer.Serialize(_options.Value, SerializerOptions);
        Log.RuleSetInEffect(_logger, _version.Current.Value, configuration);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
