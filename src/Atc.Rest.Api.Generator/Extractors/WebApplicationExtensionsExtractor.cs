namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Generates the host wiring of a server: <c>Use{Api}Api()</c> adds the middleware the specification calls for, in the
/// recommended order; <c>Map{Api}Api()</c> maps the endpoints and nothing else (ASP.NET's own Use/Map split). The
/// endpoint method is not called <c>Map{Api}Endpoints()</c>: a path segment's own <c>Map{Segment}Endpoints()</c> would
/// clash with it (an API <c>Pets</c> with a <c>/pets</c> path).
/// </summary>
public static class WebApplicationExtensionsExtractor
{
    /// <summary>
    /// Generates the <c>Use{Api}Api()</c> and <c>Map{Api}Api()</c> extension methods.
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
    /// Generates the complete file content of the WebApplication extension class.
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
        contentBuilder.AppendLine("public static WebApplication UseApi(this WebApplication app, Action<ApiMiddlewareOptions>? configure = null)");
        contentBuilder.AppendLine("var options = new ApiMiddlewareOptions();");
        if (useGlobalErrorHandler)
        {
            contentBuilder.AppendLine("app.UseGlobalErrorHandler(options.ConfigureErrorHandling);");
        }

        contentBuilder.AppendLine("throw new InvalidOperationException(");
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
        builder.AppendLine($"/// The host wiring of the {apiName} API: <see cref=\"Use{apiName}Api\"/> adds the middleware,");
        builder.AppendLine($"/// <see cref=\"Map{apiName}Api\"/> maps the endpoints.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class UnifiedWebApplicationExtensions");
        builder.AppendLine("{");

        // The mark Use{Api}Api() leaves in the application's properties, for Map{Api}Api() to check
        builder.AppendLine(4, $"private const string UseApiMark = \"{projectName}.Generated.Use{apiName}Api\";");
        builder.AppendLine();

        GenerateUseApiMethod(builder, projectName, apiName, features, useGlobalErrorHandler);
        builder.AppendLine();
        GenerateMapApiMethod(builder, projectName, apiName);

        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the Use{Api}Api method: the middleware, in the recommended order, each behind its own switch.
    /// </summary>
    private static void GenerateUseApiMethod(
        StringBuilder builder,
        string projectName,
        string apiName,
        ServerFeatures features,
        bool useGlobalErrorHandler)
    {
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, $"/// Adds the middleware the {apiName} API needs, in the recommended order. Call it before <see cref=\"Map{apiName}Api\"/>.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"app\">The web application.</param>");
        builder.AppendLine(4, "/// <param name=\"configure\">");
        builder.AppendLine(4, "/// Turn off a middleware the host adds itself (each has a switch in <see cref=\"ApiMiddlewareOptions\"/>), or add");
        builder.AppendLine(4, "/// the host's own middleware with <c>AfterErrorHandling</c>.");
        builder.AppendLine(4, "/// </param>");
        builder.AppendLine(4, "/// <returns>The web application, for chaining.</returns>");

        // The middleware in the order this method adds it - a list, so a tooltip shows one per line
        var middleware = new List<string>
        {
            useGlobalErrorHandler
                ? "Global error handler - first, so what anything after it throws becomes a ProblemDetails"
                : "Exception handler (outside Development) - a ProblemDetails 500 for an unhandled exception",
            "Status code pages - a ProblemDetails body for an error response that has none",
            "The host's own middleware from <c>AfterErrorHandling</c> (CORS, HSTS, HTTPS redirection)",
        };
        var rateLimiter = features.HasRateLimiting
            ? (features.LimiterNeedsUser
                ? "Rate limiter - after authentication, as a policy partitions by user"
                : "Rate limiter - the specification declares rate limits")
            : null;
        var authentication = features.RequiresAuthentication
            ? "Authentication and authorization - the specification requires a signed-in caller"
            : null;
        middleware.AddRange((features.LimiterNeedsUser ? new[] { authentication, rateLimiter } : new[] { rateLimiter, authentication }).OfType<string>());
        if (features.HasOutputCaching)
        {
            middleware.Add("Output cache - after authentication, so an authenticated caller's answer is not cached");
        }

        builder.AppendLine(4, "/// <remarks>");
        XmlDocCommentHelper.AppendParagraph(builder, 4, "Adds, in this order:");
        XmlDocCommentHelper.AppendBulletList(builder, 4, middleware);
        builder.AppendLine(4, "/// <para>");
        builder.AppendLine(4, "/// Turn off what the host adds itself. A host that orders its whole pipeline itself does not call this method,");
        builder.AppendLine(4, $"/// adds the middleware above itself and calls <c>Map{apiName}Api(requireUse: false)</c>.");
        builder.AppendLine(4, "/// </para>");
        builder.AppendLine(4, "/// </remarks>");
        builder.AppendLine(4, $"public static WebApplication Use{apiName}Api(");
        builder.AppendLine(8, "this WebApplication app,");
        builder.AppendLine(8, "Action<ApiMiddlewareOptions>? configure = null)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "var options = new ApiMiddlewareOptions();");
        builder.AppendLine(8, "configure?.Invoke(options);");

        if (useGlobalErrorHandler)
        {
            // Atc.Rest.MinimalApi's error handler, outermost: a middleware only catches what is thrown after it, so it
            // also turns an exception from authentication (an unreachable token authority) or the limiter into a problem
            builder.AppendLine();
            builder.AppendLine(8, "// Global error handling middleware, first: it converts what anything after it throws (authentication, the");
            builder.AppendLine(8, "// rate limiter, the endpoints) to a ProblemDetails");
            builder.AppendLine(8, "if (options.UseGlobalErrorHandler)");
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

        // The host's own middleware: inside the error handling (what it throws or answers empty is a ProblemDetails),
        // ahead of the limiter, authentication and the output cache (CORS has to run before authentication)
        builder.AppendLine();
        builder.AppendLine(8, "// The host's own middleware (CORS, HSTS, HTTPS redirection): inside the error handling, ahead of the rest");
        builder.AppendLine(8, "options.AfterErrorHandling?.Invoke(app);");

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

        builder.AppendLine();
        builder.AppendLine(8, "((global::Microsoft.AspNetCore.Builder.IApplicationBuilder)app).Properties[UseApiMark] = true;");
        builder.AppendLine();
        builder.AppendLine(8, "return app;");
        builder.AppendLine(4, "}");
    }

    /// <summary>
    /// Generates the Map{Api}Api method: the endpoints, no middleware. Until 3.1 the method of that name also added the
    /// middleware, so it refuses to run without Use{Api}Api() first - unless the host orders its own pipeline.
    /// </summary>
    private static void GenerateMapApiMethod(
        StringBuilder builder,
        string projectName,
        string apiName)
    {
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, $"/// Maps all endpoints of the {apiName} API. Adds no middleware: call <see cref=\"Use{apiName}Api\"/> first.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"app\">The web application.</param>");
        builder.AppendLine(4, "/// <param name=\"requireUse\">");
        builder.AppendLine(4, $"/// True (default): throw when <see cref=\"Use{apiName}Api\"/> was not called first. False: the host adds the");
        builder.AppendLine(4, "/// middleware itself.");
        builder.AppendLine(4, "/// </param>");
        builder.AppendLine(4, "/// <returns>The web application, for chaining.</returns>");
        builder.AppendLine(4, $"/// <exception cref=\"InvalidOperationException\"><see cref=\"Use{apiName}Api\"/> was not called first and <paramref name=\"requireUse\"/> is true.</exception>");
        builder.AppendLine(4, "/// <remarks>");
        XmlDocCommentHelper.AppendParagraph(builder, 4, $"Until 3.1 this method also added the middleware; since 3.2 <see cref=\"Use{apiName}Api\"/> does.");
        builder.AppendLine(4, "/// </remarks>");
        builder.AppendLine(4, $"public static WebApplication Map{apiName}Api(");
        builder.AppendLine(8, "this WebApplication app,");
        builder.AppendLine(8, "bool requireUse = true)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "// Until 3.1 this method also added the middleware: a host that still calls only it would lose its error");
        builder.AppendLine(8, "// handling without a compile error");
        builder.AppendLine(8, "if (requireUse && !((global::Microsoft.AspNetCore.Builder.IApplicationBuilder)app).Properties.ContainsKey(UseApiMark))");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "throw new InvalidOperationException(");
        builder.AppendLine(16, $"\"Call app.Use{apiName}Api() before app.Map{apiName}Api(): it adds the error handling, the status code pages \" +");
        builder.AppendLine(16, $"\"and the rest of the middleware the endpoints rely on (since 3.2 Map{apiName}Api() maps only the endpoints). \" +");
        builder.AppendLine(16, $"\"A host that adds that middleware itself calls app.Map{apiName}Api(requireUse: false).\");");
        builder.AppendLine(8, "}");
        builder.AppendLine();
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
}