using System.Globalization;
using FraudRuleEngine.Api.Configuration;
using FraudRuleEngine.Api.Endpoints;
using FraudRuleEngine.Api.Middleware;
using FraudRuleEngine.Infrastructure;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

// A bootstrap logger so anything thrown before the host is built is still recorded, rather than the
// process dying with only a stack trace on stderr.
Serilog.Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Machine readable JSON in production, so a log pipeline can parse it, and readable lines in
    // development. Levels can be overridden through a Serilog section in configuration.
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

    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<DomainExceptionHandler>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddFraudRuleEngine(builder.Configuration);
    builder.Services.AddFraudEnginePersistence(connectionString);

    var app = builder.Build();

    // One log line per request, carrying the correlation id, rather than the framework's several. The
    // request body is never logged, so transaction identifiers and amounts stay out of the logs.
    app.UseSerilogRequestLogging(options =>
        options.EnrichDiagnosticContext = (diagnostic, httpContext) =>
            diagnostic.Set("CorrelationId", CorrelationId.For(httpContext)));

    // Sets the correlation id on the response, so a caller can quote it and it can be found in the logs.
    app.Use(async (httpContext, next) =>
    {
        httpContext.Response.Headers["X-Correlation-Id"] = CorrelationId.For(httpContext);
        await next().ConfigureAwait(false);
    });

    // Before the endpoints, so anything they throw is turned into a problem response rather than an empty
    // 500 with the detail only in the logs.
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();

        // Development only. Published API documentation hands out the shape of every route and payload.
        app.MapScalarApiReference(options => options
            .WithTitle("Fraud rule engine")
            .WithTheme(ScalarTheme.BluePlanet)
            .AddDocument("v1"));

        // Development only. An application that migrates on boot fights itself once it runs more than one
        // replica, so production applies migrations as a separate step.
        await app.Services.ApplyMigrationsAsync();
    }

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
