var builder = WebApplication.CreateSlimBuilder(args);

// Configure OpenAPI document generation
builder.Services.AddOpenApi();

// The generated API services: RFC 9457 ProblemDetails for every error, the enum converter, ... The name comes from
// the marker's namespace, Contoso.Data.Nexus.Api.Contracts: a trailing Contracts / Api says nothing about the API
builder.Services.AddNexusApi();

// ============================================
// AUTHENTICATION - JWT Bearer (Demo Mode)
// ============================================
// WARNING: This accepts ANY bearer token - for demo purposes only!
// See ServiceCollectionExtensions.cs for implementation details.
builder.Services.AddAuthenticationForNexusDemo();

// Register Nexus dependencies
builder.Services.AddSingleton<DeviceInMemoryRepository>();
builder.Services.AddApiHandlersFromDomain();

// Register health check endpoints (generated from marker file)
builder.Services.AddApiHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Redirect root to Scalar UI
app
    .MapGet("/", () => Results.Redirect("/scalar/v1"))
    .ExcludeFromDescription();

// The generated middleware and every endpoint: the exception handler (outside Development), status code pages, and
// authentication + authorization - the specification declares its access with x-authorize-roles only, no security
// scheme, and that counts as a secured API
app.MapNexusApi();

// Map health check endpoints (/health, /health/live, /health/ready)
app.MapHealthCheckEndpoints();

await app
    .RunAsync()
    .ConfigureAwait(false);