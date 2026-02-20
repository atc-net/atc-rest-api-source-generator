// ReSharper disable StringLiteralTypo
// ReSharper disable ConvertIfStatementToReturnStatement
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
        ["Dictionary<"] = NamespaceConstants.SystemCollectionsGeneric,
        ["List<"] = NamespaceConstants.SystemCollectionsGeneric,
        ["IEnumerable<"] = NamespaceConstants.SystemCollectionsGeneric,
        ["IAsyncEnumerable<"] = NamespaceConstants.SystemCollectionsGeneric,
        ["IList<"] = NamespaceConstants.SystemCollectionsGeneric,
        ["ICollection<"] = NamespaceConstants.SystemCollectionsGeneric,
        ["IReadOnlyList<"] = NamespaceConstants.SystemCollectionsGeneric,
        ["IReadOnlyDictionary<"] = NamespaceConstants.SystemCollectionsGeneric,
        ["HashSet<"] = NamespaceConstants.SystemCollectionsGeneric,
        ["KeyValuePair<"] = NamespaceConstants.SystemCollectionsGeneric,

        // System.IO
        ["Stream "] = NamespaceConstants.SystemIO,
        ["Stream?"] = NamespaceConstants.SystemIO,
        ["Stream)"] = NamespaceConstants.SystemIO,
        ["MemoryStream"] = NamespaceConstants.SystemIO,

        // System.Net
        ["HttpStatusCode"] = NamespaceConstants.SystemNet,

        // System.Net.Http
        ["HttpClient"] = NamespaceConstants.SystemNetHttp,
        ["HttpMethod"] = NamespaceConstants.SystemNetHttp,
        ["HttpRequestMessage"] = NamespaceConstants.SystemNetHttp,
        ["HttpResponseMessage"] = NamespaceConstants.SystemNetHttp,
        ["StreamContent"] = NamespaceConstants.SystemNetHttp,
        ["StringContent"] = NamespaceConstants.SystemNetHttp,
        ["ByteArrayContent"] = NamespaceConstants.SystemNetHttp,
        ["FormUrlEncodedContent"] = NamespaceConstants.SystemNetHttp,
        ["MultipartFormDataContent"] = NamespaceConstants.SystemNetHttp,

        // System.Net.Http.Headers
        ["AuthenticationHeaderValue"] = NamespaceConstants.SystemNetHttpHeaders,
        ["MediaTypeHeaderValue"] = NamespaceConstants.SystemNetHttpHeaders,

        // System.Net.Http.Json
        ["ReadFromJsonAsync"] = NamespaceConstants.SystemNetHttpJson,
        ["PostAsJsonAsync"] = NamespaceConstants.SystemNetHttpJson,
        ["PutAsJsonAsync"] = NamespaceConstants.SystemNetHttpJson,
        ["GetFromJsonAsync"] = NamespaceConstants.SystemNetHttpJson,

        // System.Runtime.CompilerServices
        ["[EnumeratorCancellation]"] = "System.Runtime.CompilerServices",

        // System.Runtime.Serialization
        ["[EnumMember("] = NamespaceConstants.SystemRuntimeSerialization,

        // System.Text.Json
        ["JsonSerializer"] = NamespaceConstants.SystemTextJson,
        ["JsonSerializerOptions"] = NamespaceConstants.SystemTextJson,

        // System.Text.Json.Serialization
        ["[JsonPropertyName("] = NamespaceConstants.SystemTextJsonSerialization,
        ["[JsonConverter("] = NamespaceConstants.SystemTextJsonSerialization,
        ["[JsonPolymorphic"] = NamespaceConstants.SystemTextJsonSerialization,
        ["[JsonDerivedType("] = NamespaceConstants.SystemTextJsonSerialization,
        ["JsonStringEnumConverter"] = NamespaceConstants.SystemTextJsonSerialization,

        // System.Threading
        ["CancellationToken"] = NamespaceConstants.SystemThreading,
        ["SemaphoreSlim"] = NamespaceConstants.SystemThreading,

        // System.Threading.Tasks
        ["Task<"] = NamespaceConstants.SystemThreadingTasks,
        ["Task "] = NamespaceConstants.SystemThreadingTasks,
        ["ValueTask<"] = NamespaceConstants.SystemThreadingTasks,
        ["ValueTask "] = NamespaceConstants.SystemThreadingTasks,

        // System.Threading.RateLimiting
        ["QueueProcessingOrder"] = "System.Threading.RateLimiting",

        // System.ComponentModel.DataAnnotations
        ["[Required]"] = NamespaceConstants.SystemComponentModelDataAnnotations,
        ["[Range("] = NamespaceConstants.SystemComponentModelDataAnnotations,
        ["[MinLength("] = NamespaceConstants.SystemComponentModelDataAnnotations,
        ["[MaxLength("] = NamespaceConstants.SystemComponentModelDataAnnotations,
        ["[StringLength("] = NamespaceConstants.SystemComponentModelDataAnnotations,
        ["[RegularExpression("] = NamespaceConstants.SystemComponentModelDataAnnotations,
        ["[EmailAddress]"] = NamespaceConstants.SystemComponentModelDataAnnotations,
        ["[Url]"] = NamespaceConstants.SystemComponentModelDataAnnotations,

        // System.ComponentModel
        ["[DefaultValue("] = NamespaceConstants.SystemComponentModel,

        // Microsoft.AspNetCore.Http
        ["IFormFile"] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        ["IFormFileCollection"] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        ["StatusCodes."] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        ["HttpContext"] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        ["RequestDelegate"] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        [": IResult"] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        ["Results.Ok("] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        ["Results.Created("] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        ["Results.NotFound("] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        ["Results.NoContent("] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        ["Results.BadRequest("] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        ["Results.Unauthorized("] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        ["Results.Forbid("] = NamespaceConstants.MicrosoftAspNetCoreHttp,
        ["TypedResults."] = NamespaceConstants.MicrosoftAspNetCoreHttp,

        // Microsoft.AspNetCore.Mvc
        ["[FromQuery("] = NamespaceConstants.MicrosoftAspNetCoreMvc,
        ["[FromRoute("] = NamespaceConstants.MicrosoftAspNetCoreMvc,
        ["[FromBody]"] = NamespaceConstants.MicrosoftAspNetCoreMvc,
        ["[FromHeader("] = NamespaceConstants.MicrosoftAspNetCoreMvc,
        ["[FromServices]"] = NamespaceConstants.MicrosoftAspNetCoreMvc,
        ["[AsParameters]"] = NamespaceConstants.MicrosoftAspNetCoreMvc,

        // Microsoft.AspNetCore.Builder
        ["WebApplication"] = NamespaceConstants.MicrosoftAspNetCoreBuilder,
        ["MapGroup("] = NamespaceConstants.MicrosoftAspNetCoreBuilder,
        ["MapGet("] = NamespaceConstants.MicrosoftAspNetCoreBuilder,
        ["MapPost("] = NamespaceConstants.MicrosoftAspNetCoreBuilder,
        ["MapPut("] = NamespaceConstants.MicrosoftAspNetCoreBuilder,
        ["MapDelete("] = NamespaceConstants.MicrosoftAspNetCoreBuilder,
        ["MapPatch("] = NamespaceConstants.MicrosoftAspNetCoreBuilder,

        // Microsoft.AspNetCore.Routing
        ["RouteGroupBuilder"] = NamespaceConstants.MicrosoftAspNetCoreRouting,

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
        ["IServiceCollection"] = NamespaceConstants.MicrosoftExtensionsDependencyInjection,
        ["AddScoped<"] = NamespaceConstants.MicrosoftExtensionsDependencyInjection,
        ["AddSingleton<"] = NamespaceConstants.MicrosoftExtensionsDependencyInjection,
        ["AddTransient<"] = NamespaceConstants.MicrosoftExtensionsDependencyInjection,
        ["AddHttpClient<"] = NamespaceConstants.MicrosoftExtensionsDependencyInjection,
        ["AddHttpClient("] = NamespaceConstants.MicrosoftExtensionsDependencyInjection,
        ["AddOptions<"] = NamespaceConstants.MicrosoftExtensionsDependencyInjection,

        // Microsoft.Extensions.Http
        ["IHttpClientFactory"] = "Microsoft.Extensions.Http",
        ["IHttpClientBuilder"] = NamespaceConstants.MicrosoftExtensionsDependencyInjection,

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
        ["IFileContent"] = "Atc.Rest.Client",
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

        foreach (var kvp in TypeMappings.Where(kvp => ContentContains(content, kvp.Key)))
        {
            usings.Add(kvp.Value);
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
        foreach (var ns in usings
                     .OrderBy(GetSortOrder)
                     .ThenBy(u => u, StringComparer.Ordinal))
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

    /// <summary>
    /// Sorts global using directives with System namespaces first, then groups by namespace prefix
    /// with empty lines between groups.
    /// </summary>
    /// <param name="globalUsings">The collection of global using directives (e.g., "global using System;").</param>
    /// <param name="removeNamespaceGroupSeparator">If true, removes blank lines between namespace groups. Default: false.</param>
    /// <returns>A sorted string with global usings grouped by namespace prefix.</returns>
    public static string SortGlobalUsings(
        IEnumerable<string> globalUsings,
        bool removeNamespaceGroupSeparator = false)
    {
        // Extract namespaces from "global using X;" statements
        var namespaces = globalUsings
            .Select(ExtractNamespaceFromGlobalUsing)
            .ToList();

        if (namespaces.Count == 0)
        {
            return string.Empty;
        }

        // Delegate to GlobalUsingsHelper for consistent sorting and grouping
        return GlobalUsingsHelper.GenerateContent(
            namespaces,
            setSystemFirst: true,
            addNamespaceSeparator: !removeNamespaceGroupSeparator);
    }

    /// <summary>
    /// Extracts the namespace from a global using directive.
    /// </summary>
    /// <param name="usingDirective">The global using directive (e.g., "global using System;").</param>
    /// <returns>The extracted namespace (e.g., "System").</returns>
    public static string ExtractNamespaceFromGlobalUsing(string usingDirective)
    {
        const string prefix = "global using ";
        if (!usingDirective.StartsWith(prefix, StringComparison.Ordinal))
        {
            return usingDirective;
        }

        var ns = usingDirective.Substring(prefix.Length);
        return ns.EndsWith(";", StringComparison.Ordinal)
            ? ns.Substring(0, ns.Length - 1)
            : ns;
    }

    /// <summary>
    /// Gets the first segment of a namespace for grouping purposes.
    /// </summary>
    /// <param name="ns">The namespace to extract the prefix from.</param>
    /// <returns>The first segment of the namespace (e.g., "System" from "System.Threading").</returns>
    public static string GetNamespacePrefix(string ns)
    {
        var dotIndex = ns.IndexOf('.');
        return dotIndex > 0 ? ns.Substring(0, dotIndex) : ns;
    }
}