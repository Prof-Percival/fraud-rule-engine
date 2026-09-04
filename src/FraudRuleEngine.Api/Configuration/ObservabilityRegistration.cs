using FraudRuleEngine.Application.Diagnostics;
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

    // Exporters are opt in: OTLP only when an endpoint is set, console only as a development switch.
    // The instrumentation runs regardless, so the data is there for anything listening.
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
                metrics.AddMeter(FraudMetrics.MeterName);

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
