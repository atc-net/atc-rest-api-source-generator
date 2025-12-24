var builder = DistributedApplication.CreateBuilder(args);

// Add the PetStoreFull API project
var api = builder
    .AddProject<Projects.PetStoreFull_Api>("api")
    .WithExternalHttpEndpoints();

// Add the PetStoreFull Client application
// The client will reference the API to get its endpoint
builder
    .AddProject<Projects.PetStoreFull_ClientApp>("client")
    .WithReference(api);

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);