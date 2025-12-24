// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Helper for conditionally adding using statements based on content analysis.
/// Scans generated code content for type patterns and returns only the required using statements.
/// </summary>
public static class UsingStatementHelper
{
    /// <summary>
    /// Type pattern to using namespace mappings.
    /// Key: detection pattern (for Contains check), Value: using namespace.
    /// </summary>
    private static readonly Dictionary<string, string> TypeMappings = new(StringComparer.Ordinal)
    {
        // System namespace (basic types that need explicit using)
        ["ArgumentNullException"] = "System",
        ["ArgumentException"] = "System",
        ["InvalidOperationException"] = "System",
        ["NotImplementedException"] = "System",
        ["Action<"] = "System",
        ["Func<"] = "System",

        // System.Collections.Generic
        ["Dictionary<"] = "System.Collections.Generic",
        ["List<"] = "System.Collections.Generic",
        ["IEnumerable<"] = "System.Collections.Generic",
        ["IAsyncEnumerable<"] = "System.Collections.Generic",
        ["IList<"] = "System.Collections.Generic",
        ["ICollection<"] = "System.Collections.Generic",
        ["IReadOnlyList<"] = "System.Collections.Generic",
        ["IReadOnlyDictionary<"] = "System.Collections.Generic",
        ["HashSet<"] = "System.Collections.Generic",
        ["KeyValuePair<"] = "System.Collections.Generic",

        // System.IO
        ["Stream "] = "System.IO",
        ["Stream?"] = "System.IO",
        ["Stream)"] = "System.IO",
        ["MemoryStream"] = "System.IO",

        // System.Net
        ["HttpStatusCode"] = "System.Net",

        // System.Net.Http
        ["HttpClient"] = "System.Net.Http",
        ["HttpMethod"] = "System.Net.Http",
        ["HttpRequestMessage"] = "System.Net.Http",
        ["HttpResponseMessage"] = "System.Net.Http",
        ["StreamContent"] = "System.Net.Http",
        ["StringContent"] = "System.Net.Http",
        ["ByteArrayContent"] = "System.Net.Http",
        ["FormUrlEncodedContent"] = "System.Net.Http",
        ["MultipartFormDataContent"] = "System.Net.Http",

        // System.Net.Http.Headers
        ["AuthenticationHeaderValue"] = "System.Net.Http.Headers",
        ["MediaTypeHeaderValue"] = "System.Net.Http.Headers",

        // System.Net.Http.Json
        ["ReadFromJsonAsync"] = "System.Net.Http.Json",
        ["PostAsJsonAsync"] = "System.Net.Http.Json",
        ["PutAsJsonAsync"] = "System.Net.Http.Json",
        ["GetFromJsonAsync"] = "System.Net.Http.Json",

        // System.Runtime.CompilerServices
        ["[EnumeratorCancellation]"] = "System.Runtime.CompilerServices",

        // System.Runtime.Serialization
        ["[EnumMember("] = "System.Runtime.Serialization",

        // System.Text.Json
        ["JsonSerializer"] = "System.Text.Json",
        ["JsonSerializerOptions"] = "System.Text.Json",

        // System.Text.Json.Serialization
        ["[JsonPropertyName("] = "System.Text.Json.Serialization",
        ["[JsonConverter("] = "System.Text.Json.Serialization",
        ["[JsonPolymorphic"] = "System.Text.Json.Serialization",
        ["[JsonDerivedType("] = "System.Text.Json.Serialization",
        ["JsonStringEnumConverter"] = "System.Text.Json.Serialization",

        // System.Threading
        ["CancellationToken"] = "System.Threading",
        ["SemaphoreSlim"] = "System.Threading",

        // System.Threading.Tasks
        ["Task<"] = "System.Threading.Tasks",
        ["Task "] = "System.Threading.Tasks",
        ["ValueTask<"] = "System.Threading.Tasks",
        ["ValueTask "] = "System.Threading.Tasks",

        // System.Threading.RateLimiting
        ["QueueProcessingOrder"] = "System.Threading.RateLimiting",

        // System.ComponentModel.DataAnnotations
        ["[Required]"] = "System.ComponentModel.DataAnnotations",
        ["[Range("] = "System.ComponentModel.DataAnnotations",
        ["[MinLength("] = "System.ComponentModel.DataAnnotations",
        ["[MaxLength("] = "System.ComponentModel.DataAnnotations",
        ["[StringLength("] = "System.ComponentModel.DataAnnotations",
        ["[RegularExpression("] = "System.ComponentModel.DataAnnotations",
        ["[EmailAddress]"] = "System.ComponentModel.DataAnnotations",
        ["[Url]"] = "System.ComponentModel.DataAnnotations",

        // System.ComponentModel
        ["[DefaultValue("] = "System.ComponentModel",

        // Microsoft.AspNetCore.Http
        ["IFormFile"] = "Microsoft.AspNetCore.Http",
        ["IFormFileCollection"] = "Microsoft.AspNetCore.Http",
        ["StatusCodes."] = "Microsoft.AspNetCore.Http",
        ["HttpContext"] = "Microsoft.AspNetCore.Http",
        ["RequestDelegate"] = "Microsoft.AspNetCore.Http",
        [": IResult"] = "Microsoft.AspNetCore.Http",
        ["Results.Ok("] = "Microsoft.AspNetCore.Http",
        ["Results.Created("] = "Microsoft.AspNetCore.Http",
        ["Results.NotFound("] = "Microsoft.AspNetCore.Http",
        ["Results.NoContent("] = "Microsoft.AspNetCore.Http",
        ["Results.BadRequest("] = "Microsoft.AspNetCore.Http",
        ["Results.Unauthorized("] = "Microsoft.AspNetCore.Http",
        ["Results.Forbid("] = "Microsoft.AspNetCore.Http",
        ["TypedResults."] = "Microsoft.AspNetCore.Http",

        // Microsoft.AspNetCore.Mvc
        ["[FromQuery("] = "Microsoft.AspNetCore.Mvc",
        ["[FromRoute("] = "Microsoft.AspNetCore.Mvc",
        ["[FromBody]"] = "Microsoft.AspNetCore.Mvc",
        ["[FromHeader("] = "Microsoft.AspNetCore.Mvc",
        ["[FromServices]"] = "Microsoft.AspNetCore.Mvc",
        ["[AsParameters]"] = "Microsoft.AspNetCore.Mvc",

        // Microsoft.AspNetCore.Builder
        ["WebApplication"] = "Microsoft.AspNetCore.Builder",
        ["MapGroup("] = "Microsoft.AspNetCore.Builder",
        ["MapGet("] = "Microsoft.AspNetCore.Builder",
        ["MapPost("] = "Microsoft.AspNetCore.Builder",
        ["MapPut("] = "Microsoft.AspNetCore.Builder",
        ["MapDelete("] = "Microsoft.AspNetCore.Builder",
        ["MapPatch("] = "Microsoft.AspNetCore.Builder",

        // Microsoft.AspNetCore.Routing
        ["RouteGroupBuilder"] = "Microsoft.AspNetCore.Routing",

        // Microsoft.AspNetCore.Authorization
        ["AddAuthorization("] = "Microsoft.AspNetCore.Authorization",
        ["AddPolicy("] = "Microsoft.AspNetCore.Authorization",
        ["RequireRole("] = "Microsoft.AspNetCore.Authorization",
        ["RequireClaim("] = "Microsoft.AspNetCore.Authorization",
        ["RequireAssertion("] = "Microsoft.AspNetCore.Authorization",
        ["[Authorize"] = "Microsoft.AspNetCore.Authorization",

        // Microsoft.AspNetCore.RateLimiting
        ["AddRateLimiter("] = "Microsoft.AspNetCore.RateLimiting",
        ["AddFixedWindowLimiter("] = "Microsoft.AspNetCore.RateLimiting",
        ["AddSlidingWindowLimiter("] = "Microsoft.AspNetCore.RateLimiting",
        ["AddTokenBucketLimiter("] = "Microsoft.AspNetCore.RateLimiting",
        ["AddConcurrencyLimiter("] = "Microsoft.AspNetCore.RateLimiting",

        // Microsoft.AspNetCore.Authentication.Cookies
        ["CookieAuthenticationDefaults"] = "Microsoft.AspNetCore.Authentication.Cookies",

        // Microsoft.AspNetCore.Authentication.OpenIdConnect
        ["OpenIdConnectDefaults"] = "Microsoft.AspNetCore.Authentication.OpenIdConnect",
        ["AddOpenIdConnect("] = "Microsoft.AspNetCore.Authentication.OpenIdConnect",

        // Microsoft.Extensions.Configuration
        ["IConfiguration"] = "Microsoft.Extensions.Configuration",

        // Microsoft.Extensions.DependencyInjection
        ["IServiceCollection"] = "Microsoft.Extensions.DependencyInjection",
        ["AddScoped<"] = "Microsoft.Extensions.DependencyInjection",
        ["AddSingleton<"] = "Microsoft.Extensions.DependencyInjection",
        ["AddTransient<"] = "Microsoft.Extensions.DependencyInjection",
        ["AddHttpClient<"] = "Microsoft.Extensions.DependencyInjection",
        ["AddHttpClient("] = "Microsoft.Extensions.DependencyInjection",
        ["AddOptions<"] = "Microsoft.Extensions.DependencyInjection",

        // Microsoft.Extensions.Http
        ["IHttpClientFactory"] = "Microsoft.Extensions.Http",
        ["IHttpClientBuilder"] = "Microsoft.Extensions.DependencyInjection",

        // Microsoft.Extensions.Http.Resilience
        ["AddResilienceHandler("] = "Microsoft.Extensions.Http.Resilience",
        ["HttpRetryStrategyOptions"] = "Microsoft.Extensions.Http.Resilience",
        ["HttpCircuitBreakerStrategyOptions"] = "Microsoft.Extensions.Http.Resilience",

        // Microsoft.Extensions.Logging
        ["ILogger<"] = "Microsoft.Extensions.Logging",

        // Microsoft.Extensions.Options
        ["IOptions<"] = "Microsoft.Extensions.Options",
        ["BindConfiguration("] = "Microsoft.Extensions.Options",

        // Polly
        ["PredicateBuilder<"] = "Polly",
        ["DelayBackoffType"] = "Polly",

        // Polly.Timeout
        ["TimeoutRejectedException"] = "Polly.Timeout",

        // Asp.Versioning
        ["ApiVersion"] = "Asp.Versioning",
        ["ApiVersionSet"] = "Asp.Versioning",
        ["QueryStringApiVersionReader"] = "Asp.Versioning",
        ["UrlSegmentApiVersionReader"] = "Asp.Versioning",
        ["HeaderApiVersionReader"] = "Asp.Versioning",
        ["NewApiVersionSet("] = "Asp.Versioning",

        // Atc.Rest.MinimalApi
        ["IEndpointDefinition"] = "Atc.Rest.MinimalApi.Abstractions",
        ["ValidationFilter<"] = "Atc.Rest.MinimalApi.Filters.Endpoints",
        ["UseGlobalErrorHandler("] = "Atc.Rest.MinimalApi.Extensions",
        ["GlobalErrorHandlingOptions"] = "Atc.Rest.MinimalApi.Options",

        // Atc.Rest.Client
        ["IHttpMessageFactory"] = "Atc.Rest.Client.Builder",
        ["BinaryEndpointResponse"] = "Atc.Rest.Client",
        ["EndpointResponse<"] = "Atc.Rest.Client",
        ["IEndpointResponse"] = "Atc.Rest.Client",
    };

    /// <summary>
    /// Checks if content contains a specific type pattern.
    /// Uses ordinal comparison for performance.
    /// </summary>
    /// <param name="content">The generated code content to analyze.</param>
    /// <param name="pattern">The type pattern to search for.</param>
    /// <returns>True if the content contains the pattern.</returns>
    public static bool ContentContains(
        string content,
        string pattern)
        => content.IndexOf(pattern, StringComparison.Ordinal) >= 0;

    /// <summary>
    /// Gets the required using namespaces based on content analysis.
    /// </summary>
    /// <param name="content">The generated code content to analyze.</param>
    /// <param name="alwaysInclude">Namespaces to always include regardless of content.</param>
    /// <returns>A HashSet of required using namespaces.</returns>
    public static HashSet<string> GetRequiredUsings(
        string content,
        params string[] alwaysInclude)
    {
        var usings = new HashSet<string>(alwaysInclude, StringComparer.Ordinal);

        foreach (var kvp in TypeMappings)
        {
            if (ContentContains(content, kvp.Key))
            {
                usings.Add(kvp.Value);
            }
        }

        return usings;
    }

    /// <summary>
    /// Appends sorted using statements to a StringBuilder.
    /// Sorts by: System.*, Microsoft.*, Asp.*, Atc.*, Polly.*, then custom.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="usings">The collection of using namespaces.</param>
    public static void AppendUsings(
        StringBuilder sb,
        IEnumerable<string> usings)
    {
        foreach (var ns in usings.OrderBy(GetSortOrder).ThenBy(u => u, StringComparer.Ordinal))
        {
            sb.AppendLine($"using {ns};");
        }
    }

    /// <summary>
    /// Builds a complete header with auto-generated comment, nullable enable, and using statements.
    /// </summary>
    /// <param name="content">The generated code content to analyze.</param>
    /// <param name="alwaysInclude">Namespaces to always include regardless of content.</param>
    /// <returns>The complete header string.</returns>
    public static string BuildHeader(
        string content,
        params string[] alwaysInclude)
    {
        var usings = GetRequiredUsings(content, alwaysInclude);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        AppendUsings(sb, usings);
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Gets the sort order for a namespace to ensure consistent ordering.
    /// </summary>
    /// <param name="ns">The namespace to get sort order for.</param>
    /// <returns>A sort order value.</returns>
    private static int GetSortOrder(string ns)
    {
        if (ns.StartsWith("System", StringComparison.Ordinal))
        {
            return 0;
        }

        if (ns.StartsWith("Microsoft", StringComparison.Ordinal))
        {
            return 1;
        }

        if (ns.StartsWith("Asp.", StringComparison.Ordinal))
        {
            return 2;
        }

        if (ns.StartsWith("Atc.", StringComparison.Ordinal))
        {
            return 3;
        }

        if (ns.StartsWith("Polly", StringComparison.Ordinal))
        {
            return 4;
        }

        return 5;
    }
}