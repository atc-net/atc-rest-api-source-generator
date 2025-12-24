var builder = WebApplication.CreateSlimBuilder(args);

// Configure OpenAPI document generation
builder.Services.AddOpenApi();

// Register PetStore dependencies
builder.Services.AddSingleton<PetInMemoryRepository>();
builder.Services.AddApiHandlersFromDomain();

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

// Map PetStore endpoints using generated extension method
app.MapEndpoints();

await app
    .RunAsync()
    .ConfigureAwait(false);