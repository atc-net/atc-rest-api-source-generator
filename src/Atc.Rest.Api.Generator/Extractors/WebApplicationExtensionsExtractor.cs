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
    /// <returns>ClassParameters for the WebApplication extensions class, or null if no middleware is needed.</returns>
    public static ClassParameters? Extract(
        string projectName,
        bool useGlobalErrorHandler)
    {
        if (!useGlobalErrorHandler)
        {
            return null;
        }

        var methods = new List<MethodParameters>();

        // Generate Use{ProjectName}Api method
        var useApiMethod = GenerateUseApiMethod(projectName, useGlobalErrorHandler);
        methods.Add(useApiMethod);

        var headerContent = GenerateHeaderContent(projectName, includeEndpointUsings: false);

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
        ServerConfig config,
        bool useGlobalErrorHandler = true)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Auto-detect features from OpenAPI spec
        var hasRateLimiting = openApiDoc.HasRateLimiting();
        var hasSecurity = openApiDoc.HasSecuritySchemes() || openApiDoc.HasJwtBearerSecurity();

        // A limiter partitioned by user reads HttpContext.User: authentication has to run before it
        var limiterNeedsUser = hasRateLimiting && hasSecurity &&
                               RateLimitPoliciesExtractor.CollectPolicies(openApiDoc, config?.IncludeDeprecated ?? false)
                                   .Values.Any(p => p.Partition == RateLimitPartitionStrategy.User);

        return GenerateUnifiedFileContent(projectName, hasRateLimiting, hasSecurity, limiterNeedsUser, openApiDoc.HasOutputCaching(), useGlobalErrorHandler);
    }

    /// <summary>
    /// Generates the complete file content for the unified Map{ProjectName}Api method.
    /// </summary>
    private static string GenerateUnifiedFileContent(
        string projectName,
        bool hasRateLimiting,
        bool hasSecurity,
        bool limiterNeedsUser,
        bool hasOutputCaching,
        bool useGlobalErrorHandler)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public static class UnifiedWebApplicationExtensions");
        contentBuilder.AppendLine("public static WebApplication MapApi(this WebApplication app, Action<ApiMiddlewareOptions>? configure = null)");
        contentBuilder.AppendLine("var options = new ApiMiddlewareOptions();");
        if (useGlobalErrorHandler)
        {
            contentBuilder.AppendLine("app.UseGlobalErrorHandler(options.ConfigureErrorHandling);");
        }

        contentBuilder.AppendLine("app.MapEndpoints();");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, NamespaceConstants.SystemCodeDomCompiler));
        builder.AppendLine($"using {projectName}.Generated;");
        builder.AppendLine($"using {projectName}.Generated.Endpoints;");

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
        GenerateMapApiMethod(builder, projectName, hasRateLimiting, hasSecurity, limiterNeedsUser, hasOutputCaching, useGlobalErrorHandler);

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
        bool limiterNeedsUser,
        bool hasOutputCaching,
        bool useGlobalErrorHandler)
    {
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, $"/// Configures {projectName} API middleware and maps all endpoints.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"app\">The WebApplication to configure.</param>");
        builder.AppendLine(4, "/// <param name=\"configure\">Optional configuration for API middleware options.</param>");
        builder.AppendLine(4, "/// <returns>The WebApplication for method chaining.</returns>");
        builder.AppendLine(4, "/// <remarks>");
        builder.AppendLine(4, "/// This method configures:");
        builder.AppendLine(4, "/// - Status code pages writing ProblemDetails for empty error responses (when ProblemDetails is registered)");

        if (hasRateLimiting)
        {
            builder.AppendLine(4, "/// - Rate limiter middleware (auto-detected)");
        }

        if (hasSecurity)
        {
            builder.AppendLine(4, "/// - Authentication and authorization middleware (auto-detected)");
        }

        if (hasOutputCaching)
        {
            builder.AppendLine(4, "/// - Output cache middleware (auto-detected, after authentication)");
        }

        builder.AppendLine(4, useGlobalErrorHandler
            ? "/// - Global error handling middleware"
            : "/// - The framework's exception handler (a ProblemDetails 500 for an unhandled exception)");
        builder.AppendLine(4, "/// - All API endpoints");
        builder.AppendLine(4, "/// </remarks>");
        builder.AppendLine(4, $"public static WebApplication Map{CasingHelper.GetLastNameSegmentWithoutApiSuffix(projectName)}Api(");
        builder.AppendLine(8, "this WebApplication app,");
        builder.AppendLine(8, "Action<ApiMiddlewareOptions>? configure = null)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "var options = new ApiMiddlewareOptions();");
        builder.AppendLine(8, "configure?.Invoke(options);");

        if (!useGlobalErrorHandler)
        {
            // Without Atc.Rest.MinimalApi's error handler: the framework's, outermost, so it sees what anything below
            // throws. Development keeps its developer exception page.
            builder.AppendLine();
            builder.AppendLine(8, "// An unhandled exception as a ProblemDetails 500 (needs AddProblemDetails; not in Development, where the");
            builder.AppendLine(8, "// developer exception page shows it)");
            builder.AppendLine(8, "if (options.UseExceptionHandler &&");
            builder.AppendLine(8, "    !global::Microsoft.Extensions.Hosting.HostEnvironmentEnvExtensions.IsDevelopment(app.Environment) &&");
            builder.AppendLine(8, "    app.Services.GetService(typeof(global::Microsoft.AspNetCore.Http.IProblemDetailsService)) is not null)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, "app.UseExceptionHandler();");
            builder.AppendLine(8, "}");
        }

        // Status code pages first, so every empty error response below (rate limiter, auth, routing,
        // endpoints) gets a ProblemDetails body. Only with ProblemDetails registered: on its own,
        // UseStatusCodePages() writes text/plain.
        builder.AppendLine();
        builder.AppendLine(8, "// ProblemDetails body for error responses that have none (needs AddProblemDetails)");
        builder.AppendLine(8, "if (options.UseStatusCodePages &&");
        builder.AppendLine(8, "    app.Services.GetService(typeof(global::Microsoft.AspNetCore.Http.IProblemDetailsService)) is not null)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "app.UseStatusCodePages();");
        builder.AppendLine(8, "}");

        // The rate limiter runs before authentication, so a rejected request costs no token validation -
        // unless a policy partitions by user: then the limiter needs the authenticated user, and the
        // order is turned around.
        if (limiterNeedsUser)
        {
            AppendAuthentication(builder, hasSecurity);
            AppendRateLimiter(builder, hasRateLimiting, "// Rate limiter middleware (after authentication: a policy partitions by user)");
        }
        else
        {
            AppendRateLimiter(builder, hasRateLimiting, "// Rate limiter middleware (before auth to protect against DDoS)");
            AppendAuthentication(builder, hasSecurity);
        }

        // The output cache after authentication: the default policy never caches an authenticated caller's answer, and
        // it can only tell once the user is known
        if (hasOutputCaching)
        {
            builder.AppendLine();
            builder.AppendLine(8, "// Output cache middleware (after authentication: an authenticated caller's answer is not cached). It");
            builder.AppendLine(8, "// stands aside when the host added UseOutputCache() itself: a second one throws on every request.");
            builder.AppendLine(8, "if (options.UseOutputCache)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, "app.UseWhen(");
            builder.AppendLine(16, "context => context.Features.Get<global::Microsoft.AspNetCore.OutputCaching.IOutputCacheFeature>() is null,");
            builder.AppendLine(16, "branch => branch.UseOutputCache());");
            builder.AppendLine(8, "}");
        }

        // Global error handler (with Atc.Rest.MinimalApi)
        if (useGlobalErrorHandler)
        {
            builder.AppendLine();
            builder.AppendLine(8, "// Global error handling middleware (converts exceptions to ProblemDetails)");
            builder.AppendLine(8, "app.UseGlobalErrorHandler(options.ConfigureErrorHandling);");
        }

        // Map endpoints (always)
        builder.AppendLine();
        builder.AppendLine(8, "// Map all API endpoints");
        builder.AppendLine(8, "app.MapEndpoints();");
        builder.AppendLine();
        builder.AppendLine(8, "return app;");
        builder.AppendLine(4, "}");
    }

    private static void AppendRateLimiter(
        StringBuilder builder,
        bool hasRateLimiting,
        string comment)
    {
        if (!hasRateLimiting)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(8, comment);
        builder.AppendLine(8, "if (options.UseRateLimiter)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "app.UseRateLimiter();");
        builder.AppendLine(8, "}");
    }

    private static void AppendAuthentication(
        StringBuilder builder,
        bool hasSecurity)
    {
        if (!hasSecurity)
        {
            return;
        }

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

    private static string GenerateHeaderContent(
        string projectName,
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
        builder.Append(UsingStatementHelper.BuildHeader(content, NamespaceConstants.SystemCodeDomCompiler));

        if (includeEndpointUsings)
        {
            builder.AppendLine($"using {projectName}.Generated.Endpoints;");
        }

        return builder.ToString();
    }

    private static MethodParameters GenerateUseApiMethod(
        string projectName,
        bool useGlobalErrorHandler)
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

        var content = GenerateUseApiMethodContent(useGlobalErrorHandler);

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
            Name: $"Use{CasingHelper.GetLastNameSegmentWithoutApiSuffix(projectName)}Api",
            Parameters: methodParams,
            AlwaysBreakDownParameters: true,
            UseExpressionBody: false,
            Content: content);
    }

    private static string GenerateUseApiMethodContent(
        bool useGlobalErrorHandler)
    {
        var builder = new StringBuilder();

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