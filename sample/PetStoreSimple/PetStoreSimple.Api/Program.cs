var builder = WebApplication.CreateSlimBuilder(args);

// Configure OpenAPI document generation
builder.Services.AddOpenApi();

// RFC 9457 ProblemDetails for every error
builder.Services.AddProblemDetails();

// Register PetStore dependencies
builder.Services.AddSingleton<PetInMemoryRepository>();
builder.Services.AddApiHandlersFromDomain();

var app = builder.Build();

// An unhandled exception is a 500 ProblemDetails (no detail outside Development), not an empty 500
app.UseExceptionHandler();

// A ProblemDetails body for error responses that have none (first in the pipeline)
app.UseStatusCodePages();

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