namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts parameters for WebApplication extension methods that configure middleware.
/// Generates the unified Map{Api}Api() method: the middleware the specification calls for, then the endpoints.
/// </summary>
public static class WebApplicationExtensionsExtractor
{
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

        return GenerateUnifiedFileContent(projectName, CasingHelper.GetApiName(projectName, config?.ApiName), ServerFeatures.Of(openApiDoc, config), useGlobalErrorHandler);
    }

    /// <summary>
    /// Generates the complete file content for the unified Map{ProjectName}Api method.
    /// </summary>
    private static string GenerateUnifiedFileContent(
        string projectName,
        string apiName,
        ServerFeatures features,
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
        GenerateMapApiMethod(builder, projectName, apiName, features, useGlobalErrorHandler);

        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the Map{ProjectName}Api method.
    /// </summary>
    private static void GenerateMapApiMethod(
        StringBuilder builder,
        string projectName,
        string apiName,
        ServerFeatures features,
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

        if (features.HasRateLimiting)
        {
            builder.AppendLine(4, "/// - Rate limiter middleware (auto-detected)");
        }

        if (features.RequiresAuthentication)
        {
            builder.AppendLine(4, "/// - Authentication and authorization middleware (auto-detected)");
        }

        if (features.HasOutputCaching)
        {
            builder.AppendLine(4, "/// - Output cache middleware (auto-detected, after authentication)");
        }

        builder.AppendLine(4, useGlobalErrorHandler
            ? "/// - Global error handling middleware"
            : "/// - The framework's exception handler (a ProblemDetails 500 for an unhandled exception)");
        builder.AppendLine(4, "/// - All API endpoints");
        builder.AppendLine(4, "/// </remarks>");
        builder.AppendLine(4, $"public static WebApplication Map{apiName}Api(");
        builder.AppendLine(8, "this WebApplication app,");
        builder.AppendLine(8, "Action<ApiMiddlewareOptions>? configure = null)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "var options = new ApiMiddlewareOptions();");
        builder.AppendLine(8, "configure?.Invoke(options);");

        // A host that orders its own pipeline leaves every middleware it has a counterpart of to itself - also one a
        // later version adds
        builder.AppendLine();
        builder.AppendLine(8, "// A host that orders its own pipeline has its own error handler, status code pages and authentication");
        builder.AppendLine(8, "var hostOwnsPipeline = options.HostOwnsPipeline;");

        if (useGlobalErrorHandler)
        {
            // Atc.Rest.MinimalApi's error handler, outermost: a middleware only catches what is thrown after it, so it
            // also turns an exception from authentication (an unreachable token authority) or the limiter into a problem
            builder.AppendLine();
            builder.AppendLine(8, "// Global error handling middleware, first: it converts what anything after it throws (authentication, the");
            builder.AppendLine(8, "// rate limiter, the endpoints) to a ProblemDetails");
            builder.AppendLine(8, "if (!hostOwnsPipeline && options.UseGlobalErrorHandler)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, "app.UseGlobalErrorHandler(options.ConfigureErrorHandling);");
            builder.AppendLine(8, "}");
        }
        else
        {
            // Without Atc.Rest.MinimalApi's error handler: the framework's, outermost, so it sees what anything below
            // throws. Development keeps its developer exception page.
            builder.AppendLine();
            builder.AppendLine(8, "// An unhandled exception as a ProblemDetails 500 (needs AddProblemDetails; not in Development, where the");
            builder.AppendLine(8, "// developer exception page shows it)");
            builder.AppendLine(8, "if (!hostOwnsPipeline && options.UseExceptionHandler &&");
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
        builder.AppendLine(8, "if (!hostOwnsPipeline && options.UseStatusCodePages &&");
        builder.AppendLine(8, "    app.Services.GetService(typeof(global::Microsoft.AspNetCore.Http.IProblemDetailsService)) is not null)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "app.UseStatusCodePages();");
        builder.AppendLine(8, "}");

        // The rate limiter runs before authentication, so a rejected request costs no token validation -
        // unless a policy partitions by user: then the limiter needs the authenticated user, and the
        // order is turned around.
        if (features.LimiterNeedsUser)
        {
            AppendAuthentication(builder, features);
            AppendRateLimiter(builder, features, "// Rate limiter middleware (after authentication: a policy partitions by user)");
        }
        else
        {
            AppendRateLimiter(builder, features, "// Rate limiter middleware (before auth to protect against DDoS)");
            AppendAuthentication(builder, features);
        }

        // The output cache after authentication: the default policy never caches an authenticated caller's answer, and
        // it can only tell once the user is known
        if (features.HasOutputCaching)
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
        ServerFeatures features,
        string comment)
    {
        if (!features.HasRateLimiting)
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
        ServerFeatures features)
    {
        if (!features.RequiresAuthentication)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(8, "// Authentication and authorization middleware");
        builder.AppendLine(8, "if (!hostOwnsPipeline && options.UseAuthentication)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "app.UseAuthentication();");
        builder.AppendLine(12, "if (options.UseAuthorization)");
        builder.AppendLine(12, "{");
        builder.AppendLine(16, "app.UseAuthorization();");
        builder.AppendLine(12, "}");
        builder.AppendLine(8, "}");
        builder.AppendLine(8, "else if (!hostOwnsPipeline && options.UseAuthorization)");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "app.UseAuthorization();");
        builder.AppendLine(8, "}");
    }
}