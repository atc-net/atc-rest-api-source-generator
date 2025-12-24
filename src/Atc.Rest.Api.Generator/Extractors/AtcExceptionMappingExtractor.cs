// ReSharper disable ConvertIfStatementToReturnStatement
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts ATC exception mapping middleware and extension methods.
/// TEMPORARY: Waiting for feature in Atc.Rest.MinimalApi (GitHub issue #22)
/// See: https://github.com/atc-net/atc-rest-minimalapi/issues/22
/// Remove this extractor when Atc.Rest.MinimalApi adds MapException&lt;T&gt;() support.
/// </summary>
public static class AtcExceptionMappingExtractor
{
    /// <summary>
    /// Extracts the AtcExceptionMappingOptions class parameters.
    /// </summary>
    /// <param name="projectName">The project name for namespace.</param>
    /// <returns>The generated file content as a string.</returns>
    public static string ExtractOptions(string projectName)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("public sealed class AtcExceptionMappingOptions");
        contentBuilder.AppendLine("public Dictionary<Type, int> ExceptionMappings { get; } = new();");
        contentBuilder.AppendLine("public AtcExceptionMappingOptions MapException<TException>(int statusCode) where TException : Exception");
        var content = contentBuilder.ToString();

        var sb = new StringBuilder();

        // Header
        sb.Append(UsingStatementHelper.BuildHeader(content));
        sb.AppendLine($"namespace {projectName}.Generated.Middleware;");
        sb.AppendLine();

        // Class documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Options for ATC exception-to-status-code mapping.");
        sb.AppendLine("/// TEMPORARY: Will be replaced by Atc.Rest.MinimalApi feature.");
        sb.AppendLine("/// </summary>");

        // Class definition
        sb.AppendLine("public sealed class AtcExceptionMappingOptions");
        sb.AppendLine("{");

        // ExceptionMappings property
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Gets the dictionary of exception type to HTTP status code mappings.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public Dictionary<Type, int> ExceptionMappings { get; } = new();");
        sb.AppendLine();

        // MapException method
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Maps an exception type to an HTTP status code.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "/// <typeparam name=\"TException\">The exception type to map.</typeparam>");
        sb.AppendLine(4, "/// <param name=\"statusCode\">The HTTP status code to return for this exception.</param>");
        sb.AppendLine(4, "/// <returns>This options instance for method chaining.</returns>");
        sb.AppendLine(4, "public AtcExceptionMappingOptions MapException<TException>(int statusCode)");
        sb.AppendLine(8, "where TException : Exception");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "ExceptionMappings[typeof(TException)] = statusCode;");
        sb.AppendLine(8, "return this;");
        sb.AppendLine(4, "}");

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Extracts the AtcExceptionMappingMiddleware class parameters.
    /// </summary>
    /// <param name="projectName">The project name for namespace.</param>
    /// <returns>The generated file content as a string.</returns>
    public static string ExtractMiddleware(string projectName)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("public sealed class AtcExceptionMappingMiddleware");
        contentBuilder.AppendLine("private readonly RequestDelegate next;");
        contentBuilder.AppendLine("private readonly ILogger<AtcExceptionMappingMiddleware> logger;");
        contentBuilder.AppendLine("IOptions<AtcExceptionMappingOptions> options,");
        contentBuilder.AppendLine("public async Task InvokeAsync(HttpContext context)");
        contentBuilder.AppendLine("catch (Exception ex)");
        var content = contentBuilder.ToString();

        var sb = new StringBuilder();

        // Header
        sb.Append(UsingStatementHelper.BuildHeader(content));
        sb.AppendLine($"namespace {projectName}.Generated.Middleware;");
        sb.AppendLine();

        // Class documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Middleware for ATC exception-to-status-code mapping.");
        sb.AppendLine("/// TEMPORARY: Will be replaced by Atc.Rest.MinimalApi feature.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine("/// This middleware sets the HTTP status code for mapped exceptions and rethrows,");
        sb.AppendLine("/// allowing GlobalErrorHandlingMiddleware to handle the response body.");
        sb.AppendLine("/// </remarks>");

        // Class definition
        sb.AppendLine("public sealed class AtcExceptionMappingMiddleware");
        sb.AppendLine("{");

        // Fields
        sb.AppendLine(4, "private readonly RequestDelegate next;");
        sb.AppendLine(4, "private readonly AtcExceptionMappingOptions options;");
        sb.AppendLine(4, "private readonly ILogger<AtcExceptionMappingMiddleware> logger;");
        sb.AppendLine();

        // Constructor
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Initializes a new instance of the <see cref=\"AtcExceptionMappingMiddleware\"/> class.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public AtcExceptionMappingMiddleware(");
        sb.AppendLine(8, "RequestDelegate next,");
        sb.AppendLine(8, "IOptions<AtcExceptionMappingOptions> options,");
        sb.AppendLine(8, "ILogger<AtcExceptionMappingMiddleware> logger)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "this.next = next;");
        sb.AppendLine(8, "this.options = options.Value;");
        sb.AppendLine(8, "this.logger = logger;");
        sb.AppendLine(4, "}");
        sb.AppendLine();

        // InvokeAsync method
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Invokes the middleware.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "/// <param name=\"context\">The HTTP context.</param>");
        sb.AppendLine(4, "/// <returns>A task representing the asynchronous operation.</returns>");
        sb.AppendLine(4, "public async Task InvokeAsync(HttpContext context)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "try");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "await next(context);");
        sb.AppendLine(8, "}");
        sb.AppendLine(8, "catch (Exception ex)");
        sb.AppendLine(8, "{");
        sb.AppendLine(12, "if (options.ExceptionMappings.TryGetValue(ex.GetType(), out var statusCode))");
        sb.AppendLine(12, "{");
        sb.AppendLine(16, "logger.LogWarning(");
        sb.AppendLine(20, "ex,");
        sb.AppendLine(20, "\"ATC Exception Mapping: {ExceptionType} mapped to HTTP {StatusCode}\",");
        sb.AppendLine(20, "ex.GetType().Name,");
        sb.AppendLine(20, "statusCode);");
        sb.AppendLine();
        sb.AppendLine(16, "context.Response.StatusCode = statusCode;");
        sb.AppendLine(12, "}");
        sb.AppendLine();
        sb.AppendLine(12, "// Rethrow for GlobalErrorHandlingMiddleware to handle response body");
        sb.AppendLine(12, "throw;");
        sb.AppendLine(8, "}");
        sb.AppendLine(4, "}");

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Extracts the AtcExceptionMappingExtensions class with DI and middleware registration.
    /// </summary>
    /// <param name="projectName">The project name for namespace.</param>
    /// <returns>The generated file content as a string.</returns>
    public static string ExtractExtensions(string projectName)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("public static class AtcExceptionMappingExtensions");
        contentBuilder.AppendLine("public static IServiceCollection AddAtcExceptionMapping(this IServiceCollection services, Action<AtcExceptionMappingOptions>? configure = null)");
        contentBuilder.AppendLine("public static WebApplication UseAtcExceptionMapping(this WebApplication app)");
        contentBuilder.AppendLine("app.UseMiddleware<AtcExceptionMappingMiddleware>();");
        var content = contentBuilder.ToString();

        var sb = new StringBuilder();

        // Header
        sb.Append(UsingStatementHelper.BuildHeader(content));
        sb.AppendLine($"namespace {projectName}.Generated.Middleware;");
        sb.AppendLine();

        // Class documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Extension methods for ATC exception mapping.");
        sb.AppendLine("/// TEMPORARY: Will be replaced by Atc.Rest.MinimalApi feature.");
        sb.AppendLine("/// </summary>");

        // Class definition
        sb.AppendLine("public static class AtcExceptionMappingExtensions");
        sb.AppendLine("{");

        // AddAtcExceptionMapping method
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Adds ATC exception mapping services to the service collection.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        sb.AppendLine(4, "/// <param name=\"configure\">Optional configuration action for exception mappings.</param>");
        sb.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        sb.AppendLine(4, "/// <example>");
        sb.AppendLine(4, "/// <code>");
        sb.AppendLine(4, "/// builder.Services.AddAtcExceptionMapping(options =>");
        sb.AppendLine(4, "/// {");
        sb.AppendLine(4, "///     options.MapException&lt;OrderNotFoundException&gt;(StatusCodes.Status404NotFound);");
        sb.AppendLine(4, "///     options.MapException&lt;DuplicateResourceException&gt;(StatusCodes.Status409Conflict);");
        sb.AppendLine(4, "/// });");
        sb.AppendLine(4, "/// </code>");
        sb.AppendLine(4, "/// </example>");
        sb.AppendLine(4, "public static IServiceCollection AddAtcExceptionMapping(");
        sb.AppendLine(8, "this IServiceCollection services,");
        sb.AppendLine(8, "Action<AtcExceptionMappingOptions>? configure = null)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "var options = new AtcExceptionMappingOptions();");
        sb.AppendLine(8, "configure?.Invoke(options);");
        sb.AppendLine(8, "services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));");
        sb.AppendLine(8, "return services;");
        sb.AppendLine(4, "}");
        sb.AppendLine();

        // UseAtcExceptionMapping method
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// Adds the ATC exception mapping middleware to the application pipeline.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "/// <param name=\"app\">The web application.</param>");
        sb.AppendLine(4, "/// <returns>The web application for method chaining.</returns>");
        sb.AppendLine(4, "/// <remarks>");
        sb.AppendLine(4, "/// This middleware should be added BEFORE UseGlobalErrorHandler() so that");
        sb.AppendLine(4, "/// custom exception mappings take precedence.");
        sb.AppendLine(4, "/// </remarks>");
        sb.AppendLine(4, "public static WebApplication UseAtcExceptionMapping(this WebApplication app)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "app.UseMiddleware<AtcExceptionMappingMiddleware>();");
        sb.AppendLine(8, "return app;");
        sb.AppendLine(4, "}");

        sb.AppendLine("}");

        return sb.ToString();
    }
}