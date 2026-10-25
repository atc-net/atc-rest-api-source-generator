var builder = WebApplication.CreateSlimBuilder(args);

// Increase request body size limit for image uploads (base64 encoded in JSON)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
});

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
            .WithExposedHeaders("Retry-After", "Content-Disposition", "ETag", "X-Stream-Format") // The 429 back-off, a download's file name, a summary's version, a stream's format
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
// OPENTELEMETRY - Distributed Tracing
// ============================================
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Showcase.Handlers.*")
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());

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

// ============================================
// HEALTH CHECKS - Generated from marker file
// ============================================
builder.Services.AddApiHealthChecks();

// Register Showcase dependencies (repositories)
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<AccountInMemoryRepository>();
builder.Services.AddSingleton<DocumentInMemoryRepository>();
builder.Services.AddSingleton<FileInMemoryRepository>();
builder.Services.AddSingleton<SubscriptionInMemoryRepository>();
builder.Services.AddSingleton<TaskInMemoryRepository>();
builder.Services.AddSingleton<UserInMemoryRepository>();

builder.Services.AddSingleton<Showcase.Api.Domain.ApiHandlers.Streams.LivePriceFeed>();

var app = builder.Build();

// Enable CORS
app.UseCors();

// ============================================
// AUDIT - what the specification marks
// ============================================
// An operation's own x- extensions reach its endpoint as ApiOperationExtension metadata: the specification marks the
// operations to audit with x-audit: true. ApiOperations names an operation in code, so renaming it in the
// specification breaks the build here instead of silently changing what is logged.
app.Use(async (context, next) =>
{
    await next(context);

    var endpoint = context.GetEndpoint();
    if (Showcase.Generated.ApiOperationExtension.Find(endpoint, "x-audit") == "true" &&
        endpoint?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName is { } operation)
    {
        var level = operation == Showcase.Generated.ApiOperations.DeleteAccountById
            ? LogLevel.Warning
            : LogLevel.Information;
        app.Logger.Log(level, "Audit: {Operation} answered {StatusCode}", operation, context.Response.StatusCode);
    }
});

// Map SignalR hub for real-time notifications
app.MapHub<NotificationHub>(NotificationsHubContract.Url);

// ============================================
// SHOWCASE API MIDDLEWARE AND ENDPOINTS - Simplified pipeline
// ============================================
// Configures: rate limiter, auth, error handling, and maps all endpoints
// The end-to-end tests (test/Showcase.E2ETests) drive three apps at once against this API, from one IP address and
// as the one demo identity: the per-minute limits of the specification (50 user requests, 10 uploads) would answer
// a full run with 429s. That run sets Showcase__DisableRateLimiter=true; the policies stay registered either way.
var disableRateLimiter = app.Configuration.GetValue<bool>("Showcase:DisableRateLimiter");
app.MapShowcaseApi(options => options.UseRateLimiter = !disableRateLimiter);

// Map webhook endpoints (receives external webhook calls)
app.MapShowcaseWebhooks();

// Map health check endpoints (secured via the X-Health-Api-Key header)
app.MapHealthCheckEndpoints();

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