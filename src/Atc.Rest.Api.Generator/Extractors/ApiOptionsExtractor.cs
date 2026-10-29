namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts parameters for generating API options classes (ApiServiceOptions, ApiMiddlewareOptions).
/// These options enable simplified API surface while allowing granular control.
/// </summary>
public static class ApiOptionsExtractor
{
    /// <summary>
    /// Generates the ApiServiceOptions class.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document for auto-detection.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="config">The server configuration for versioning settings.</param>
    /// <returns>Generated code content for the ApiServiceOptions class.</returns>
    public static string ExtractServiceOptions(
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        return GenerateApiServiceOptionsContent(projectName, ServerFeatures.Of(openApiDoc, config));
    }

    /// <summary>
    /// Generates the ApiMiddlewareOptions class.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document for auto-detection.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <returns>Generated code content for the ApiMiddlewareOptions class.</returns>
    public static string ExtractMiddlewareOptions(
        OpenApiDocument openApiDoc,
        string projectName,
        bool useGlobalErrorHandler = true)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        return GenerateApiMiddlewareOptionsContent(projectName, ServerFeatures.Of(openApiDoc, config: null), useGlobalErrorHandler);
    }

    /// <summary>
    /// Generates the complete file content for ApiServiceOptions.
    /// </summary>
    private static string GenerateApiServiceOptionsContent(
        string projectName,
        ServerFeatures features)
    {
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public class ApiServiceOptions");
        var content = contentBuilder.ToString();

        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, NamespaceConstants.SystemCodeDomCompiler));
        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated;");
        builder.AppendLine();

        GenerateApiServiceOptions(builder, features);

        return builder.ToString();
    }

    /// <summary>
    /// Generates the complete file content for ApiMiddlewareOptions.
    /// </summary>
    private static string GenerateApiMiddlewareOptionsContent(
        string projectName,
        ServerFeatures features,
        bool useGlobalErrorHandler)
    {
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public class ApiMiddlewareOptions");
        contentBuilder.AppendLine("public Action<IApplicationBuilder>? AfterErrorHandling { get; set; }");
        if (useGlobalErrorHandler)
        {
            contentBuilder.AppendLine("public Action<GlobalErrorHandlingOptions>? ConfigureErrorHandling { get; set; }");
        }

        var content = contentBuilder.ToString();

        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, NamespaceConstants.SystemCodeDomCompiler));
        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated;");
        builder.AppendLine();

        GenerateApiMiddlewareOptions(builder, features, useGlobalErrorHandler);

        return builder.ToString();
    }

    /// <summary>
    /// Generates the ApiServiceOptions class.
    /// </summary>
    private static void GenerateApiServiceOptions(
        StringBuilder builder,
        ServerFeatures features)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// The options of the generated <c>Add…Api()</c>: a switch per registration it makes.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("/// <remarks>");
        builder.AppendLine("/// Note: the handlers (AddApiHandlersFrom{HandlerProject}) and the validators (AddApiValidatorsFrom{HandlerProject},");
        builder.AppendLine("/// generated when the handler project has one) are registered next to these, from the handler project.");
        builder.AppendLine("/// </remarks>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public class ApiServiceOptions");
        builder.AppendLine("{");

        // Each feature switch exists only when Add{Api}Api() reads it - when the specification (or the marker)
        // declares the feature. A switch nothing reads would say the feature can be turned on.
        if (features.HasRateLimiting)
        {
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Register the rate limiting policies (<c>AddApiRateLimiting()</c>).");
            builder.AppendLine(4, "/// Generated because the specification declares x-ratelimit-* policies. Default: true.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public bool UseRateLimiting { get; set; } = true;");
            builder.AppendLine();
        }

        if (features.HasOutputCaching)
        {
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Register the output cache policies (<c>AddApiOutputCaching()</c>).");
            builder.AppendLine(4, "/// Generated because the specification declares x-cache-type: output. Default: true.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public bool UseOutputCaching { get; set; } = true;");
            builder.AppendLine();
        }

        if (features.HasVersioning)
        {
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Register API versioning.");
            builder.AppendLine(4, "/// Generated because the server marker sets a versioningStrategy. Default: true.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public bool UseVersioning { get; set; } = true;");
            builder.AppendLine();
        }

        if (features.HasSecuritySchemes)
        {
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Register the authorization policies of the security schemes (<c>AddApiSecurityPolicies()</c>).");
            builder.AppendLine(4, "/// Generated because the specification declares security schemes. Default: true.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public bool UseSecurity { get; set; } = true;");
            builder.AppendLine();
        }

        // UseProblemDetails - always on by default
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Register RFC 9457 ProblemDetails (<c>AddProblemDetails()</c>), so errors raised outside the handlers");
        builder.AppendLine(4, "/// (malformed requests, unknown routes, empty error results) get an <c>application/problem+json</c> body.");
        builder.AppendLine(4, "/// Turn off when the host configures ProblemDetails itself. Default: true.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "public bool UseProblemDetails { get; set; } = true;");

        builder.AppendLine("}");
    }

    /// <summary>
    /// Generates the ApiMiddlewareOptions class.
    /// </summary>
    private static void GenerateApiMiddlewareOptions(
        StringBuilder builder,
        ServerFeatures features,
        bool useGlobalErrorHandler)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// The options of the generated <c>Use…Api()</c>: a switch per middleware it adds, and");
        builder.AppendLine("/// <see cref=\"AfterErrorHandling\"/> for the host's own middleware.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public class ApiMiddlewareOptions");
        builder.AppendLine("{");

        // The host's own middleware, at the spot the generator keeps for it
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// The host's own middleware (CORS, HSTS, HTTPS redirection, ...), added right after the error handling and");
        builder.AppendLine(4, "/// before the rate limiter, authentication and the output cache: what it throws or answers empty becomes a");
        builder.AppendLine(4, "/// ProblemDetails, and CORS runs before authentication as it has to. Default: none.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "public Action<IApplicationBuilder>? AfterErrorHandling { get; set; }");
        builder.AppendLine();

        // Each feature switch exists only when Use{Api}Api() reads it - when the specification declares the feature
        if (features.HasRateLimiting)
        {
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Add the rate limiter middleware (<c>UseRateLimiter()</c>).");
            builder.AppendLine(4, "/// Generated because the specification declares x-ratelimit-* policies. Default: true.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public bool UseRateLimiter { get; set; } = true;");
            builder.AppendLine();
        }

        if (features.HasOutputCaching)
        {
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Add the output cache middleware (<c>UseOutputCache()</c>, after authentication: an authenticated");
            builder.AppendLine(4, "/// caller's answer is never cached). A host that adds <c>UseOutputCache()</c> itself after <c>Use…Api()</c>");
            builder.AppendLine(4, "/// turns this off - a second output cache throws on every request; one it adds before is detected.");
            builder.AppendLine(4, "/// Generated because the specification declares x-cache-type: output. Default: true.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public bool UseOutputCache { get; set; } = true;");
            builder.AppendLine();
        }

        if (features.RequiresAuthentication)
        {
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Add the authentication middleware (<c>UseAuthentication()</c>).");
            builder.AppendLine(4, "/// Generated because the specification requires a signed-in caller (security schemes, x-authorize-roles");
            builder.AppendLine(4, "/// or x-authentication-required). Default: true.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public bool UseAuthentication { get; set; } = true;");
            builder.AppendLine();

            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Add the authorization middleware (<c>UseAuthorization()</c>).");
            builder.AppendLine(4, "/// Generated because the specification requires a signed-in caller (security schemes, x-authorize-roles");
            builder.AppendLine(4, "/// or x-authentication-required). Default: true.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public bool UseAuthorization { get; set; } = true;");
            builder.AppendLine();
        }

        // UseStatusCodePages - always on by default
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Write a ProblemDetails body for error responses that have none (<c>UseStatusCodePages()</c>).");
        builder.AppendLine(4, "/// Only applied when ProblemDetails is registered (see <c>ApiServiceOptions.UseProblemDetails</c>);");
        builder.AppendLine(4, "/// without it, status code pages would write plain text. Default: true.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "public bool UseStatusCodePages { get; set; } = true;");
        builder.AppendLine();

        if (useGlobalErrorHandler)
        {
            // ConfigureErrorHandling - with Atc.Rest.MinimalApi's global error handler
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Add Atc.Rest.MinimalApi's global error handler (<c>UseGlobalErrorHandler()</c>) first, which answers an");
            builder.AppendLine(4, "/// exception with a ProblemDetails. Turn off when the host adds it itself, at the start of its own pipeline.");
            builder.AppendLine(4, "/// Default: true.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public bool UseGlobalErrorHandler { get; set; } = true;");
            builder.AppendLine();
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Configure global error handling options.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public Action<GlobalErrorHandlingOptions>? ConfigureErrorHandling { get; set; }");
        }
        else
        {
            // Without the package the framework's own exception handler answers an unhandled exception
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Answer an unhandled exception with a ProblemDetails 500 (<c>UseExceptionHandler()</c>). Only applied");
            builder.AppendLine(4, "/// when ProblemDetails is registered (see <c>ApiServiceOptions.UseProblemDetails</c>) and outside the");
            builder.AppendLine(4, "/// Development environment, where the developer exception page shows the exception. Default: true.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public bool UseExceptionHandler { get; set; } = true;");
        }

        builder.AppendLine("}");
    }
}