using FraudRuleEngine.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Validate the container while building it rather than on first request. A missing or uninstantiable
// registration then fails the process at startup, where a deployment notices, instead of surfacing as
// a 500 the first time a particular endpoint is hit.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddOpenApi();
builder.Services.AddFraudRuleEngine();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();
