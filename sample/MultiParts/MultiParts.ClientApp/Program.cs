using Api.Generated.Products.Client;
using Api.Generated.Products.Models;
using Api.Generated.Users.Client;
using Api.Generated.Users.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Create host builder
var builder = Host.CreateApplicationBuilder(args);

// Configure HTTP client for MultiParts API
// Use environment variable or default to localhost
var apiBaseUrl = Environment.GetEnvironmentVariable("services__multiparts-api__https__0")
    ?? Environment.GetEnvironmentVariable("services__multiparts-api__http__0")
    ?? "http://localhost:5000";

builder.Services.AddHttpClient<UsersClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddHttpClient<ProductsClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

var host = builder.Build();

// Get the clients
var usersClient = host.Services.GetRequiredService<UsersClient>();
var productsClient = host.Services.GetRequiredService<ProductsClient>();

Console.WriteLine("=== MultiParts API Demo (Multi-Part Specification) ===");
Console.WriteLine($"API Base URL: {apiBaseUrl}");
Console.WriteLine();

// Demo: Users endpoints
Console.WriteLine("--- Users ---");
try
{
    // Create a user
    var createUserParams = new CreateUserParameters(new CreateUserRequest("John Doe", "john.doe@example.com"));
    var createdUser = await usersClient.CreateUserAsync(createUserParams);
    Console.WriteLine($"Created user: {createdUser.Name} ({createdUser.Email})");

    // List users
    var users = await usersClient.ListUsersAsync();
    Console.WriteLine($"Total users: {users.Length}");

    // Get user by ID
    var getUserParams = new GetUserByIdParameters(createdUser.Id);
    var user = await usersClient.GetUserByIdAsync(getUserParams);
    Console.WriteLine($"Retrieved user: {user.Name}");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"Users API error: {ex.Message}");
}

Console.WriteLine();

// Demo: Products endpoints
Console.WriteLine("--- Products ---");
try
{
    // Create a product
    var createProductParams = new CreateProductParameters(
        new CreateProductRequest("Widget", "A useful widget", 19.99, "Electronics"));
    var createdProduct = await productsClient.CreateProductAsync(createProductParams);
    Console.WriteLine($"Created product: {createdProduct.Name} (${createdProduct.Price})");

    // List products
    var listParams = new ListProductsParameters(10, 0);
    var products = await productsClient.ListProductsAsync(listParams);
    Console.WriteLine($"Total products: {products.Length}");

    // Get product by ID
    var getProductParams = new GetProductByIdParameters(createdProduct.Id);
    var product = await productsClient.GetProductByIdAsync(getProductParams);
    Console.WriteLine($"Retrieved product: {product.Name}");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"Products API error: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("=== Demo complete ===");
