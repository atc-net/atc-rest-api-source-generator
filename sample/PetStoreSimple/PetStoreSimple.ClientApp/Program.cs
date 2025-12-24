#pragma warning disable CA1031, CA1303, CA2000

// Redirect Console output to also write to VS Debug Output window
Console.SetOut(new DualWriter(Console.Out));

Console.WriteLine("PetStoreSimple Client Demo (EndpointPerOperation Mode)");
Console.WriteLine("======================================================");
Console.WriteLine();

// Get API endpoint from environment variable (set by Aspire) or use default
var apiBaseUrl = Environment.GetEnvironmentVariable("services__api__http__0")
    ?? Environment.GetEnvironmentVariable("services__api__https__0")
    ?? "http://localhost:5046";

Console.WriteLine($"Connecting to API at: {apiBaseUrl}");
Console.WriteLine();

// Configure DI container with HttpClient and endpoints
var services = new ServiceCollection();

// Register Atc.Rest.Client services (HttpClient + IHttpMessageFactory)
const string httpClientName = "PetStoreSimple-ApiClient";
services.AddAtcRestClient(httpClientName, new Uri(apiBaseUrl), TimeSpan.FromSeconds(30));

// Register all Pets API endpoints
services.AddPetsEndpoints();

await using var serviceProvider = services.BuildServiceProvider();

try
{
    // 1. Create a new pet
    Console.WriteLine("1. Creating a new pet...");
    var createPetsEndpoint = serviceProvider.GetRequiredService<ICreatePetsEndpoint>();
    var createResult = await createPetsEndpoint
        .ExecuteAsync(httpClientName: httpClientName, cancellationToken: CancellationToken.None)
        .ConfigureAwait(false);

    Console.WriteLine(createResult.IsCreated
        ? "   Pet created successfully"
        : $"   Create failed with status: {createResult.StatusCode}");

    Console.WriteLine();

    // 2. Get all pets
    Console.WriteLine("2. Getting all pets...");
    var listPetsEndpoint = serviceProvider.GetRequiredService<IListPetsEndpoint>();
    var listResult = await listPetsEndpoint
        .ExecuteAsync(
            new ListPetsParameters(Limit: null),
            httpClientName,
            CancellationToken.None)
        .ConfigureAwait(false);

    if (listResult.IsOk)
    {
        var allPets = listResult.OkContent;
        Console.WriteLine("   Found pets:");
        foreach (var pet in allPets)
        {
            Console.WriteLine($"   - {pet.Name} (ID: {pet.Id}, Tag: {pet.Tag})");
        }
    }
    else
    {
        Console.WriteLine($"   List failed with status: {listResult.StatusCode}");
    }

    Console.WriteLine();

    // 3. Get pet by ID
    Console.WriteLine("3. Getting pet by ID (1)...");
    var showPetByIdEndpoint = serviceProvider.GetRequiredService<IShowPetByIdEndpoint>();
    var showResult = await showPetByIdEndpoint
        .ExecuteAsync(
            new ShowPetByIdParameters(PetId: "1"),
            httpClientName,
            CancellationToken.None)
        .ConfigureAwait(false);

    if (showResult.IsOk)
    {
        var specificPet = showResult.OkContent;
        Console.WriteLine("   Pet Details:");
        Console.WriteLine($"   - ID: {specificPet.Id}");
        Console.WriteLine($"   - Name: {specificPet.Name}");
        Console.WriteLine($"   - Tag: {specificPet.Tag}");
    }
    else
    {
        Console.WriteLine($"   Show failed with status: {showResult.StatusCode}");
    }

    Console.WriteLine();

    Console.WriteLine("All API calls completed successfully!");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"HTTP Error: {ex.Message}");
    Console.WriteLine("  Make sure the API is running on http://localhost:5046");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}