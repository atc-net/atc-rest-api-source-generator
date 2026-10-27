namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// The exception a typed client throws for an error response: an <c>HttpRequestException</c> - so a catch of that
/// still catches it - that also carries what the message cannot: the response's headers, its body as text, and a
/// <c>Retry-After</c> as a wait.
/// </summary>
public static class ApiExceptionExtractor
{
    /// <summary>
    /// The class name of the generated exception.
    /// </summary>
    public const string ClassName = "ApiException";

    /// <summary>
    /// The class name when the specification has a schema of its own named <see cref="ClassName"/>: the exception lives
    /// in the root namespace, where it would hide that model in every generated file.
    /// </summary>
    public const string FallbackClassName = "ApiResponseException";

    /// <summary>
    /// The class name of the exception for a specification with these schema names.
    /// </summary>
    /// <param name="schemaNames">The names under <c>components/schemas</c>.</param>
    /// <returns><see cref="ClassName"/>, or <see cref="FallbackClassName"/> when a schema is named so.</returns>
    public static string ClassNameFor(IEnumerable<string>? schemaNames)
        => schemaNames is not null && schemaNames.Any(IsNamedAsTheException)
            ? FallbackClassName
            : ClassName;

    /// <summary>
    /// The source of the exception, in the given namespace.
    /// </summary>
    /// <param name="namespaceName">The namespace of the generated file.</param>
    /// <param name="className">The class name (<see cref="ClassNameFor"/>).</param>
    /// <returns>The file content.</returns>
    public static string Generate(
        string namespaceName,
        string className)
        => $$"""
             {{HeaderBuilder.WithUsings(NamespaceConstants.SystemCodeDomCompiler).TrimEnd()}}

             namespace {{namespaceName}};

             /// <summary>
             /// An error response from the API: a status outside 200-299. It is an
             /// <see cref="global::System.Net.Http.HttpRequestException"/> - a catch of that still catches it, and its
             /// <c>StatusCode</c> is the response's - that also carries the response's headers and its body.
             /// </summary>
             [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
             public sealed class {{className}} : global::System.Net.Http.HttpRequestException
             {
                 /// <summary>
                 /// Initializes a new instance of the <see cref="{{className}}"/> class.
                 /// </summary>
                 /// <param name="message">The message.</param>
                 /// <param name="statusCode">The response's status code.</param>
                 /// <param name="content">The response body as text.</param>
                 /// <param name="headers">The response's headers.</param>
                 /// <param name="retryAfter">The wait a <c>Retry-After</c> header asks for.</param>
                 public {{className}}(
                     string message,
                     global::System.Net.HttpStatusCode statusCode,
                     string content,
                     global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Collections.Generic.IEnumerable<string>> headers,
                     global::System.TimeSpan? retryAfter = null)
                     : base(message, inner: null, statusCode)
                 {
                     Content = content;
                     Headers = headers;
                     RetryAfter = retryAfter;
                 }

                 /// <summary>
                 /// The response body as text: at most its first 4,096 bytes, a longer body ends with an ellipsis.
                 /// </summary>
                 public string Content { get; }

                 /// <summary>
                 /// The response's headers, the content headers (<c>Content-Type</c>) included. Names are compared
                 /// without regard to case.
                 /// </summary>
                 public global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Collections.Generic.IEnumerable<string>> Headers { get; }

                 /// <summary>
                 /// The wait the response's <c>Retry-After</c> header asks for (a 429, a 503): the seconds it names, or the
                 /// time left until the date it names (never negative). <see langword="null"/> without the header, or
                 /// when it is neither.
                 /// </summary>
                 public global::System.TimeSpan? RetryAfter { get; }

                 /// <summary>
                 /// The exception for an error response whose body has been read.
                 /// </summary>
                 /// <param name="response">The response.</param>
                 /// <param name="content">The response body as text.</param>
                 /// <returns>The exception to throw.</returns>
                 public static {{className}} From(
                     global::System.Net.Http.HttpResponseMessage response,
                     string content)
                 {
                     var headers = new global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.IEnumerable<string>>(global::System.StringComparer.OrdinalIgnoreCase);
                     foreach (var header in response.Headers)
                     {
                         headers[header.Key] = new global::System.Collections.Generic.List<string>(header.Value);
                     }

                     foreach (var header in response.Content.Headers)
                     {
                         headers[header.Key] = new global::System.Collections.Generic.List<string>(header.Value);
                     }

                     global::System.TimeSpan? retryAfter = null;
                     if (response.Headers.RetryAfter is { } retry)
                     {
                         if (retry.Delta is { } delta)
                         {
                             retryAfter = delta;
                         }
                         else if (retry.Date is { } date)
                         {
                             var left = date - global::System.DateTimeOffset.UtcNow;
                             retryAfter = left > global::System.TimeSpan.Zero ? left : global::System.TimeSpan.Zero;
                         }
                     }

                     return new {{className}}(
                         $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {content}",
                         response.StatusCode,
                         content,
                         headers,
                         retryAfter);
                 }
             }
             """;

    // A schema whose model is generated as ApiException: the name as it is, or as its casing and separators are
    // normalised (api_exception, apiException)
    private static bool IsNamedAsTheException(string schemaName)
    {
        var letters = new StringBuilder(schemaName.Length);
        foreach (var c in schemaName)
        {
            if (char.IsLetterOrDigit(c))
            {
                letters.Append(c);
            }
        }

        return string.Equals(letters.ToString(), ClassName, StringComparison.OrdinalIgnoreCase);
    }
}