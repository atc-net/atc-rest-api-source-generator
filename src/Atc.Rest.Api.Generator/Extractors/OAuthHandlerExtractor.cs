namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OAuth2 configuration and generates the authentication handler.
/// The handler is a DelegatingHandler that injects Bearer tokens into outgoing requests.
/// </summary>
public static class OAuthHandlerExtractor
{
    /// <summary>
    /// Extracts the OAuth authentication handler.
    /// </summary>
    /// <param name="projectName">The project name for namespace.</param>
    /// <returns>Generated code content for the OAuthAuthenticationHandler class.</returns>
    public static string Extract(string projectName)
    {
        // Generate class content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public sealed class OAuthAuthenticationHandler : DelegatingHandler");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "private readonly IOAuthTokenProvider _tokenProvider;");
        contentBuilder.AppendLine(4, "public OAuthAuthenticationHandler(IOAuthTokenProvider tokenProvider)");
        contentBuilder.AppendLine(8, "_tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));");
        contentBuilder.AppendLine(4, "protected override async Task<HttpResponseMessage> SendAsync(");
        contentBuilder.AppendLine(8, "HttpRequestMessage request,");
        contentBuilder.AppendLine(8, "CancellationToken cancellationToken)");
        contentBuilder.AppendLine(8, "var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);");
        contentBuilder.AppendLine(8, "request.Headers.Authorization = new AuthenticationHeaderValue(\"Bearer\", token);");
        contentBuilder.AppendLine(8, "if (response.StatusCode == HttpStatusCode.Unauthorized)");
        contentBuilder.AppendLine(4, "private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)");
        contentBuilder.AppendLine(8, "clone.Content = new ByteArrayContent(contentBytes);");
        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, NamespaceConstants.SystemCodeDomCompiler));
        builder.AppendLine($"namespace {projectName}.Generated.OAuth;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// HTTP message handler that adds OAuth2 Bearer authentication to outgoing requests.");
        builder.AppendLine("/// Automatically handles 401 responses by refreshing the token and retrying once.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public sealed class OAuthAuthenticationHandler : DelegatingHandler");
        builder.AppendLine("{");
        builder.AppendLine(4, "private readonly IOAuthTokenProvider _tokenProvider;");
        builder.AppendLine();
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Initializes a new instance of the <see cref=\"OAuthAuthenticationHandler\"/> class.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"tokenProvider\">The OAuth token provider.</param>");
        builder.AppendLine(4, "public OAuthAuthenticationHandler(IOAuthTokenProvider tokenProvider)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "_tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));");
        builder.AppendLine(4, "}");
        builder.AppendLine();
        builder.AppendLine(4, "/// <inheritdoc />");
        builder.AppendLine(4, "protected override async Task<HttpResponseMessage> SendAsync(");
        builder.AppendLine(8, "HttpRequestMessage request,");
        builder.AppendLine(8, "CancellationToken cancellationToken)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "// Get access token and set Authorization header");
        builder.AppendLine(8, "var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);");
        builder.AppendLine(8, "request.Headers.Authorization = new AuthenticationHeaderValue(\"Bearer\", token);");
        builder.AppendLine();
        builder.AppendLine(8, "// Send the request");
        builder.AppendLine(8, "var response = await base.SendAsync(request, cancellationToken);");
        builder.AppendLine();
        builder.AppendLine(8, "// Handle 401 Unauthorized - refresh token and retry once");
        builder.AppendLine(8, "if (response.StatusCode == HttpStatusCode.Unauthorized)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "// Dispose the failed response");
        builder.AppendLine(12, "response.Dispose();");
        builder.AppendLine();
        builder.AppendLine(12, "// Force token refresh");
        builder.AppendLine(12, "token = await _tokenProvider.RefreshTokenAsync(cancellationToken);");
        builder.AppendLine();
        builder.AppendLine(12, "// Clone the request (original may have been read)");
        builder.AppendLine(12, "using var retryRequest = await CloneRequestAsync(request);");
        builder.AppendLine(12, "retryRequest.Headers.Authorization = new AuthenticationHeaderValue(\"Bearer\", token);");
        builder.AppendLine();
        builder.AppendLine(12, "// Retry the request");
        builder.AppendLine(12, "response = await base.SendAsync(retryRequest, cancellationToken);");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "return response;");
        builder.AppendLine(4, "}");
        builder.AppendLine();
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Clones an HTTP request message for retry.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "var clone = new HttpRequestMessage(request.Method, request.RequestUri);");
        builder.AppendLine();
        builder.AppendLine(8, "// Copy headers");
        builder.AppendLine(8, "foreach (var header in request.Headers)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "clone.Headers.TryAddWithoutValidation(header.Key, header.Value);");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "// Copy content if present");
        builder.AppendLine(8, "if (request.Content != null)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "var contentBytes = await request.Content.ReadAsByteArrayAsync();");
        builder.AppendLine(12, "clone.Content = new ByteArrayContent(contentBytes);");
        builder.AppendLine();
        builder.AppendLine(12, "// Copy content headers");
        builder.AppendLine(12, "foreach (var header in request.Content.Headers)");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, "clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);");
        builder.AppendLine(12, "}");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine("#if NET5_0_OR_GREATER");
        builder.AppendLine(8, "// Copy options (HttpRequestOptions in .NET 5+)");
        builder.AppendLine(8, "foreach (var option in request.Options)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "clone.Options.TryAdd(option.Key, option.Value);");
        builder.AppendLine(8, "}");
        builder.AppendLine("#endif");
        builder.AppendLine();
        builder.AppendLine(8, "return clone;");
        builder.AppendLine(4, "}");
        builder.AppendLine("}");

        return builder.ToString();
    }
}