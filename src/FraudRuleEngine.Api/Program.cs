using System.Globalization;
using FraudRuleEngine.Api.Configuration;
using FraudRuleEngine.Api.Endpoints;
using FraudRuleEngine.Api.Middleware;
using FraudRuleEngine.Infrastructure;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

// Bootstrap logger, so a failure before the host is built is still recorded.
Serilog.Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // JSON in production for a log pipeline, readable lines in development.
    builder.Services.AddSerilog((services, logger) =>
    {
        logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning);

        if (builder.Environment.IsDevelopment())
        {
            logger.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
        }
        else
        {
            logger.WriteTo.Console(new CompactJsonFormatter());
        }
    });

    // Validate the container while building it rather than on first request. A missing or uninstantiable
    // registration then fails the process at startup, where a deployment notices, instead of surfacing as
    // a 500 the first time a particular endpoint is hit.
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateOnBuild = true;
        options.ValidateScopes = true;
    });

    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:Default is not configured. The service cannot run without a database.");

    builder.Services.AddFraudEngineTelemetry(builder.Configuration, builder.Environment);
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<DomainExceptionHandler>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddFraudRuleEngine(builder.Configuration);
    builder.Services.AddFraudEngineSecurity(builder.Configuration);
    builder.Services.AddFraudEnginePersistence(connectionString);
    builder.Services.AddHostedService<RuleSetStartupLogger>();

    var app = builder.Build();

    // One log line per request with the correlation id. The body is never logged, so identifiers and
    // amounts stay out of the logs. Health probes drop to Verbose so they do not drown the log.
    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (httpContext, _, _) =>
            httpContext.Request.Path.StartsWithSegments("/health")
                ? Serilog.Events.LogEventLevel.Verbose
                : Serilog.Events.LogEventLevel.Information;
        options.EnrichDiagnosticContext = (diagnostic, httpContext) =>
            diagnostic.Set("CorrelationId", CorrelationId.For(httpContext));
    });

    app.Use(async (httpContext, next) =>
    {
        httpContext.Response.Headers["X-Correlation-Id"] = CorrelationId.For(httpContext);
        await next().ConfigureAwait(false);
    });

    // Before the endpoints, so anything they throw is turned into a problem response rather than an empty
    // 500 with the detail only in the logs.
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();

        // Development only. Published API documentation hands out the shape of every route and payload.
        app.MapScalarApiReference(options => options
                .WithTitle("Fraud rule engine")
                .WithTheme(ScalarTheme.BluePlanet)
                .AddDocument("v1"))
            .AllowAnonymous();

        // Development only. An application that migrates on boot fights itself once it runs more than one
        // replica, so production applies migrations as a separate step.
        await app.Services.ApplyMigrationsAsync();
    }

    // Liveness answers "is the process up", so it runs no checks. Readiness runs the ready-tagged
    // checks, so an unreachable database takes the service out of rotation without killing it.
    app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") })
        .AllowAnonymous();

    app.MapTransactionEndpoints();
    app.MapAssessmentEndpoints();

    await app.RunAsync();
}
catch (Exception exception)
{
    Serilog.Log.Fatal(exception, "The service failed to start.");
    throw;
}
finally
{
    await Serilog.Log.CloseAndFlushAsync();
}
