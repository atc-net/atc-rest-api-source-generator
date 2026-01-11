namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenID Connect configuration and generates DI extension methods for server-side authentication.
/// </summary>
public static class OpenIdConnectDependencyInjectionExtractor
{
    /// <summary>
    /// Extracts the OpenID Connect DI extension methods.
    /// </summary>
    /// <param name="oidcConfig">The OpenID Connect configuration.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <returns>Generated code content for the OpenID Connect service collection extensions, or null if not configured.</returns>
    public static string? Extract(
        OpenIdConnectConfig? oidcConfig,
        string projectName)
        => oidcConfig == null
            ? null
            : GenerateFileContent(oidcConfig, projectName);

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        OpenIdConnectConfig oidcConfig,
        string projectName)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public static class OpenIdConnectAuthenticationExtensions");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "public static IServiceCollection AddOpenIdConnectAuthentication(");
        contentBuilder.AppendLine(8, "this IServiceCollection services,");
        contentBuilder.AppendLine(8, "IConfiguration configuration)");
        contentBuilder.AppendLine(8, "services.AddAuthentication(options =>");
        contentBuilder.AppendLine(12, "options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;");
        contentBuilder.AppendLine(12, "options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;");
        contentBuilder.AppendLine(8, ".AddCookie()");
        contentBuilder.AppendLine(8, ".AddOpenIdConnect(options =>");
        contentBuilder.AppendLine(12, "?? throw new InvalidOperationException(\"Config is required\");");
        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, "System.CodeDom.Compiler"));
        builder.AppendLine($"namespace {projectName}.Generated.Authentication;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Extension methods for configuring OpenID Connect authentication.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class OpenIdConnectAuthenticationExtensions");
        builder.AppendLine("{");

        // Generate Add{ProjectName}OpenIdConnectAuthentication method
        var methodName = $"Add{CasingHelper.GetLastNameSegment(projectName)}OpenIdConnectAuthentication";
        var configSection = "Authentication:OpenIdConnect";

        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, $"/// Adds OpenID Connect authentication for {projectName}.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        builder.AppendLine(4, "/// <param name=\"configuration\">The configuration instance.</param>");
        builder.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        builder.AppendLine(4, "/// <remarks>");
        builder.AppendLine(4, "/// Configure OpenID Connect settings in appsettings.json:");
        builder.AppendLine(4, "/// <code>");
        builder.AppendLine(4, "/// {");
        builder.AppendLine(4, $"///   \"{configSection}\": {{");
        builder.AppendLine(4, "///     \"Authority\": \"https://your-identity-provider\",");
        builder.AppendLine(4, "///     \"ClientId\": \"your-client-id\",");
        builder.AppendLine(4, "///     \"ClientSecret\": \"your-client-secret\"");
        builder.AppendLine(4, "///   }");
        builder.AppendLine(4, "/// }");
        builder.AppendLine(4, "/// </code>");
        builder.AppendLine(4, "/// </remarks>");
        builder.AppendLine(4, $"public static IServiceCollection {methodName}(");
        builder.AppendLine(8, "this IServiceCollection services,");
        builder.AppendLine(8, "IConfiguration configuration)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "services.AddAuthentication(options =>");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;");
        builder.AppendLine(12, "options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;");
        builder.AppendLine(8, "})");
        builder.AppendLine(8, ".AddCookie()");
        builder.AppendLine(8, ".AddOpenIdConnect(options =>");
        builder.AppendLine(8, "{");

        // Authority from OpenAPI spec or configuration override
        if (!string.IsNullOrEmpty(oidcConfig.OpenIdConnectUrl))
        {
            builder.AppendLine(12, $"// Authority from OpenAPI specification: {oidcConfig.OpenIdConnectUrl}");
            builder.AppendLine(12, $"options.Authority = configuration[\"{configSection}:Authority\"]");
            builder.AppendLine(16, $"?? \"{oidcConfig.OpenIdConnectUrl}\";");
        }
        else
        {
            builder.AppendLine(12, $"options.Authority = configuration[\"{configSection}:Authority\"]");
            builder.AppendLine(16, $"?? throw new InvalidOperationException(\"{configSection}:Authority is required\");");
        }

        builder.AppendLine();
        builder.AppendLine(12, $"options.ClientId = configuration[\"{configSection}:ClientId\"]");
        builder.AppendLine(16, $"?? throw new InvalidOperationException(\"{configSection}:ClientId is required\");");
        builder.AppendLine();
        builder.AppendLine(12, $"options.ClientSecret = configuration[\"{configSection}:ClientSecret\"];");
        builder.AppendLine();
        builder.AppendLine(12, "// Standard OpenID Connect configuration");
        builder.AppendLine(12, "options.ResponseType = \"code\";");
        builder.AppendLine(12, "options.SaveTokens = true;");
        builder.AppendLine(12, "options.GetClaimsFromUserInfoEndpoint = true;");
        builder.AppendLine();

        // Generate scopes
        builder.AppendLine(12, "// Scopes from OpenAPI security requirements");
        builder.AppendLine(12, "options.Scope.Clear();");

        // Always include openid scope (required for OpenID Connect)
        var scopes = new List<string> { "openid" };

        // Add scopes from configuration
        foreach (var scope in oidcConfig.DefaultScopes.Keys)
        {
            if (!string.Equals(scope, "openid", StringComparison.OrdinalIgnoreCase) &&
                !scopes.Contains(scope, StringComparer.Ordinal))
            {
                scopes.Add(scope);
            }
        }

        // If no custom scopes, add profile as default
        if (scopes.Count == 1)
        {
            scopes.Add("profile");
        }

        foreach (var scope in scopes)
        {
            builder.AppendLine(12, $"options.Scope.Add(\"{scope}\");");
        }

        builder.AppendLine(8, "});");
        builder.AppendLine();
        builder.AppendLine(8, "return services;");
        builder.AppendLine(4, "}");
        builder.AppendLine("}");

        return builder.ToString();
    }
}