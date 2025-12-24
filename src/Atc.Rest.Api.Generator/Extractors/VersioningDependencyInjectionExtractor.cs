namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts API versioning configuration and generates DI registration code.
/// Supports QueryString, UrlSegment, and Header versioning strategies.
/// </summary>
public static class VersioningDependencyInjectionExtractor
{
    /// <summary>
    /// Extracts versioning dependency injection class parameters based on configuration.
    /// </summary>
    /// <param name="projectName">The name of the project (used for namespace and method naming).</param>
    /// <param name="config">The server configuration containing versioning settings.</param>
    /// <returns>ClassParameters for the versioning DI registration class, or null if versioning is disabled.</returns>
    public static ClassParameters? Extract(
        string projectName,
        ServerConfig config)
    {
        if (config.VersioningStrategy == VersioningStrategyType.None)
        {
            return null;
        }

        // Generate the AddApiVersioning method content
        var methodContent = GenerateMethodContent(config);

        // Build method parameters
        var methodParams = new List<ParameterBaseParameters>
        {
            new(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: "this IServiceCollection",
                IsNullableType: false,
                IsReferenceType: true,
                Name: "services",
                DefaultValue: null),
        };

        var methodDocParams = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "services", "The service collection." },
        };

        var method = new MethodParameters(
            DocumentationTags: new CodeDocumentationTags(
                summary: GetMethodSummary(config.VersioningStrategy),
                parameters: methodDocParams,
                remark: null,
                code: null,
                example: null,
                exceptions: null,
                @return: "The service collection for method chaining."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: "IServiceCollection",
            Name: $"Add{projectName}ApiVersioning",
            Parameters: methodParams,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: methodContent);

        // Generate header content
        var headerContent = GenerateHeaderContent();

        return new ClassParameters(
            HeaderContent: headerContent,
            Namespace: $"{projectName}.Generated.DependencyInjection",
            DocumentationTags: new CodeDocumentationTags(
                summary: "Extension methods for configuring API versioning in the dependency injection container."),
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicStaticClass,
            ClassTypeName: "ApiVersioningServiceCollectionExtensions",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: new List<MethodParameters> { method },
            GenerateToStringMethod: false);
    }

    private static string GetMethodSummary(VersioningStrategyType strategy)
        => strategy switch
        {
            VersioningStrategyType.QueryString => "Configures API versioning using query string parameter (e.g., ?api-version=1.0).",
            VersioningStrategyType.UrlSegment => "Configures API versioning using URL path segment (e.g., /v1/pets).",
            VersioningStrategyType.Header => "Configures API versioning using HTTP header (e.g., X-Api-Version: 1.0).",
            _ => "Configures API versioning.",
        };

    private static string GenerateHeaderContent()
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("[GeneratedCode]");
        contentBuilder.AppendLine("public static IServiceCollection AddApiVersioning(");
        contentBuilder.AppendLine(4, "this IServiceCollection services)");
        contentBuilder.AppendLine("options.DefaultApiVersion = new ApiVersion(1, 0);");
        contentBuilder.AppendLine("options.ApiVersionReader = new QueryStringApiVersionReader(\"api-version\");");
        contentBuilder.AppendLine("options.ApiVersionReader = new UrlSegmentApiVersionReader();");
        contentBuilder.AppendLine("options.ApiVersionReader = new HeaderApiVersionReader(\"X-Api-Version\");");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        return UsingStatementHelper.BuildHeader(content, "System.CodeDom.Compiler");
    }

    private static string GenerateMethodContent(ServerConfig config)
    {
        var builder = new StringBuilder();

        // Parse default API version
        var versionParts = config.DefaultApiVersion.Split('.');
        var majorVersion = versionParts.Length > 0 ? versionParts[0] : "1";
        var minorVersion = versionParts.Length > 1 ? versionParts[1] : "0";

        builder.AppendLine("services.AddApiVersioning(options =>");
        builder.AppendLine("{");
        builder.AppendLine(4, $"options.DefaultApiVersion = new ApiVersion({majorVersion}, {minorVersion});");

        // AssumeDefaultVersionWhenUnspecified only applies to QueryString and Header
        if (config.VersioningStrategy != VersioningStrategyType.UrlSegment)
        {
            builder.AppendLine(4, $"options.AssumeDefaultVersionWhenUnspecified = {config.AssumeDefaultVersionWhenUnspecified.ToString().ToLowerInvariant()};");
        }

        builder.AppendLine(4, $"options.ReportApiVersions = {config.ReportApiVersions.ToString().ToLowerInvariant()};");

        // Configure ApiVersionReader based on strategy
        switch (config.VersioningStrategy)
        {
            case VersioningStrategyType.QueryString:
                builder.AppendLine(4, $"options.ApiVersionReader = new QueryStringApiVersionReader(\"{config.VersionQueryParameterName}\");");
                break;

            case VersioningStrategyType.UrlSegment:
                builder.AppendLine(4, "options.ApiVersionReader = new UrlSegmentApiVersionReader();");
                break;

            case VersioningStrategyType.Header:
                builder.AppendLine(4, $"options.ApiVersionReader = new HeaderApiVersionReader(\"{config.VersionHeaderName}\");");
                break;
        }

        builder.AppendLine("});");
        builder.AppendLine();
        builder.Append("return services;");

        return builder.ToString();
    }
}