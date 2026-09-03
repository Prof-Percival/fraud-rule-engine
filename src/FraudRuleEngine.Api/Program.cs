using FraudRuleEngine.Api.Configuration;
using FraudRuleEngine.Api.Endpoints;
using FraudRuleEngine.Api.Middleware;
using FraudRuleEngine.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddFraudRuleEngine();
builder.Services.AddFraudEnginePersistence(connectionString);

var app = builder.Build();

// Before the endpoints, so anything they throw is turned into a problem response rather than an empty
// 500 with the detail only in the logs.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Development only. An application that migrates on boot fights itself once it runs more than one
    // replica, so production applies migrations as a separate step.
    await app.Services.ApplyMigrationsAsync();
}

app.MapTransactionEndpoints();

await app.RunAsync();
