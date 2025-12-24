var builder = DistributedApplication.CreateBuilder(args);

// Add the PetStoreSimple API project
var api = builder
    .AddProject<Projects.PetStoreSimple_Api>("api")
    .WithExternalHttpEndpoints();

// Add the PetStoreSimple Client application
// The client will reference the API to get its endpoint
builder
    .AddProject<Projects.PetStoreSimple_ClientApp>("client")
    .WithReference(api);

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);