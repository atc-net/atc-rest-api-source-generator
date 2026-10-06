var builder = WebApplication.CreateSlimBuilder(args);

// Configure OpenAPI document generation
builder.Services.AddOpenApi();

// RFC 9457 ProblemDetails for every error
builder.Services.AddProblemDetails();

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

// An unhandled exception is a 500 ProblemDetails (no detail outside Development), not an empty 500
app.UseExceptionHandler();

// A ProblemDetails body for error responses that have none (first in the pipeline)
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Redirect root to Scalar UI
app
    .MapGet("/", () => Results.Redirect("/scalar/v1"))
    .ExcludeFromDescription();

// Map Nexus endpoints using generated extension method
app.MapEndpoints();

// Map health check endpoints (/health, /health/live, /health/ready)
app.MapHealthCheckEndpoints();

await app
    .RunAsync()
    .ConfigureAwait(false);