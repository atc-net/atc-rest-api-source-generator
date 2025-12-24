var builder = WebApplication.CreateSlimBuilder(args);

// Configure JSON serialization to use string enum converter
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Configure CORS for Blazor WebAssembly client and SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true) // Allow any origin for SignalR
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // Required for SignalR
    });
});

// ============================================
// SIGNALR - Real-time notifications
// ============================================
builder.Services.AddSignalR();
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddHostedService<NotificationBackgroundService>();

// Configure OpenAPI document generation
builder.Services.AddOpenApi();

// ============================================
// AUTHENTICATION - JWT Bearer (Demo Mode)
// ============================================
// WARNING: This accepts ANY bearer token - for demo purposes only!
// See ServiceCollectionExtensions.cs for implementation details.
builder.Services.AddAuthenticationForShowcaseDemo();

// ============================================
// SHOWCASE API SERVICES - Simplified registration
// ============================================
// Registers rate limiting and security policies from OpenAPI spec
builder.Services.AddShowcaseApi();

// Register handler implementations and validators from Domain project
builder.Services.AddApiHandlersFromDomain();
builder.Services.AddApiValidatorsFromDomain();
builder.Services.AddWebhookHandlersFromDomain();

// Register Showcase dependencies (repositories)
builder.Services.AddSingleton<AccountInMemoryRepository>();
builder.Services.AddSingleton<TaskInMemoryRepository>();
builder.Services.AddSingleton<FileInMemoryRepository>();
builder.Services.AddSingleton<UserInMemoryRepository>();
builder.Services.AddSingleton<SubscriptionInMemoryRepository>();

var app = builder.Build();

// Enable CORS
app.UseCors();

// Map SignalR hub for real-time notifications
app.MapHub<NotificationHub>("/hubs/notifications");

// ============================================
// SHOWCASE API MIDDLEWARE AND ENDPOINTS - Simplified pipeline
// ============================================
// Configures: rate limiter, auth, error handling, and maps all endpoints
app.MapShowcaseApi();

// Map webhook endpoints (receives external webhook calls)
app.MapShowcaseWebhooks();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Redirect root to Scalar UI
app
    .MapGet("/", () => Results.Redirect("/scalar/v1"))
    .ExcludeFromDescription();

await app
    .RunAsync()
    .ConfigureAwait(false);