namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts parameters for WebApplication extension methods that configure middleware.
/// Generates UseGlobalErrorHandler and UseEndpointDefinitions setup when Atc.Rest.MinimalApi is referenced.
/// </summary>
public static class WebApplicationExtensionsExtractor
{
    /// <summary>
    /// Extracts WebApplication extension class parameters for middleware configuration.
    /// </summary>
    /// <param name="projectName">The name of the project (used for namespace and method naming).</param>
    /// <param name="useGlobalErrorHandler">Whether to include GlobalErrorHandler middleware setup.</param>
    /// <param name="useAtcExceptionMapping">Whether to include AtcExceptionMapping middleware setup (TEMPORARY).</param>
    /// <returns>ClassParameters for the WebApplication extensions class, or null if no middleware is needed.</returns>
    public static ClassParameters? Extract(
        string projectName,
        bool useGlobalErrorHandler,
        bool useAtcExceptionMapping = false)
    {
        if (!useGlobalErrorHandler)
        {
            return null;
        }

        var methods = new List<MethodParameters>();

        // Generate Use{ProjectName}Api method
        var useApiMethod = GenerateUseApiMethod(projectName, useGlobalErrorHandler, useAtcExceptionMapping);
        methods.Add(useApiMethod);

        var headerContent = GenerateHeaderContent(projectName, useAtcExceptionMapping, includeEndpointUsings: false);

        return new ClassParameters(
            HeaderContent: headerContent,
            Namespace: $"{projectName}.Generated.Extensions",
            DocumentationTags: new CodeDocumentationTags($"Extension methods for configuring {projectName} API middleware."),
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicStaticClass,
            ClassTypeName: "WebApplicationExtensions",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: methods,
            GenerateToStringMethod: false);
    }

    /// <summary>
    /// Generates the unified Map{ProjectName}Api() extension method that combines middleware and endpoint mapping.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document for auto-detection.</param>
    /// <param name="projectName">The project name for namespace and method naming.</param>
    /// <param name="config">The server configuration.</param>
    /// <returns>Generated code content for the unified WebApplication extension class.</returns>
    public static string ExtractUnified(
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Auto-detect features from OpenAPI spec
        var hasRateLimiting = openApiDoc.HasRateLimiting();
        var hasSecurity = openApiDoc.HasSecuritySchemes() || openApiDoc.HasJwtBearerSecurity();
        var useAtcExceptionMapping = config.UseAtcExceptionMapping != MinimalApiPackageMode.Disabled;

        return GenerateUnifiedFileContent(projectName, hasRateLimiting, hasSecurity, useAtcExceptionMapping);
    }

    /// <summary>
    /// Generates the complete file content for the unified Map{ProjectName}Api method.
    /// </summary>
    private static string GenerateUnifiedFileContent(
        string projectName,
        bool hasRateLimiting,
        bool hasSecurity,
        bool useAtcExceptionMapping)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public static class UnifiedWebApplicationExtensions");
        contentBuilder.AppendLine("public static WebApplication MapApi(this WebApplication app, Action<ApiMiddlewareOptions>? configure = null)");
        contentBuilder.AppendLine("var options = new ApiMiddlewareOptions();");
        contentBuilder.AppendLine("app.UseGlobalErrorHandler(options.ConfigureErrorHandling);");
        contentBuilder.AppendLine("app.MapEndpoints();");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, "System.CodeDom.Compiler"));
        builder.AppendLine($"using {projectName}.Generated;");
        builder.AppendLine($"using {projectName}.Generated.Endpoints;");

        if (useAtcExceptionMapping)
        {
            builder.AppendLine($"using {projectName}.Generated.Middleware;");
        }

        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.Extensions;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Unified extension methods for configuring {projectName} API middleware and endpoints.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class UnifiedWebApplicationExtensions");
        builder.AppendLine("{");

        // Generate Map{ProjectName}Api method
        GenerateMapApiMethod(builder, projectName, hasRateLimiting, hasSecurity, useAtcExceptionMapping);

        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the Map{ProjectName}Api method.
    /// </summary>
    private static void GenerateMapApiMethod(
        StringBuilder builder,
        string projectName,
        bool hasRateLimiting,
        bool hasSecurity,
        bool useAtcExceptionMapping)
    {
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, $"/// Configures {projectName} API middleware and maps all endpoints.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"app\">The WebApplication to configure.</param>");
        builder.AppendLine(4, "/// <param name=\"configure\">Optional configuration for API middleware options.</param>");
        builder.AppendLine(4, "/// <returns>The WebApplication for method chaining.</returns>");
        builder.AppendLine(4, "/// <remarks>");
        builder.AppendLine(4, "/// This method configures:");

        if (hasRateLimiting)
        {
            builder.AppendLine(4, "/// - Rate limiter middleware (auto-detected)");
        }

        if (hasSecurity)
        {
            builder.AppendLine(4, "/// - Authentication and authorization middleware (auto-detected)");
        }

        builder.AppendLine(4, "/// - Global error handling middleware");
        builder.AppendLine(4, "/// - All API endpoints");
        builder.AppendLine(4, "/// </remarks>");
        builder.AppendLine(4, $"public static WebApplication Map{projectName.ToPascalCaseForDotNet()}Api(");
        builder.AppendLine(8, "this WebApplication app,");
        builder.AppendLine(8, "Action<ApiMiddlewareOptions>? configure = null)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "var options = new ApiMiddlewareOptions();");
        builder.AppendLine(8, "configure?.Invoke(options);");

        // Rate limiter (optional)
        if (hasRateLimiting)
        {
            builder.AppendLine();
            builder.AppendLine(8, "// Rate limiter middleware (before auth to protect against DDoS)");
            builder.AppendLine(8, "if (options.UseRateLimiter)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, "app.UseRateLimiter();");
            builder.AppendLine(8, "}");
        }

        // Authentication and authorization (optional)
        if (hasSecurity)
        {
            builder.AppendLine();
            builder.AppendLine(8, "// Authentication and authorization middleware");
            builder.AppendLine(8, "if (options.UseAuthentication)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, "app.UseAuthentication();");
            builder.AppendLine(12, "if (options.UseAuthorization)");
            builder.AppendLine(12, "{");
            builder.AppendLine(16, "app.UseAuthorization();");
            builder.AppendLine(12, "}");
            builder.AppendLine(8, "}");
            builder.AppendLine(8, "else if (options.UseAuthorization)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, "app.UseAuthorization();");
            builder.AppendLine(8, "}");
        }

        // AtcExceptionMapping (TEMPORARY)
        if (useAtcExceptionMapping)
        {
            builder.AppendLine();
            builder.AppendLine(8, "// ATC exception mapping middleware (TEMPORARY)");
            builder.AppendLine(8, "// See: https://github.com/atc-net/atc-rest-minimalapi/issues/22");
            builder.AppendLine(8, "app.UseAtcExceptionMapping();");
        }

        // Global error handler (always)
        builder.AppendLine();
        builder.AppendLine(8, "// Global error handling middleware (converts exceptions to ProblemDetails)");
        builder.AppendLine(8, "app.UseGlobalErrorHandler(options.ConfigureErrorHandling);");

        // Map endpoints (always)
        builder.AppendLine();
        builder.AppendLine(8, "// Map all API endpoints");
        builder.AppendLine(8, "app.MapEndpoints();");
        builder.AppendLine();
        builder.AppendLine(8, "return app;");
        builder.AppendLine(4, "}");
    }

    private static string GenerateHeaderContent(
        string projectName,
        bool useAtcExceptionMapping,
        bool includeEndpointUsings = false)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("[GeneratedCode]");
        contentBuilder.AppendLine("public static WebApplication UseApi(this WebApplication app, Action<GlobalErrorHandlingOptions>? configureErrorHandling = null)");
        contentBuilder.AppendLine("app.UseGlobalErrorHandler(configureErrorHandling);");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, "System.CodeDom.Compiler"));

        if (includeEndpointUsings)
        {
            builder.AppendLine($"using {projectName}.Generated.Endpoints;");
        }

        // Add AtcExceptionMapping namespace (TEMPORARY: waiting for Atc.Rest.MinimalApi feature)
        if (useAtcExceptionMapping)
        {
            builder.AppendLine($"using {projectName}.Generated.Middleware;");
        }

        return builder.ToString();
    }

    private static MethodParameters GenerateUseApiMethod(
        string projectName,
        bool useGlobalErrorHandler,
        bool useAtcExceptionMapping)
    {
        var methodParams = new List<ParameterBaseParameters>
        {
            new(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: "this WebApplication",
                IsNullableType: false,
                IsReferenceType: true,
                Name: "app",
                DefaultValue: null),
            new(
                Attributes: null,
                GenericTypeName: "Action",
                IsGenericListType: false,
                TypeName: "GlobalErrorHandlingOptions",
                IsNullableType: true,
                IsReferenceType: true,
                Name: "configureErrorHandling",
                DefaultValue: "null"),
        };

        var content = GenerateUseApiMethodContent(useGlobalErrorHandler, useAtcExceptionMapping);

        var parameterDocs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["app"] = "The WebApplication to configure.",
            ["configureErrorHandling"] = "Optional configuration for global error handling options.",
        };

        return new MethodParameters(
            DocumentationTags: new CodeDocumentationTags(
                $"Configures {projectName} API middleware including global error handling.",
                parameterDocs,
                remark: null,
                code: null,
                example: null,
                exceptions: null,
                @return: "The WebApplication for method chaining."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: "WebApplication",
            Name: $"Use{projectName}Api",
            Parameters: methodParams,
            AlwaysBreakDownParameters: true,
            UseExpressionBody: false,
            Content: content);
    }

    private static string GenerateUseApiMethodContent(
        bool useGlobalErrorHandler,
        bool useAtcExceptionMapping)
    {
        var builder = new StringBuilder();

        // AtcExceptionMapping must be registered BEFORE GlobalErrorHandler
        // so that custom exception mappings take precedence
        // TEMPORARY: Waiting for feature in Atc.Rest.MinimalApi (GitHub issue #22)
        if (useAtcExceptionMapping)
        {
            builder.AppendLine("// Configure ATC exception mapping middleware (TEMPORARY)");
            builder.AppendLine("// See: https://github.com/atc-net/atc-rest-minimalapi/issues/22");
            builder.AppendLine("app.UseAtcExceptionMapping();");
            builder.AppendLine();
        }

        if (useGlobalErrorHandler)
        {
            builder.AppendLine("// Configure global error handling middleware");
            builder.AppendLine("app.UseGlobalErrorHandler(configureErrorHandling);");
            builder.AppendLine();
        }

        builder.Append("return app;");

        return builder.ToString();
    }
}