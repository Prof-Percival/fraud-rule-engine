using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FraudRuleEngine.Api.Configuration;

/// <summary>
/// Wires OpenTelemetry traces and metrics.
/// </summary>
internal static class ObservabilityRegistration
{
    private const string ServiceName = "fraud-rule-engine";

    /// <remarks>
    /// Exporters are opt in. The OTLP exporter is added only when an endpoint is configured, so nothing
    /// tries to reach a collector that is not there, and the console exporter is a development switch for
    /// seeing spans and metrics without one. The instrumentation always runs, so the data is there for
    /// whatever is listening, including dotnet-counters.
    /// </remarks>
    internal static IServiceCollection AddFraudEngineTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var useConsole = environment.IsDevelopment()
            && configuration.GetValue("Telemetry:Console", false);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddSource("Npgsql");

                if (otlpEndpoint is not null)
                {
                    tracing.AddOtlpExporter();
                }

                if (useConsole)
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddRuntimeInstrumentation();
                metrics.AddMeter("Npgsql");

                if (otlpEndpoint is not null)
                {
                    metrics.AddOtlpExporter();
                }

                if (useConsole)
                {
                    metrics.AddConsoleExporter();
                }
            });

        return services;
    }
}
