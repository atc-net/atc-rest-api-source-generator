using Api;
using Api.Generated.Endpoints;
using MultiParts.Api.Domain.Repositories;
using Scalar.AspNetCore;

var builder = WebApplication.CreateSlimBuilder(args);

// Configure OpenAPI document generation
builder.Services.AddOpenApi();

// Register in-memory repositories
builder.Services.AddSingleton<UserInMemoryRepository>();
builder.Services.AddSingleton<ProductInMemoryRepository>();

// Register API handlers from domain project
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

// Map all endpoints using generated extension method
app.MapEndpoints();

await app
    .RunAsync()
    .ConfigureAwait(false);
