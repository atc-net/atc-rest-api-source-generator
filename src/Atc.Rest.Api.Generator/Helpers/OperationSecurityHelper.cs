namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The credentials a generated client sends per operation, from the specification's security requirements - one rule
/// for the C# clients and the TypeScript client.
/// </summary>
public static class OperationSecurityHelper
{
    /// <summary>
    /// Whether the specification says anything about security: security schemes, a document or operation security
    /// requirement, or the ATC security extensions. Without it every operation is "unspecified" and a client sends the
    /// access token when it has one; with it an operation without a requirement is anonymous.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns><see langword="true"/> when the specification declares security.</returns>
    public static bool DeclaresSecurity(OpenApiDocument openApiDoc)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

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
    /// The credentials an operation accepts: empty for an anonymous operation (<c>security: []</c>,
    /// <c>x-authentication-required: false</c>, or no requirement in a specification that declares security), else its
    /// schemes in the order declared, each once. A scheme a client cannot send (http basic) is left out.
    /// <see langword="null"/> when the specification declares no security (a client sends the access token).
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="path">The operation's path.</param>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="declaresSecurity">Whether the specification declares security (<see cref="DeclaresSecurity"/>).</param>
    /// <returns>The schemes, or <see langword="null"/>.</returns>
    public static IReadOnlyList<OperationSecurityScheme>? Resolve(
        OpenApiOperation operation,
        string path,
        OpenApiDocument openApiDoc,
        bool declaresSecurity)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (!declaresSecurity ||
            openApiDoc.Paths is null ||
            path is null ||
            !openApiDoc.Paths.TryGetValue(path, out var pathItem))
        {
            return null;
        }

        var unified = operation.ExtractUnifiedSecurityConfiguration(pathItem, openApiDoc);
        if (unified.AllowAnonymous || !unified.AuthenticationRequired)
        {
            return [];
        }

        // The requirements as declared: alternatives (OR) of schemes (AND). A client sends what it has for each scheme.
        var requirements = operation.Security ?? openApiDoc.Security;
        if (requirements is null)
        {
            // Authentication required by the ATC extensions alone: a bearer token
            return [new OperationSecurityScheme("bearer", IsBearer: true, In: null, ParameterName: null)];
        }

        var schemes = new List<OperationSecurityScheme>();
        foreach (var requirement in requirements)
        {
            foreach (var entry in requirement)
            {
                var reference = entry.Key;
                var name = reference.Reference?.Id ?? reference.Name ?? string.Empty;
                var scheme = openApiDoc.Components?.SecuritySchemes is { } declared && declared.TryGetValue(name, out var found)
                    ? found
                    : null;

                // One bearer token, however many schemes carry it
                if (SchemeOf(name, scheme) is { } resolved &&
                    !schemes.Exists(s => resolved.IsBearer ? s.IsBearer : s == resolved))
                {
                    schemes.Add(resolved);
                }
            }
        }

        return schemes;
    }

    private static OperationSecurityScheme? SchemeOf(
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
                return new OperationSecurityScheme(name, IsBearer: true, In: null, ParameterName: null);
            case Microsoft.OpenApi.SecuritySchemeType.ApiKey when !string.IsNullOrEmpty(scheme.Name):
                var location = scheme.In switch
                {
                    ParameterLocation.Query => "query",
                    ParameterLocation.Cookie => "cookie",
                    _ => "header",
                };
                return new OperationSecurityScheme(name, IsBearer: false, location, scheme.Name);
            default:
                // http basic (and other http schemes): a client has no credentials for it
                return null;
        }
    }
}