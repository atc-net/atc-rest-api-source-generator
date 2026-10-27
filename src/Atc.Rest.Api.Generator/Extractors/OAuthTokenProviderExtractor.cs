namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OAuth2 configuration and generates the token provider interface and implementation.
/// The token provider handles automatic token acquisition and refresh.
/// </summary>
public static class OAuthTokenProviderExtractor
{
    /// <summary>
    /// Extracts the token response model class.
    /// </summary>
    /// <param name="projectName">The project name for namespace.</param>
    /// <returns>Generated code content for the OAuthTokenResponse class.</returns>
    public static string ExtractTokenResponse(string projectName)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"namespace {projectName}.Generated.OAuth;");
        contentBuilder.AppendLine();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public sealed class OAuthTokenResponse");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "[JsonPropertyName(\"access_token\")]");
        contentBuilder.AppendLine(4, "public string AccessToken { get; set; } = string.Empty;");
        contentBuilder.AppendLine(4, "[JsonPropertyName(\"token_type\")]");
        contentBuilder.AppendLine(4, "public string TokenType { get; set; } = \"Bearer\";");
        contentBuilder.AppendLine(4, "[JsonPropertyName(\"expires_in\")]");
        contentBuilder.AppendLine(4, "public int ExpiresIn { get; set; }");
        contentBuilder.AppendLine(4, "[JsonPropertyName(\"refresh_token\")]");
        contentBuilder.AppendLine(4, "public string? RefreshToken { get; set; }");
        contentBuilder.AppendLine(4, "[JsonPropertyName(\"scope\")]");
        contentBuilder.AppendLine(4, "public string? Scope { get; set; }");
        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, NamespaceConstants.SystemCodeDomCompiler));
        builder.AppendLine($"namespace {projectName}.Generated.OAuth;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// OAuth2 token endpoint response.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public sealed class OAuthTokenResponse");
        builder.AppendLine("{");
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Gets or sets the access token.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "[JsonPropertyName(\"access_token\")]");
        builder.AppendLine(4, "public string AccessToken { get; set; } = string.Empty;");
        builder.AppendLine();
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Gets or sets the token type (typically \"Bearer\").");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "[JsonPropertyName(\"token_type\")]");
        builder.AppendLine(4, "public string TokenType { get; set; } = \"Bearer\";");
        builder.AppendLine();
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Gets or sets the token expiration time in seconds.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "[JsonPropertyName(\"expires_in\")]");
        builder.AppendLine(4, "public int ExpiresIn { get; set; }");
        builder.AppendLine();
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Gets or sets the refresh token (optional, for Authorization Code flow).");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "[JsonPropertyName(\"refresh_token\")]");
        builder.AppendLine(4, "public string? RefreshToken { get; set; }");
        builder.AppendLine();
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Gets or sets the granted scopes (optional).");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "[JsonPropertyName(\"scope\")]");
        builder.AppendLine(4, "public string? Scope { get; set; }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Extracts the token provider interface.
    /// </summary>
    /// <param name="projectName">The project name for namespace.</param>
    /// <returns>Generated code content for the IOAuthTokenProvider interface.</returns>
    public static string ExtractInterface(string projectName)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public interface IOAuthTokenProvider");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);");
        contentBuilder.AppendLine(4, "Task<string> RefreshTokenAsync(CancellationToken cancellationToken = default);");
        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, NamespaceConstants.SystemCodeDomCompiler));
        builder.AppendLine($"namespace {projectName}.Generated.OAuth;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Interface for OAuth2 token provider.");
        builder.AppendLine("/// Provides methods to acquire and refresh access tokens.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public interface IOAuthTokenProvider");
        builder.AppendLine("{");
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Gets a valid access token, refreshing if necessary.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"cancellationToken\">Cancellation token.</param>");
        builder.AppendLine(4, "/// <returns>A valid access token.</returns>");
        builder.AppendLine(4, "Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);");
        builder.AppendLine();
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Forces a token refresh regardless of current token state.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"cancellationToken\">Cancellation token.</param>");
        builder.AppendLine(4, "/// <returns>A new access token.</returns>");
        builder.AppendLine(4, "Task<string> RefreshTokenAsync(CancellationToken cancellationToken = default);");
        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Extracts the token provider implementation.
    /// </summary>
    /// <param name="oauthConfig">The OAuth configuration.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <returns>Generated code content for the OAuthTokenProvider class.</returns>
    public static string? ExtractImplementation(
        OAuthConfig? oauthConfig,
        string projectName)
    {
        if (oauthConfig is null)
        {
            return null;
        }

        return GenerateImplementation(oauthConfig, projectName);
    }

    /// <summary>
    /// Generates the token provider implementation.
    /// </summary>
    private static string GenerateImplementation(
        OAuthConfig oauthConfig,
        string projectName)
    {
        // Generate class content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public sealed class OAuthTokenProvider : IOAuthTokenProvider, IDisposable");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "private readonly HttpClient _httpClient;");
        contentBuilder.AppendLine(4, "private readonly IOptions<OAuthClientOptions> _options;");
        contentBuilder.AppendLine(4, "private readonly SemaphoreSlim _refreshLock = new(1, 1);");
        contentBuilder.AppendLine(4, "public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)");
        contentBuilder.AppendLine(4, "public async Task<string> RefreshTokenAsync(CancellationToken cancellationToken = default)");
        contentBuilder.AppendLine(8, "var parameters = new Dictionary<string, string>");
        contentBuilder.AppendLine(8, "using var content = new FormUrlEncodedContent(parameters);");
        contentBuilder.AppendLine(8, "return await ReadTokenResponseAsync(response.Content, cancellationToken).ConfigureAwait(false);");
        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, NamespaceConstants.SystemCodeDomCompiler));
        AppendClassHeader(builder, projectName);
        AppendGetAccessTokenAsync(builder);
        AppendRefreshTokenAsync(builder, oauthConfig.SupportsRefreshTokens);
        AppendRequestTokenWithClientCredentialsAsync(builder);

        if (oauthConfig.SupportsRefreshTokens)
        {
            AppendRequestTokenWithRefreshTokenAsync(builder);
        }

        builder.AppendLine();
        AppendReadTokenResponse(builder);
        builder.AppendLine();
        builder.AppendLine(4, "/// <inheritdoc />");
        builder.AppendLine(4, "public void Dispose()");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "_refreshLock.Dispose();");
        builder.AppendLine(4, "}");
        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// The namespace, the class, its fields and its constructor.
    /// </summary>
    private static void AppendClassHeader(
        StringBuilder builder,
        string projectName)
    {
        builder.AppendLine($"namespace {projectName}.Generated.OAuth;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Thread-safe OAuth2 token provider implementation.");
        builder.AppendLine("/// Handles automatic token acquisition and refresh with caching.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public sealed class OAuthTokenProvider : IOAuthTokenProvider, IDisposable");
        builder.AppendLine("{");
        builder.AppendLine(4, "private readonly HttpClient _httpClient;");
        builder.AppendLine(4, "private readonly IOptions<OAuthClientOptions> _options;");
        builder.AppendLine(4, "private readonly SemaphoreSlim _refreshLock = new(1, 1);");
        builder.AppendLine();
        builder.AppendLine(4, "private string? _accessToken;");
        builder.AppendLine(4, "private string? _refreshToken;");
        builder.AppendLine(4, "private DateTimeOffset _expiresAt;");
        builder.AppendLine();
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Initializes a new instance of the <see cref=\"OAuthTokenProvider\"/> class.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"httpClient\">The HTTP client for token requests.</param>");
        builder.AppendLine(4, "/// <param name=\"options\">The OAuth configuration options.</param>");
        builder.AppendLine(4, "public OAuthTokenProvider(HttpClient httpClient, IOptions<OAuthClientOptions> options)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));");
        builder.AppendLine(8, "_options = options ?? throw new ArgumentNullException(nameof(options));");
        builder.AppendLine(4, "}");
        builder.AppendLine();
    }

    /// <summary>
    /// GetAccessTokenAsync: the cached token while it is valid, else a refresh.
    /// </summary>
    private static void AppendGetAccessTokenAsync(StringBuilder builder)
    {
        builder.AppendLine(4, "/// <inheritdoc />");
        builder.AppendLine(4, "public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "// Fast path: check if current token is still valid");
        builder.AppendLine(8, "if (!string.IsNullOrEmpty(_accessToken) &&");
        builder.AppendLine(12, "DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-_options.Value.RefreshBufferSeconds))");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "return _accessToken;");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "// Slow path: need to refresh token");
        builder.AppendLine(8, "return await RefreshTokenAsync(cancellationToken).ConfigureAwait(false);");
        builder.AppendLine(4, "}");
        builder.AppendLine();
    }

    /// <summary>
    /// RefreshTokenAsync: one refresh at a time - the refresh token when the flow has one, else client credentials.
    /// </summary>
    private static void AppendRefreshTokenAsync(
        StringBuilder builder,
        bool supportsRefreshTokens)
    {
        builder.AppendLine(4, "/// <inheritdoc />");
        builder.AppendLine(4, "public async Task<string> RefreshTokenAsync(CancellationToken cancellationToken = default)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);");
        builder.AppendLine(8, "try");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "// Double-check after acquiring lock");
        builder.AppendLine(12, "if (!string.IsNullOrEmpty(_accessToken) &&");
        builder.AppendLine(16, "DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-_options.Value.RefreshBufferSeconds))");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, "return _accessToken;");
        builder.AppendLine(12, "}");
        builder.AppendLine();
        builder.AppendLine(12, "var opts = _options.Value;");
        builder.AppendLine(12, "OAuthTokenResponse? tokenResponse;");
        builder.AppendLine();

        // Check if refresh token flow should be attempted
        if (supportsRefreshTokens)
        {
            builder.AppendLine(12, "// Try refresh token first if available");
            builder.AppendLine(12, "if (!string.IsNullOrEmpty(_refreshToken) && !string.IsNullOrEmpty(opts.RefreshUrl))");
            builder.AppendLine(12, "{");
            builder.AppendLine(16, "tokenResponse = await RequestTokenWithRefreshTokenAsync(opts, cancellationToken).ConfigureAwait(false);");
            builder.AppendLine(12, "}");
            builder.AppendLine(12, "else");
            builder.AppendLine(12, "{");
            builder.AppendLine(16, "// Fall back to Client Credentials");
            builder.AppendLine(16, "tokenResponse = await RequestTokenWithClientCredentialsAsync(opts, cancellationToken).ConfigureAwait(false);");
            builder.AppendLine(12, "}");
        }
        else
        {
            builder.AppendLine(12, "// Request token using Client Credentials");
            builder.AppendLine(12, "tokenResponse = await RequestTokenWithClientCredentialsAsync(opts, cancellationToken).ConfigureAwait(false);");
        }

        builder.AppendLine();
        builder.AppendLine(12, "if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.AccessToken))");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, "throw new InvalidOperationException(\"Failed to acquire OAuth2 access token.\");");
        builder.AppendLine(12, "}");
        builder.AppendLine();
        builder.AppendLine(12, "// Update cached values");
        builder.AppendLine(12, "_accessToken = tokenResponse.AccessToken;");
        builder.AppendLine(12, "_expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);");
        builder.AppendLine();
        builder.AppendLine(12, "if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, "_refreshToken = tokenResponse.RefreshToken;");
        builder.AppendLine(12, "}");
        builder.AppendLine();
        builder.AppendLine(12, "return _accessToken;");
        builder.AppendLine(8, "}");
        builder.AppendLine(8, "finally");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "_refreshLock.Release();");
        builder.AppendLine(8, "}");
        builder.AppendLine(4, "}");
        builder.AppendLine();
    }

    /// <summary>
    /// RequestTokenWithClientCredentialsAsync: the client credentials grant.
    /// </summary>
    private static void AppendRequestTokenWithClientCredentialsAsync(
        StringBuilder builder)
    {
        builder.AppendLine(4, "private async Task<OAuthTokenResponse?> RequestTokenWithClientCredentialsAsync(");
        builder.AppendLine(8, "OAuthClientOptions opts,");
        builder.AppendLine(8, "CancellationToken cancellationToken)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "if (string.IsNullOrEmpty(opts.TokenUrl))");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "throw new InvalidOperationException(\"OAuth2 TokenUrl is not configured.\");");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "var parameters = new Dictionary<string, string>");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "[\"grant_type\"] = \"client_credentials\",");
        builder.AppendLine(12, "[\"client_id\"] = opts.ClientId,");
        builder.AppendLine(12, "[\"client_secret\"] = opts.ClientSecret,");
        builder.AppendLine(8, "};");
        builder.AppendLine();
        builder.AppendLine(8, "if (!string.IsNullOrEmpty(opts.Scopes))");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "parameters[\"scope\"] = opts.Scopes;");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "using var content = new FormUrlEncodedContent(parameters);");
        builder.AppendLine(8, "using var response = await _httpClient.PostAsync(opts.TokenUrl, content, cancellationToken).ConfigureAwait(false);");
        builder.AppendLine();
        builder.AppendLine(8, "response.EnsureSuccessStatusCode();");
        builder.AppendLine();
        builder.AppendLine(8, "return await ReadTokenResponseAsync(response.Content, cancellationToken).ConfigureAwait(false);");
        builder.AppendLine(4, "}");
    }

    /// <summary>
    /// RequestTokenWithRefreshTokenAsync: the refresh token grant, falling back to client credentials.
    /// </summary>
    private static void AppendRequestTokenWithRefreshTokenAsync(
        StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine(4, "private async Task<OAuthTokenResponse?> RequestTokenWithRefreshTokenAsync(");
        builder.AppendLine(8, "OAuthClientOptions opts,");
        builder.AppendLine(8, "CancellationToken cancellationToken)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "var refreshUrl = opts.RefreshUrl ?? opts.TokenUrl;");
        builder.AppendLine(8, "if (string.IsNullOrEmpty(refreshUrl))");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "throw new InvalidOperationException(\"OAuth2 RefreshUrl is not configured.\");");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "var parameters = new Dictionary<string, string>");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "[\"grant_type\"] = \"refresh_token\",");
        builder.AppendLine(12, "[\"refresh_token\"] = _refreshToken!,");
        builder.AppendLine(12, "[\"client_id\"] = opts.ClientId,");
        builder.AppendLine(12, "[\"client_secret\"] = opts.ClientSecret,");
        builder.AppendLine(8, "};");
        builder.AppendLine();
        builder.AppendLine(8, "using var content = new FormUrlEncodedContent(parameters);");
        builder.AppendLine(8, "using var response = await _httpClient.PostAsync(refreshUrl, content, cancellationToken).ConfigureAwait(false);");
        builder.AppendLine();
        builder.AppendLine(8, "// If refresh fails, try client credentials as fallback");
        builder.AppendLine(8, "if (!response.IsSuccessStatusCode)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "_refreshToken = null;");
        builder.AppendLine(12, "return await RequestTokenWithClientCredentialsAsync(opts, cancellationToken).ConfigureAwait(false);");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "return await ReadTokenResponseAsync(response.Content, cancellationToken).ConfigureAwait(false);");
        builder.AppendLine(4, "}");
    }

    /// <summary>
    /// <c>ReadTokenResponseAsync</c>: the token response read member by member from the JSON document - no
    /// reflection-based serialization, so it is trim- and AOT-safe - as the serializer reads it with Web defaults:
    /// member names in any case, a number in a string, other members skipped; a member of the wrong type, or a body
    /// that is no JSON object, is a <c>JsonException</c>, and a <c>null</c> body no token.
    /// </summary>
    private static void AppendReadTokenResponse(StringBuilder builder)
    {
        builder.AppendLine(4, "private static async Task<OAuthTokenResponse?> ReadTokenResponseAsync(");
        builder.AppendLine(8, "HttpContent content,");
        builder.AppendLine(8, "CancellationToken cancellationToken)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);");
        builder.AppendLine(8, "using var document = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);");
        builder.AppendLine(8, "var root = document.RootElement;");
        builder.AppendLine(8, "if (root.ValueKind == System.Text.Json.JsonValueKind.Null)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "return null;");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "if (root.ValueKind != System.Text.Json.JsonValueKind.Object)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "throw new System.Text.Json.JsonException(\"The token response is no JSON object.\");");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "var token = new OAuthTokenResponse();");
        builder.AppendLine(8, "foreach (var member in root.EnumerateObject())");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "// Upper-cased as an ordinal case-insensitive comparison does");
        builder.AppendLine(12, "switch (member.Name.ToUpperInvariant())");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, "case \"ACCESS_TOKEN\":");
        builder.AppendLine(20, "token.AccessToken = Text(member.Value)!;");
        builder.AppendLine(20, "break;");
        builder.AppendLine(16, "case \"TOKEN_TYPE\":");
        builder.AppendLine(20, "token.TokenType = Text(member.Value)!;");
        builder.AppendLine(20, "break;");
        builder.AppendLine(16, "case \"EXPIRES_IN\":");
        builder.AppendLine(20, "token.ExpiresIn = Seconds(member.Value);");
        builder.AppendLine(20, "break;");
        builder.AppendLine(16, "case \"REFRESH_TOKEN\":");
        builder.AppendLine(20, "token.RefreshToken = Text(member.Value);");
        builder.AppendLine(20, "break;");
        builder.AppendLine(16, "case \"SCOPE\":");
        builder.AppendLine(20, "token.Scope = Text(member.Value);");
        builder.AppendLine(20, "break;");
        builder.AppendLine(12, "}");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "return token;");
        builder.AppendLine();
        builder.AppendLine(8, "static string? Text(System.Text.Json.JsonElement value)");
        builder.AppendLine(12, "=> value.ValueKind switch");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, "System.Text.Json.JsonValueKind.String => value.GetString(),");
        builder.AppendLine(16, "System.Text.Json.JsonValueKind.Null => null,");
        builder.AppendLine(16, "_ => throw new System.Text.Json.JsonException($\"A token response member is a {value.ValueKind}, not a string.\"),");
        builder.AppendLine(12, "};");
        builder.AppendLine();
        builder.AppendLine(8, "static int Seconds(System.Text.Json.JsonElement value)");
        builder.AppendLine(12, "=> value.ValueKind switch");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, "System.Text.Json.JsonValueKind.Number when value.TryGetInt32(out var seconds) => seconds,");
        builder.AppendLine(16, "System.Text.Json.JsonValueKind.String when int.TryParse(value.GetString(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var seconds) => seconds,");
        builder.AppendLine(16, "_ => throw new System.Text.Json.JsonException(\"The token response's expires_in is no whole number.\"),");
        builder.AppendLine(12, "};");
        builder.AppendLine(4, "}");
    }
}