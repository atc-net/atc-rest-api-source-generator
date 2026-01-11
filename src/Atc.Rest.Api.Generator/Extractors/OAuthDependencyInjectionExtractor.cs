namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OAuth2 configuration and generates DI extension methods.
/// Provides methods for registering OAuth components in the service collection.
/// </summary>
public static class OAuthDependencyInjectionExtractor
{
    /// <summary>
    /// Extracts the OAuth DI extension methods.
    /// </summary>
    /// <param name="oauthConfig">The OAuth configuration.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <returns>Generated code content for the OAuth service collection extensions, or null if no OAuth configured.</returns>
    public static string? Extract(
        OAuthConfig? oauthConfig,
        string projectName)
    {
        if (oauthConfig == null)
        {
            return null;
        }

        return GenerateFileContent(oauthConfig, projectName);
    }

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        OAuthConfig oauthConfig,
        string projectName)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public static class OAuthServiceCollectionExtensions");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "public static IServiceCollection AddOAuthAuthentication(");
        contentBuilder.AppendLine(8, "this IServiceCollection services,");
        contentBuilder.AppendLine(8, "Action<OAuthClientOptions>? configure = null)");
        contentBuilder.AppendLine(8, "var optionsBuilder = services.AddOptions<OAuthClientOptions>()");
        contentBuilder.AppendLine(12, ".BindConfiguration(\"OAuth\");");
        contentBuilder.AppendLine(8, "services.AddHttpClient<IOAuthTokenProvider, OAuthTokenProvider>(\"OAuthTokenProvider\");");
        contentBuilder.AppendLine(8, "services.AddTransient<OAuthAuthenticationHandler>();");
        contentBuilder.AppendLine(4, "public static IHttpClientBuilder AddOAuthAuthentication(");
        contentBuilder.AppendLine(8, "this IHttpClientBuilder builder)");
        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, "System.CodeDom.Compiler"));
        builder.AppendLine($"namespace {projectName}.Generated.OAuth;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Extension methods for configuring OAuth2 authentication.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class OAuthServiceCollectionExtensions");
        builder.AppendLine("{");

        // Generate Add{ProjectName}OAuthAuthentication method
        var methodName = $"Add{CasingHelper.GetLastNameSegment(projectName)}OAuthAuthentication";
        var configSection = $"OAuth:{projectName}";

        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, $"/// Adds OAuth2 authentication services for {projectName}.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        builder.AppendLine(4, "/// <param name=\"configure\">Optional configuration action to override appsettings values.</param>");
        builder.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        builder.AppendLine(4, "/// <remarks>");
        builder.AppendLine(4, "/// Configure OAuth settings in appsettings.json:");
        builder.AppendLine(4, "/// <code>");
        builder.AppendLine(4, "/// {");
        builder.AppendLine(4, $"///   \"{configSection}\": {{");
        builder.AppendLine(4, "///     \"ClientId\": \"your-client-id\",");
        builder.AppendLine(4, "///     \"ClientSecret\": \"your-client-secret\"");
        builder.AppendLine(4, "///   }");
        builder.AppendLine(4, "/// }");
        builder.AppendLine(4, "/// </code>");
        builder.AppendLine(4, "/// </remarks>");
        builder.AppendLine(4, $"public static IServiceCollection {methodName}(");
        builder.AppendLine(8, "this IServiceCollection services,");
        builder.AppendLine(8, "Action<OAuthClientOptions>? configure = null)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "// Configure options from appsettings.json");
        builder.AppendLine(8, "var optionsBuilder = services.AddOptions<OAuthClientOptions>()");
        builder.AppendLine(12, $".BindConfiguration(\"{configSection}\");");
        builder.AppendLine();
        builder.AppendLine(8, "// Apply additional configuration if provided");
        builder.AppendLine(8, "if (configure != null)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "optionsBuilder.Configure(configure);");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "// Register token provider with its own HttpClient");
        builder.AppendLine(8, "services.AddHttpClient<IOAuthTokenProvider, OAuthTokenProvider>(\"OAuthTokenProvider\");");
        builder.AppendLine();
        builder.AppendLine(8, "// Register authentication handler as transient");
        builder.AppendLine(8, "services.AddTransient<OAuthAuthenticationHandler>();");
        builder.AppendLine();
        builder.AppendLine(8, "return services;");
        builder.AppendLine(4, "}");
        builder.AppendLine();

        // Generate AddOAuthAuthentication extension method for IHttpClientBuilder
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Adds OAuth2 authentication handler to an HTTP client builder.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"builder\">The HTTP client builder.</param>");
        builder.AppendLine(4, "/// <returns>The HTTP client builder for method chaining.</returns>");
        builder.AppendLine(4, "/// <example>");
        builder.AppendLine(4, "/// <code>");
        builder.AppendLine(4, $"/// services.AddHttpClient&lt;{projectName}Client&gt;()");
        builder.AppendLine(4, "///     .AddOAuthAuthentication();");
        builder.AppendLine(4, "/// </code>");
        builder.AppendLine(4, "/// </example>");
        builder.AppendLine(4, "public static IHttpClientBuilder AddOAuthAuthentication(");
        builder.AppendLine(8, "this IHttpClientBuilder builder)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "return builder.AddHttpMessageHandler<OAuthAuthenticationHandler>();");
        builder.AppendLine(4, "}");
        builder.AppendLine("}");

        return builder.ToString();
    }
}