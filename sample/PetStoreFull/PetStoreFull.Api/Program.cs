var builder = WebApplication.CreateSlimBuilder(args);

// Configure OpenAPI document generation
builder.Services.AddOpenApi();

// RFC 9457 ProblemDetails for every error
builder.Services.AddProblemDetails();

// ============================================
// AUTHENTICATION - JWT Bearer + API key (Demo Mode)
// ============================================
// WARNING: This accepts ANY bearer token and ANY api_key header - for demo purposes only!
// See ServiceCollectionExtensions.cs for implementation details.
builder.Services.AddAuthenticationForPetStoreFullDemo();

// Register PetStore dependencies
builder.Services.AddSingleton<PetStoreInMemoryRepository>();
builder.Services.AddApiHandlersFromDomain();

var app = builder.Build();

// Outside Development an unhandled exception is a 500 ProblemDetails, not an empty 500 (Development keeps the developer exception page)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

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

// Map PetStore endpoints using generated extension method
app.MapEndpoints();

await app
    .RunAsync()
    .ConfigureAwait(false);