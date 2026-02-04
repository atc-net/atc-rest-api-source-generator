var builder = DistributedApplication.CreateBuilder(args);

// Add the PetStoreFull API project
var api = builder
    .AddProject<Projects.PetStoreFull_Api>("api");

// Add the PetStoreFull Client application
// Pass the actual endpoint reference so it gets the runtime port
// WaitFor ensures the API is running before the client starts
builder
    .AddProject<Projects.PetStoreFull_ClientApp>("client")
    .WithReference(api.GetEndpoint("http"))
    .WaitFor(api);

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);