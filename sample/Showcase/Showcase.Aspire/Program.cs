var builder = DistributedApplication.CreateBuilder(args);

// Add the Showcase API project
var api = builder
    .AddProject<Projects.Showcase_Api>("api")
    .WithExternalHttpEndpoints();

// Add the Showcase Client application (console)
builder
    .AddProject<Projects.Showcase_ClientApp>("client")
    .WithReference(api);

// Add the Showcase Blazor application
builder
    .AddProject<Projects.Showcase_BlazorApp>("blazor")
    .WithReference(api)
    .WithExternalHttpEndpoints();

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);