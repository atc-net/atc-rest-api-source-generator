namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// The credentials a generated TypeScript client sends per operation, from the specification's security requirements:
/// the <c>security</c> request option of an operation (its schemes: a bearer token or an API key), and the ApiClient
/// runtime that applies them - the access token (<c>getAccessToken</c>) for http bearer, OAuth2 and OpenID Connect, an
/// API key (<c>apiKeys</c>, by the scheme's name) in the header, query parameter or cookie the scheme declares.
/// </summary>
public static class TypeScriptSecurity
{
    /// <summary>
    /// Whether the specification says anything about security: security schemes, a document or operation security
    /// requirement, or the ATC security extensions. Without it every operation is "unspecified" and the client sends the
    /// access token when it has one (no <c>security</c> option); with it an operation without a requirement is anonymous.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns><see langword="true"/> when the specification declares security.</returns>
    public static bool DeclaresSecurity(OpenApiDocument openApiDoc)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        if (openApiDoc.HasSecuritySchemes() || openApiDoc.Security is not null)
        {
            return true;
        }

        foreach (var pathItem in openApiDoc.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
        {
            foreach (var operation in pathItem.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            {
                if (operation.Security is not null ||
                    operation.ExtractUnifiedSecurityConfiguration(pathItem, openApiDoc).Source != SecuritySource.None)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The <c>security</c> request option of an operation: <c>[]</c> for an anonymous operation (<c>security: []</c>,
    /// <c>x-authentication-required: false</c>, or no requirement in a specification that declares security), else the
    /// schemes it accepts - <c>{ type: 'bearer' }</c> for http bearer, OAuth2 and OpenID Connect,
    /// <c>{ type: 'apiKey', scheme, in, name }</c> for an API key. A scheme the client cannot send (http basic) is left
    /// out. <see langword="null"/> when the specification declares no security (the client sends the access token).
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="path">The operation's path.</param>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="declaresSecurity">Whether the specification declares security (<see cref="DeclaresSecurity"/>).</param>
    /// <returns>The TypeScript array literal, or <see langword="null"/>.</returns>
    public static string? BuildOption(
        OpenApiOperation operation,
        string path,
        OpenApiDocument openApiDoc,
        bool declaresSecurity)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(openApiDoc);

        if (!declaresSecurity ||
            openApiDoc.Paths is null ||
            !openApiDoc.Paths.TryGetValue(path, out var pathItem))
        {
            return null;
        }

        var unified = operation.ExtractUnifiedSecurityConfiguration(pathItem, openApiDoc);
        if (unified.AllowAnonymous || !unified.AuthenticationRequired)
        {
            return "[]";
        }

        // The requirements as declared: alternatives (OR) of schemes (AND). The client sends what it has for each scheme.
        var requirements = operation.Security ?? openApiDoc.Security;
        if (requirements is null)
        {
            // Authentication required by the ATC extensions alone: a bearer token
            return "[{ type: 'bearer' }]";
        }

        var schemes = new List<string>();
        foreach (var requirement in requirements)
        {
            foreach (var (reference, _) in requirement)
            {
                var name = reference.Reference?.Id ?? reference.Name ?? string.Empty;
                var scheme = openApiDoc.Components?.SecuritySchemes is { } declared && declared.TryGetValue(name, out var found)
                    ? found
                    : null;
                var literal = SchemeLiteral(name, scheme);
                if (literal is not null && !schemes.Contains(literal, StringComparer.Ordinal))
                {
                    schemes.Add(literal);
                }
            }
        }

        return "[" + string.Join(", ", schemes) + "]";
    }

    /// <summary>
    /// Emits the <c>SecurityScheme</c> type of the ApiClient.
    /// </summary>
    /// <param name="sb">The ApiClient being written.</param>
    internal static void AppendSecuritySchemeType(StringBuilder sb)
    {
        sb.AppendLine("/**");
        sb.AppendLine(" * A security scheme of an operation (the generated client passes them): a bearer token - http bearer, OAuth2, OpenID");
        sb.AppendLine(" * Connect - from getAccessToken, or an API key from apiKeys (by the scheme's name in components.securitySchemes),");
        sb.AppendLine(" * sent in the header, query parameter or cookie the scheme declares.");
        sb.AppendLine(" */");
        sb.AppendLine("export type SecurityScheme =");
        sb.AppendLine("  | { readonly type: 'bearer' }");
        sb.AppendLine("  | { readonly type: 'apiKey'; readonly scheme: string; readonly in: 'header' | 'query' | 'cookie'; readonly name: string };");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits the <c>apiKeys</c> member of <c>ApiClientOptions</c>.
    /// </summary>
    /// <param name="sb">The ApiClient being written.</param>
    internal static void AppendApiKeysOption(StringBuilder sb)
    {
        sb.AppendLine("  /** The API keys of the specification's apiKey security schemes, by scheme name: sent with the operations that require them. */");
        sb.AppendLine("  apiKeys?: Record<string, string | (() => string | undefined | Promise<string | undefined>)>;");
    }

    /// <summary>
    /// Emits the <c>security</c> member of <c>RequestOptions</c>.
    /// </summary>
    /// <param name="sb">The ApiClient being written.</param>
    internal static void AppendSecurityRequestOption(StringBuilder sb)
    {
        sb.AppendLine("  /**");
        sb.AppendLine("   * The operation's security schemes (the generated client passes them): [] for an anonymous operation, which sends");
        sb.AppendLine("   * no credentials. Without it (a specification that declares no security) the access token is sent when there is one.");
        sb.AppendLine("   */");
        sb.AppendLine("  security?: readonly SecurityScheme[];");
    }

    /// <summary>
    /// Emits <c>credentials</c>: the headers (or the query parameters) an operation's security schemes add.
    /// </summary>
    /// <param name="sb">The ApiClient being written.</param>
    internal static void AppendCredentialsMethod(StringBuilder sb)
    {
        sb.AppendLine("  /**");
        sb.AppendLine("   * The credentials of an operation's security schemes that go in the headers (the access token as a bearer token, API");
        sb.AppendLine("   * keys in a header or a cookie) or in the query. Without security schemes - a specification that declares no security -");
        sb.AppendLine("   * the access token is sent when there is one; an anonymous operation ([]) sends none. A cookie API key is a Cookie");
        sb.AppendLine("   * header, which browsers do not let a script set (there the browser sends its own cookies): Node only.");
        sb.AppendLine("   */");
        sb.AppendLine("  private async credentials(security: readonly SecurityScheme[] | undefined, place: 'headers' | 'query'): Promise<Record<string, string>> {");
        sb.AppendLine("    const values: Record<string, string> = {};");
        sb.AppendLine("    const cookies: string[] = [];");
        sb.AppendLine("    for (const scheme of security ?? [{ type: 'bearer' } as const]) {");
        sb.AppendLine("      if (scheme.type === 'bearer') {");
        sb.AppendLine("        if (place === 'headers' && this.options.getAccessToken) {");
        sb.AppendLine("          values['Authorization'] = `Bearer ${await this.options.getAccessToken()}`;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        continue;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      if ((scheme.in === 'query') !== (place === 'query')) {");
        sb.AppendLine("        continue;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      const key = this.options.apiKeys?.[scheme.scheme];");
        sb.AppendLine("      const value = typeof key === 'function' ? await key() : key;");
        sb.AppendLine("      if (value === undefined) {");
        sb.AppendLine("        continue;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      if (scheme.in === 'cookie') {");
        sb.AppendLine("        cookies.push(`${scheme.name}=${encodeURIComponent(value)}`);");
        sb.AppendLine("      } else {");
        sb.AppendLine("        values[scheme.name] = value;");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (cookies.length > 0) {");
        sb.AppendLine("      values['Cookie'] = cookies.join('; ');");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    return values;");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static string? SchemeLiteral(
        string name,
        IOpenApiSecurityScheme? scheme)
    {
        if (scheme is null)
        {
            return null;
        }

        switch (scheme.Type)
        {
            case Microsoft.OpenApi.SecuritySchemeType.Http when string.Equals(scheme.Scheme, "bearer", StringComparison.OrdinalIgnoreCase):
            case Microsoft.OpenApi.SecuritySchemeType.OAuth2:
            case Microsoft.OpenApi.SecuritySchemeType.OpenIdConnect:
                return "{ type: 'bearer' }";
            case Microsoft.OpenApi.SecuritySchemeType.ApiKey when !string.IsNullOrEmpty(scheme.Name):
                var location = scheme.In switch
                {
                    ParameterLocation.Query => "query",
                    ParameterLocation.Cookie => "cookie",
                    _ => "header",
                };
                return "{ type: 'apiKey', scheme: '" + Js(name) + "', in: '" + location + "', name: '" + Js(scheme.Name) + "' }";
            default:
                // http basic (and other http schemes): the client has no credentials for it
                return null;
        }
    }

    private static string Js(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
}