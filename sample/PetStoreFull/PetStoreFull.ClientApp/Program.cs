#pragma warning disable CA1031, CA1303, CA2000

// Redirect Console output to also write to VS Debug Output window
Console.SetOut(new DualWriter(Console.Out));

Console.WriteLine("PetStoreFull Client Demo");
Console.WriteLine("===========================");
Console.WriteLine();

// Get API endpoint from environment variable (set by Aspire) or use default
var apiBaseUrl = Environment.GetEnvironmentVariable("services__api__http__0")
    ?? Environment.GetEnvironmentVariable("services__api__https__0")
    ?? "http://localhost:5046";

Console.WriteLine($"Connecting to API at: {apiBaseUrl}");
Console.WriteLine();

// Create HTTP client with base address
using var httpClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };

// Create typed clients for each API path segment
var petsClient = new PetsClient(httpClient);
var storesClient = new StoresClient(httpClient);
var usersClient = new UsersClient(httpClient);

try
{
    Console.WriteLine("Note: PetStoreFull uses full OpenAPI spec with different endpoints than PetStoreSimple");
    Console.WriteLine("API client is generated and ready to use!");
    Console.WriteLine("Available methods: AddPetAsync, UpdatePetAsync, FindPetsByStatusAsync, GetPetByIdAsync, etc.");
    Console.WriteLine();
    Console.WriteLine("✓ Client initialized successfully!");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"✗ HTTP Error: {ex.Message}");
    Console.WriteLine("  Make sure the API is running on http://localhost:5000");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Error: {ex.Message}");
}