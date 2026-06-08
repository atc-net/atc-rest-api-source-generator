namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Helpers for mapping OpenAPI HTTP methods onto ASP.NET Core minimal-API route
/// registration calls.
/// </summary>
/// <remarks>
/// ASP.NET Core ships <c>MapGet</c>/<c>MapPost</c>/<c>MapPut</c>/<c>MapDelete</c>/<c>MapPatch</c>
/// extensions for the standard verbs, but has no <c>MapQuery</c>/<c>MapLink</c>/etc.
/// OpenAPI 3.2 adds the <c>query</c> method and <c>additionalOperations</c> (custom
/// verbs such as <c>LINK</c>); those must be registered with
/// <c>MapMethods(pattern, methods, handler)</c>.
/// </remarks>
public static class EndpointMapHelper
{
    /// <summary>
    /// The HTTP verbs that have a dedicated <c>Map{Verb}</c> minimal-API extension.
    /// </summary>
    private static readonly HashSet<string> StandardMappableMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET",
        "POST",
        "PUT",
        "DELETE",
        "PATCH",
    };

    /// <summary>
    /// Determines whether the given HTTP method has a dedicated <c>Map{Verb}</c>
    /// minimal-API extension (GET/POST/PUT/DELETE/PATCH).
    /// </summary>
    /// <param name="httpMethod">The HTTP method (case-insensitive).</param>
    /// <returns><see langword="true"/> for standard verbs; otherwise <see langword="false"/>.</returns>
    public static bool IsStandardMappableMethod(string httpMethod)
        => httpMethod != null && StandardMappableMethods.Contains(httpMethod);

    /// <summary>
    /// Builds the minimal-API route registration call for a single-line endpoint
    /// mapping (handler passed as a method-group reference).
    /// Standard verbs use <c>Map{Verb}</c>; non-standard verbs (e.g. <c>query</c>,
    /// custom <c>additionalOperations</c>) use <c>MapMethods</c> with the verb as a
    /// literal.
    /// </summary>
    /// <param name="httpMethod">The HTTP method (e.g. <c>GET</c>, <c>query</c>, <c>LINK</c>).</param>
    /// <param name="route">The route pattern.</param>
    /// <param name="handlerExpression">The handler expression (method group or delegate).</param>
    /// <returns>The map call fragment, e.g. <c>MapGet("/pets", ListPets)</c> or
    /// <c>MapMethods("/pets", new[] { "QUERY" }, QueryPets)</c>.</returns>
    public static string BuildSingleLineMapCall(
        string httpMethod,
        string route,
        string handlerExpression)
    {
        if (IsStandardMappableMethod(httpMethod))
        {
            return $"Map{ToPascalCase(httpMethod)}(\"{route}\", {handlerExpression})";
        }

        return $"MapMethods(\"{route}\", new[] {{ \"{httpMethod.ToUpperInvariant()}\" }}, {handlerExpression})";
    }

    /// <summary>
    /// The RFC-standard HTTP verbs exposed as static <see cref="System.Net.Http.HttpMethod"/>
    /// properties (including <c>Query</c>, added in .NET 9, and <c>Connect</c>).
    /// </summary>
    private static readonly HashSet<string> HttpMethodStaticProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET",
        "POST",
        "PUT",
        "DELETE",
        "PATCH",
        "HEAD",
        "OPTIONS",
        "TRACE",
        "CONNECT",
        "QUERY",
    };

    /// <summary>
    /// Builds a C# expression that yields a <see cref="System.Net.Http.HttpMethod"/>
    /// for the given HTTP verb. RFC-standard verbs use the corresponding static
    /// property (e.g. <c>HttpMethod.Get</c>); custom verbs from OpenAPI 3.2
    /// <c>additionalOperations</c> (e.g. <c>LINK</c>) are constructed via
    /// <c>new HttpMethod("VERB")</c>.
    /// </summary>
    /// <param name="httpMethod">The HTTP method (case-insensitive).</param>
    /// <returns>The HttpMethod expression.</returns>
    public static string BuildHttpMethodExpression(string httpMethod)
        => HttpMethodStaticProperties.Contains(httpMethod)
            ? $"HttpMethod.{ToPascalCase(httpMethod)}"
            : $"new HttpMethod(\"{httpMethod.ToUpperInvariant()}\")";

    private static string ToPascalCase(string httpMethod)
        => char.ToUpperInvariant(httpMethod[0]) + httpMethod.Substring(1).ToLowerInvariant();
}