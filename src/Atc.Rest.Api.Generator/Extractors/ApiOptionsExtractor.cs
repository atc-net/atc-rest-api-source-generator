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

        var hasRateLimiting = openApiDoc.HasRateLimiting();
        var hasVersioning = config.VersioningStrategy != VersioningStrategyType.None;
        var hasSecurity = openApiDoc.HasSecuritySchemes() || openApiDoc.HasJwtBearerSecurity();

        return GenerateApiServiceOptionsContent(projectName, hasRateLimiting, hasVersioning, hasSecurity, openApiDoc.HasOutputCaching());
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

        var hasRateLimiting = openApiDoc.HasRateLimiting();
        var hasSecurity = openApiDoc.HasSecuritySchemes() || openApiDoc.HasJwtBearerSecurity();

        return GenerateApiMiddlewareOptionsContent(projectName, hasRateLimiting, hasSecurity, openApiDoc.HasOutputCaching(), useGlobalErrorHandler);
    }

    /// <summary>
    /// Generates the complete file content for ApiServiceOptions.
    /// </summary>
    private static string GenerateApiServiceOptionsContent(
        string projectName,
        bool hasRateLimiting,
        bool hasVersioning,
        bool hasSecurity,
        bool hasOutputCaching)
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

        GenerateApiServiceOptions(builder, projectName, hasRateLimiting, hasVersioning, hasSecurity, hasOutputCaching);

        return builder.ToString();
    }

    /// <summary>
    /// Generates the complete file content for ApiMiddlewareOptions.
    /// </summary>
    private static string GenerateApiMiddlewareOptionsContent(
        string projectName,
        bool hasRateLimiting,
        bool hasSecurity,
        bool hasOutputCaching,
        bool useGlobalErrorHandler)
    {
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public class ApiMiddlewareOptions");
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

        GenerateApiMiddlewareOptions(builder, projectName, hasRateLimiting, hasSecurity, hasOutputCaching, useGlobalErrorHandler);

        return builder.ToString();
    }

    /// <summary>
    /// Generates the ApiServiceOptions class.
    /// </summary>
    private static void GenerateApiServiceOptions(
        StringBuilder builder,
        string projectName,
        bool hasRateLimiting,
        bool hasVersioning,
        bool hasSecurity,
        bool hasOutputCaching)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Options for configuring {projectName} API services.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("/// <remarks>");
        builder.AppendLine("/// Note: Handler registration (AddApiHandlersFromDomain) and validation");
        builder.AppendLine("/// (AddApiValidatorsFromDomain) must be registered separately from your Domain project.");
        builder.AppendLine("/// </remarks>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public class ApiServiceOptions");
        builder.AppendLine("{");

        // UseRateLimiting - auto-detected
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Enable rate limiting policies. Auto-detected from x-ratelimit-* extensions.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, $"public bool UseRateLimiting {{ get; set; }} = {BoolToString(hasRateLimiting)};");
        builder.AppendLine();

        if (hasOutputCaching)
        {
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Register the output cache policies (<c>AddApiOutputCaching()</c>). Auto-detected from x-cache-type: output.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public bool UseOutputCaching { get; set; } = true;");
            builder.AppendLine();
        }

        // UseVersioning - auto-detected
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Enable API versioning. Auto-detected from versioningStrategy config.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, $"public bool UseVersioning {{ get; set; }} = {BoolToString(hasVersioning)};");
        builder.AppendLine();

        // UseSecurity - auto-detected
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Enable security policies. Auto-detected from security schemes.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, $"public bool UseSecurity {{ get; set; }} = {BoolToString(hasSecurity)};");
        builder.AppendLine();

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
        string projectName,
        bool hasRateLimiting,
        bool hasSecurity,
        bool hasOutputCaching,
        bool useGlobalErrorHandler)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Options for configuring {projectName} API middleware pipeline.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public class ApiMiddlewareOptions");
        builder.AppendLine("{");

        // UseRateLimiter - auto-detected
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Enable rate limiter middleware. Auto-detected from x-ratelimit-* extensions.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, $"public bool UseRateLimiter {{ get; set; }} = {BoolToString(hasRateLimiting)};");
        builder.AppendLine();

        if (hasOutputCaching)
        {
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// Enable the output cache middleware (<c>UseOutputCache()</c>, after authentication: an authenticated");
            builder.AppendLine(4, "/// caller's answer is never cached). Auto-detected from x-cache-type: output.");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public bool UseOutputCache { get; set; } = true;");
            builder.AppendLine();
        }

        // UseAuthentication - auto-detected
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Enable authentication middleware. Auto-detected from security schemes.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, $"public bool UseAuthentication {{ get; set; }} = {BoolToString(hasSecurity)};");
        builder.AppendLine();

        // UseAuthorization - auto-detected
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Enable authorization middleware. Auto-detected from security schemes.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, $"public bool UseAuthorization {{ get; set; }} = {BoolToString(hasSecurity)};");
        builder.AppendLine();

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

    /// <summary>
    /// Converts a boolean to lowercase string for C# code generation.
    /// </summary>
    private static string BoolToString(bool value)
        => value ? "true" : "false";
}