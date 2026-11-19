var builder = DistributedApplication.CreateBuilder(args);

// Add the PetStoreFull API project
var api = builder
    .AddDotnetProject("api", "../PetStoreFull.Api/PetStoreFull.Api.csproj");

// Add the PetStoreFull Client application
// Pass the actual endpoint reference so it gets the runtime port
// WaitFor ensures the API is running before the client starts
builder
    .AddDotnetProject("client", "../PetStoreFull.ClientApp/PetStoreFull.ClientApp.csproj")
    .WithReference(api.GetEndpoint("http"))
    .WaitFor(api);

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);