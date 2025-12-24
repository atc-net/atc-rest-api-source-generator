namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts parameters for generating API options classes (ApiServiceOptions, ApiMiddlewareOptions).
/// These options enable simplified API surface while allowing granular control.
/// </summary>
public static class ApiOptionsExtractor
{
    /// <summary>
    /// Generates the ApiServiceOptions and ApiMiddlewareOptions classes.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document for auto-detection.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="config">The server configuration for versioning and validation settings.</param>
    /// <returns>Generated code content for the options classes.</returns>
    public static string Extract(
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
        var hasValidation = config.UseValidationFilter != MinimalApiPackageMode.Disabled;
        var hasVersioning = config.VersioningStrategy != VersioningStrategyType.None;
        var hasSecurity = openApiDoc.HasSecuritySchemes() || openApiDoc.HasJwtBearerSecurity();

        return GenerateFileContent(projectName, hasRateLimiting, hasValidation, hasVersioning, hasSecurity);
    }

    /// <summary>
    /// Generates the complete file content for both options classes.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        bool hasRateLimiting,
        bool hasValidation,
        bool hasVersioning,
        bool hasSecurity)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public class ApiServiceOptions");
        contentBuilder.AppendLine("public class ApiMiddlewareOptions");
        contentBuilder.AppendLine("public Action<GlobalErrorHandlingOptions>? ConfigureErrorHandling { get; set; }");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, "System.CodeDom.Compiler"));
        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated;");
        builder.AppendLine();

        // Generate ApiServiceOptions class
        GenerateApiServiceOptions(builder, projectName, hasRateLimiting, hasValidation, hasVersioning, hasSecurity);

        builder.AppendLine();

        // Generate ApiMiddlewareOptions class
        GenerateApiMiddlewareOptions(builder, projectName, hasRateLimiting, hasSecurity);

        return builder.ToString();
    }

    /// <summary>
    /// Generates the ApiServiceOptions class.
    /// </summary>
    private static void GenerateApiServiceOptions(
        StringBuilder builder,
        string projectName,
        bool hasRateLimiting,
        bool hasValidation,
        bool hasVersioning,
        bool hasSecurity)
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

        builder.AppendLine("}");
    }

    /// <summary>
    /// Generates the ApiMiddlewareOptions class.
    /// </summary>
    private static void GenerateApiMiddlewareOptions(
        StringBuilder builder,
        string projectName,
        bool hasRateLimiting,
        bool hasSecurity)
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

        // ConfigureErrorHandling - always available
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Configure global error handling options.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "public Action<GlobalErrorHandlingOptions>? ConfigureErrorHandling { get; set; }");

        builder.AppendLine("}");
    }

    /// <summary>
    /// Converts a boolean to lowercase string for C# code generation.
    /// </summary>
    private static string BoolToString(bool value)
        => value ? "true" : "false";
}