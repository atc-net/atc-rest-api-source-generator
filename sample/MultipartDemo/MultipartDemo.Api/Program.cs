var builder = WebApplication.CreateSlimBuilder(args);

// Configure JSON serialization to use string enum converter
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Configure CORS for SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
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
builder.Services.AddAuthenticationForMultipartDemoDemo();

// ============================================
// API SERVICES - Simplified registration
// ============================================
builder.Services.AddMultipartDemoApi();

// Register handler implementations and validators from Domain project
builder.Services.AddApiHandlersFromDomain();
builder.Services.AddApiValidatorsFromDomain();

// Register repositories
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
// API MIDDLEWARE AND ENDPOINTS
// ============================================
app.MapMultipartDemoApi();

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