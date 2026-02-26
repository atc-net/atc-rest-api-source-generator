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

// Add the React application (Vite dev server)
// AddViteApp handles Vite lifecycle (port injection, dev-server readiness detection)
// VITE_API_BASE_URL is exposed to client code via import.meta.env
builder
    .AddViteApp("react-app", "../Showcase.ReactApp")
    .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))
    .WaitFor(api)
    .WithExternalHttpEndpoints();

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);