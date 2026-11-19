var builder = DistributedApplication.CreateBuilder(args);

// Add the PetStoreSimple API project
var api = builder
    .AddDotnetProject("api", "../PetStoreSimple.Api/PetStoreSimple.Api.csproj");

// Add the PetStoreSimple Client application
// Pass the actual endpoint reference so it gets the runtime port
// WaitFor ensures the API is running before the client starts
builder
    .AddDotnetProject("client", "../PetStoreSimple.ClientApp/PetStoreSimple.ClientApp.csproj")
    .WithReference(api.GetEndpoint("http"))
    .WaitFor(api);

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);