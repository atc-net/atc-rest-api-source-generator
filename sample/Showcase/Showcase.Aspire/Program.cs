var builder = DistributedApplication.CreateBuilder(args);

// Add the Showcase API project
// Let Aspire manage endpoints dynamically
var api = builder
    .AddProject<Projects.Showcase_Api>("api");

// Add the Showcase Client application (console)
// WaitFor ensures the API is running before the client starts
// Pass the actual endpoint reference
builder
    .AddProject<Projects.Showcase_ClientApp>("client")
    .WithReference(api.GetEndpoint("http"))
    .WaitFor(api);

// Add the Showcase Blazor application
// Pass the actual endpoint reference so it gets the runtime port
builder
    .AddProject<Projects.Showcase_BlazorApp>("blazor")
    .WithReference(api.GetEndpoint("http"))
    .WaitFor(api)
    .WithExternalHttpEndpoints();

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);