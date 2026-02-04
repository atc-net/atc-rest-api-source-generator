var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient with API base address
// For Aspire: uses services__api__https__0 or services__api__http__0 environment variable
// For standalone: uses ApiBaseAddress from appsettings.json or falls back to default
var apiBaseAddress = builder.Configuration["services:api:https:0"]
    ?? builder.Configuration["services:api:http:0"]
    ?? builder.Configuration["ApiBaseAddress"]
    ?? "http://localhost:15046";

// Demo JWT token (validation is disabled in API's Program.cs for demo purposes)
// This is a valid JWT structure but with fake claims - works because validation is disabled
const string demoToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJkZW1vLXVzZXIiLCJuYW1lIjoiRGVtbyBVc2VyIiwiaWF0IjoxNzE2MjM5MDIyfQ.demo-signature";

// Register HttpClient factory with named client for the API
// Resilience is configured via generated AddApiResilience() extension
builder.Services
    .AddHttpClient("Showcase-ApiClient", client =>
    {
        client.BaseAddress = new Uri(apiBaseAddress);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", demoToken);
    })
    .AddApiResilience(ResiliencePolicies.Standard);

// Register a separate HttpClient for testing WITHOUT resilience (no retries)
// This allows testing exception handling without retry interference
builder.Services
    .AddHttpClient("Showcase-Testing", client =>
    {
        client.BaseAddress = new Uri(apiBaseAddress);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", demoToken);
    });

// Register all generated endpoints (includes IHttpMessageFactory registration)
builder.Services.AddShowcaseEndpoints();

// Register MudBlazor services
builder.Services.AddMudServices();

// Register Gateway service
builder.Services.AddScoped<GatewayService>();

// Register Notification Hub service for real-time updates
// Uses the same apiBaseAddress for SignalR hub connection
builder.Configuration["ApiBaseUrl"] = apiBaseAddress;
builder.Services.AddScoped<NotificationHubService>();

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);