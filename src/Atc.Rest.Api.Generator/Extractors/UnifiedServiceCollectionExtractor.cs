namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts parameters for generating the unified Add{ProjectName}Api() service collection extension method.
/// This provides a simplified API surface while keeping granular methods for experts.
/// </summary>
public static class UnifiedServiceCollectionExtractor
{
    /// <summary>
    /// Generates the unified service collection extension class.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document for auto-detection.</param>
    /// <param name="projectName">The project name for namespace and method naming.</param>
    /// <param name="config">The server configuration.</param>
    /// <param name="pathSegments">Optional list of path segments for using statements.</param>
    /// <returns>Generated code content for the unified service collection extension class.</returns>
    public static string Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config,
        List<string>? pathSegments = null)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Auto-detect features from OpenAPI spec
        var hasRateLimiting = openApiDoc.HasRateLimiting();
        var hasValidation = config.UseValidationFilter != MinimalApiPackageMode.Disabled;
        var hasVersioning = config.VersioningStrategy != VersioningStrategyType.None;
        var hasSecurity = openApiDoc.HasSecuritySchemes();

        return GenerateFileContent(projectName, pathSegments, hasRateLimiting, hasValidation, hasVersioning, hasSecurity);
    }

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        List<string>? pathSegments,
        bool hasRateLimiting,
        bool hasValidation,
        bool hasVersioning,
        bool hasSecurity)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public static class UnifiedServiceCollectionExtensions");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "public static IServiceCollection AddApi(");
        contentBuilder.AppendLine(8, "this IServiceCollection services,");
        contentBuilder.AppendLine(8, "Action<ApiServiceOptions>? configure = null)");
        contentBuilder.AppendLine(8, "var options = new ApiServiceOptions();");
        contentBuilder.AppendLine(8, "configure?.Invoke(options);");
        if (hasRateLimiting)
        {
            contentBuilder.AppendLine(8, "services.AddApiRateLimiting();");
        }

        if (hasVersioning)
        {
            contentBuilder.AppendLine(8, "services.AddApiVersioning();");
        }

        if (hasSecurity)
        {
            contentBuilder.AppendLine(8, "services.AddApiSecurityPolicies();");
        }

        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, "System.CodeDom.Compiler"));
        builder.AppendLine($"using {projectName}.Generated;");

        // Add namespace for handlers
        if (pathSegments is { Count: > 0 })
        {
            foreach (var segment in pathSegments.OrderBy(s => s, StringComparer.Ordinal))
            {
                builder.AppendLine($"using {projectName}.Generated.{segment}.Handlers;");
            }
        }
        else
        {
            builder.AppendLine($"using {projectName}.Generated.Handlers;");
        }

        // Add namespaces for optional features
        if (hasRateLimiting)
        {
            builder.AppendLine($"using {projectName}.Generated.RateLimiting;");
        }

        if (hasVersioning)
        {
            builder.AppendLine($"using {projectName}.Generated.Versioning;");
        }

        if (hasSecurity)
        {
            builder.AppendLine($"using {projectName}.Generated.Security;");
        }

        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.Extensions;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Unified extension methods for configuring {projectName} API services.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class UnifiedServiceCollectionExtensions");
        builder.AppendLine("{");

        // Generate Add{ProjectName}Api method
        GenerateAddApiMethod(builder, projectName, hasRateLimiting, hasValidation, hasVersioning, hasSecurity);

        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the Add{ProjectName}Api method.
    /// </summary>
    private static void GenerateAddApiMethod(
        StringBuilder builder,
        string projectName,
        bool hasRateLimiting,
        bool hasValidation,
        bool hasVersioning,
        bool hasSecurity)
    {
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, $"/// Adds all {projectName} API services with auto-detected features.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        builder.AppendLine(4, "/// <param name=\"configure\">Optional configuration for API service options.</param>");
        builder.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        builder.AppendLine(4, "/// <remarks>");
        builder.AppendLine(4, "/// This method registers:");

        if (hasRateLimiting)
        {
            builder.AppendLine(4, "/// - Rate limiting policies (auto-detected)");
        }

        if (hasVersioning)
        {
            builder.AppendLine(4, "/// - API versioning (auto-detected)");
        }

        if (hasSecurity)
        {
            builder.AppendLine(4, "/// - Security policies (auto-detected)");
        }

        builder.AppendLine(4, "/// ");
        builder.AppendLine(4, "/// Note: You also need to register handlers and validators from your Domain project:");
        builder.AppendLine(4, "/// - services.AddApiHandlersFromDomain() - Handler implementations");

        if (hasValidation)
        {
            builder.AppendLine(4, "/// - services.AddApiValidatorsFromDomain() - FluentValidation validators");
        }

        builder.AppendLine(4, "/// </remarks>");
        builder.AppendLine(4, $"public static IServiceCollection Add{projectName.ToPascalCaseForDotNet()}Api(");
        builder.AppendLine(8, "this IServiceCollection services,");
        builder.AppendLine(8, "Action<ApiServiceOptions>? configure = null)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "var options = new ApiServiceOptions();");
        builder.AppendLine(8, "configure?.Invoke(options);");

        // Note: Handlers and validators are registered from Domain project, not here
        // because the Contracts project doesn't have access to Domain

        // Optional: rate limiting
        if (hasRateLimiting)
        {
            builder.AppendLine();
            builder.AppendLine(8, "// Register rate limiting policies from OpenAPI x-ratelimit-* extensions");
            builder.AppendLine(8, "if (options.UseRateLimiting)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, "services.AddApiRateLimiting();");
            builder.AppendLine(8, "}");
        }

        // Optional: versioning
        if (hasVersioning)
        {
            builder.AppendLine();
            builder.AppendLine(8, "// Register API versioning");
            builder.AppendLine(8, "if (options.UseVersioning)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, $"services.Add{projectName.ToPascalCaseForDotNet()}ApiVersioning();");
            builder.AppendLine(8, "}");
        }

        // Optional: security policies
        if (hasSecurity)
        {
            builder.AppendLine();
            builder.AppendLine(8, "// Register security policies from OpenAPI security schemes");
            builder.AppendLine(8, "if (options.UseSecurity)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, "services.AddApiSecurityPolicies();");
            builder.AppendLine(8, "}");
        }

        builder.AppendLine();
        builder.AppendLine(8, "return services;");
        builder.AppendLine(4, "}");
    }
}