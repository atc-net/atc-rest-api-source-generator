namespace Atc.Rest.Api.Generator.IntegrationTests.Helpers;

/// <summary>
/// Helper class for testing code generation against scenario files.
/// Uses CodeGenerationService directly without Roslyn dependencies.
/// </summary>
public static class GeneratorTestHelper
{
    /// <summary>
    /// Gets the base path for test scenarios (test/Scenarios/).
    /// </summary>
    public static string GetScenariosBasePath()
    {
        // When running tests, the working directory is typically the test project's bin folder
        // The scenario files are copied to Scenarios subfolder
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Scenarios");
    }

    /// <summary>
    /// Loads the OpenAPI document from the specified YAML path.
    /// </summary>
    public static OpenApiDocument LoadOpenApiDocument(string yamlPath)
    {
        var yamlContent = File.ReadAllText(yamlPath);
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, yamlPath);
        return document ?? throw new InvalidOperationException($"Failed to parse OpenAPI document at {yamlPath}");
    }

    /// <summary>
    /// Gets all generated server types with path information for folder-based output.
    /// </summary>
    public static IEnumerable<GeneratedType> GetServerTypesWithPaths(
        string yamlPath,
        string scenarioName)
    {
        var openApiDoc = LoadOpenApiDocument(yamlPath);
        var generatorType = CodeGenerationService.GeneratorType.Server;

        // Yield all server type categories

        // Polymorphic base types first (oneOf/anyOf abstract records)
        foreach (var type in CodeGenerationService.GeneratePolymorphicTypes(openApiDoc, scenarioName, generatorType))
        {
            yield return type;
        }

        // Regular model types (including variant records with inheritance)
        foreach (var type in CodeGenerationService.GenerateModels(openApiDoc, scenarioName, generatorType))
        {
            yield return type;
        }

        // Tuple types (prefixItems schemas - OpenAPI 3.1)
        foreach (var type in CodeGenerationService.GenerateTuples(openApiDoc, scenarioName, generatorType))
        {
            yield return type;
        }

        foreach (var type in CodeGenerationService.GenerateParameters(openApiDoc, scenarioName, generatorType))
        {
            yield return type;
        }

        // Generate ParsableList<T> helper type when any query parameter uses array types
        var parsableList = CodeGenerationService.GenerateParsableList(openApiDoc, scenarioName);
        if (parsableList != null)
        {
            yield return parsableList;
        }

        foreach (var type in CodeGenerationService.GenerateResults(openApiDoc, scenarioName, generatorType))
        {
            yield return type;
        }

        foreach (var type in CodeGenerationService.GenerateHandlerInterfaces(openApiDoc, scenarioName, generatorType))
        {
            yield return type;
        }

        // Use per-segment endpoint generation for proper file structure
        foreach (var endpoints in CodeGenerationService.GenerateEndpointsPerSegment(openApiDoc, scenarioName, generatorType))
        {
            yield return endpoints;
        }

        var di = CodeGenerationService.GenerateDependencyInjection(openApiDoc, scenarioName, generatorType);
        if (di != null)
        {
            yield return di;
        }

        // Output Caching generation
        var outputCachePolicies = CodeGenerationService.GenerateOutputCachePolicies(openApiDoc, scenarioName);
        if (outputCachePolicies != null)
        {
            yield return new GeneratedType(
                TypeName: "OutputCachePolicies",
                Category: "Caching",
                Namespace: $"{scenarioName}.Generated.Caching",
                Content: outputCachePolicies,
                RequiredUsings: ["System.CodeDom.Compiler"],
                GroupName: null,
                SubFolder: "Caching");
        }

        var outputCacheDi = CodeGenerationService.GenerateOutputCacheDependencyInjection(openApiDoc, scenarioName);
        if (outputCacheDi != null)
        {
            yield return new GeneratedType(
                TypeName: "OutputCachingServiceCollectionExtensions",
                Category: "Caching",
                Namespace: $"{scenarioName}.Generated.Caching",
                Content: outputCacheDi,
                RequiredUsings: ["System.CodeDom.Compiler", "Microsoft.AspNetCore.Authorization", "Microsoft.AspNetCore.Builder", "Microsoft.Extensions.DependencyInjection", $"{scenarioName}.Generated.Caching"],
                GroupName: null,
                SubFolder: "Caching");
        }

        // HybridCache generation
        var hybridCachePolicies = CodeGenerationService.GenerateHybridCachePolicies(openApiDoc, scenarioName);
        if (hybridCachePolicies != null)
        {
            yield return new GeneratedType(
                TypeName: "CachePolicies",
                Category: "Caching",
                Namespace: $"{scenarioName}.Generated.Caching",
                Content: hybridCachePolicies,
                RequiredUsings: ["System.CodeDom.Compiler"],
                GroupName: null,
                SubFolder: "Caching");
        }

        var hybridCacheDi = CodeGenerationService.GenerateHybridCacheDependencyInjection(openApiDoc, scenarioName);
        if (hybridCacheDi != null)
        {
            yield return new GeneratedType(
                TypeName: "HybridCachingServiceCollectionExtensions",
                Category: "Caching",
                Namespace: $"{scenarioName}.Generated.Caching",
                Content: hybridCacheDi,
                RequiredUsings: ["System.CodeDom.Compiler", "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.Caching.Hybrid", $"{scenarioName}.Generated.Caching"],
                GroupName: null,
                SubFolder: "Caching");
        }

        // Webhook generation (OpenAPI 3.1)
        foreach (var type in CodeGenerationService.GenerateWebhookHandlerInterfaces(openApiDoc, scenarioName))
        {
            yield return type;
        }

        foreach (var type in CodeGenerationService.GenerateWebhookParameters(openApiDoc, scenarioName))
        {
            yield return type;
        }

        foreach (var type in CodeGenerationService.GenerateWebhookResults(openApiDoc, scenarioName))
        {
            yield return type;
        }

        var webhookServerConfig = new ServerConfig();
        var webhookEndpoints = CodeGenerationService.GenerateWebhookEndpoints(openApiDoc, scenarioName, webhookServerConfig);
        if (webhookEndpoints != null)
        {
            yield return webhookEndpoints;
        }

        var webhookDi = CodeGenerationService.GenerateWebhookDependencyInjection(openApiDoc, scenarioName);
        if (webhookDi != null)
        {
            yield return webhookDi;
        }
    }

    /// <summary>
    /// Gets all generated client types with path information for folder-based output.
    /// Reads the marker file config to determine TypedClient vs EndpointPerOperation mode.
    /// </summary>
    public static IEnumerable<GeneratedType> GetClientTypesWithPaths(
        string yamlPath,
        string markerPath,
        string scenarioName)
    {
        var openApiDoc = LoadOpenApiDocument(yamlPath);
        var generatorType = CodeGenerationService.GeneratorType.Client;

        // Read client config to determine generation mode
        var config = LoadClientConfig(markerPath);
        var isEndpointPerOperation = config?.GenerationMode == GenerationModeType.EndpointPerOperation;

        // Polymorphic base types first (oneOf/anyOf abstract records)
        foreach (var type in CodeGenerationService.GeneratePolymorphicTypes(openApiDoc, scenarioName, generatorType))
        {
            yield return type;
        }

        // Yield models (including variant records with inheritance)
        foreach (var type in CodeGenerationService.GenerateModels(openApiDoc, scenarioName, generatorType))
        {
            yield return type;
        }

        // Tuple types (prefixItems schemas - OpenAPI 3.1)
        foreach (var type in CodeGenerationService.GenerateTuples(openApiDoc, scenarioName, generatorType))
        {
            yield return type;
        }

        // Yield parameters (client uses RequestParameters subfolder)
        foreach (var type in CodeGenerationService.GenerateParameters(openApiDoc, scenarioName, generatorType))
        {
            yield return type;
        }

        if (isEndpointPerOperation)
        {
            // EndpointPerOperation mode: generate endpoint classes, interfaces, results
            foreach (var type in CodeGenerationService.GenerateEndpointPerOperationFiles(openApiDoc, scenarioName, generatorType))
            {
                yield return type;
            }

            // Yield DI extension for EndpointPerOperation
            var di = CodeGenerationService.GenerateEndpointPerOperationDI(openApiDoc, scenarioName, generatorType);
            if (di != null)
            {
                yield return di;
            }
        }
        else
        {
            // TypedClient mode: generate HTTP client class and inline models
            foreach (var type in CodeGenerationService.GenerateHttpClient(openApiDoc, scenarioName, generatorType))
            {
                yield return type;
            }
        }
    }

    /// <summary>
    /// Gets all generated server domain types with path information for folder-based output.
    /// </summary>
    public static IEnumerable<GeneratedType> GetServerDomainTypesWithPaths(
        string yamlPath,
        string scenarioName)
    {
        var openApiDoc = LoadOpenApiDocument(yamlPath);
        var generatorType = CodeGenerationService.GeneratorType.ServerDomain;

        // Generate handler scaffolds
        if (openApiDoc.Paths != null)
        {
            foreach (var pathKvp in openApiDoc.Paths)
            {
                var pathKey = pathKvp.Key;
                if (pathKvp.Value is not OpenApiPathItem pathItem || pathItem.Operations == null)
                {
                    continue;
                }

                foreach (var operationKvp in pathItem.Operations)
                {
                    var operation = operationKvp.Value;
                    if (operation == null)
                    {
                        continue;
                    }

                    var operationId = operation.OperationId ?? GenerateOperationId(pathKey, operationKvp.Key.ToString());
                    var handlerName = operationId.EnsureFirstCharacterToUpper() + "Handler";
                    var groupName = CodeGenerationService.GetGroupingForOperation(operation, pathKey, SubFolderStrategyType.FirstPathSegment);
                    var subFolder = CodeGenerationService.GetSubFolder("Handlers", groupName, generatorType);

                    // Generate handler scaffold content
                    var content = GenerateHandlerScaffoldContent(handlerName, operationId);

                    var usings = new List<string>
                    {
                        "System",
                        "System.Threading",
                        "System.Threading.Tasks",
                        $"{scenarioName}.Generated.Models",
                        $"{scenarioName}.Generated.Parameters",
                        $"{scenarioName}.Generated.Results",
                    };

                    yield return new GeneratedType(
                        TypeName: handlerName,
                        Category: "Handlers",
                        Namespace: $"{scenarioName}.Domain.Handlers",
                        Content: content,
                        RequiredUsings: usings,
                        GroupName: groupName,
                        SubFolder: subFolder);
                }
            }
        }

        // Generate DI extension
        var diSubFolder = CodeGenerationService.GetSubFolder("DependencyInjection", null, generatorType);
        var diContent = GenerateDiExtensionContent(openApiDoc);

        var diUsings = new List<string>
        {
            "Microsoft.Extensions.DependencyInjection",
            $"{scenarioName}.Generated.Handlers",
        };

        yield return new GeneratedType(
            TypeName: "ServiceCollectionEndpointHandlerExtensions",
            Category: "DependencyInjection",
            Namespace: scenarioName,
            Content: diContent,
            RequiredUsings: diUsings,
            GroupName: null,
            SubFolder: diSubFolder);
    }

    /// <summary>
    /// Loads the client configuration from the marker file.
    /// </summary>
    private static ClientConfig? LoadClientConfig(string markerPath)
    {
        if (!File.Exists(markerPath))
        {
            return null;
        }

        try
        {
            var content = File.ReadAllText(markerPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            return JsonSerializer.Deserialize<ClientConfig>(content, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Generates handler scaffold content for ServerDomain.
    /// </summary>
    private static string GenerateHandlerScaffoldContent(
        string handlerName,
        string operationId)
    {
        var interfaceName = $"I{handlerName}";
        var resultType = $"{operationId.EnsureFirstCharacterToUpper()}Result";

        return $@"/// <summary>
/// Handler for operation request.
/// Operation: {operationId}.
/// </summary>
public sealed class {handlerName} : {interfaceName}
{{
    public Task<{resultType}> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {{
        throw new NotImplementedException(""{operationId} not implemented"");
    }}
}}";
    }

    /// <summary>
    /// Generates DI extension content for ServerDomain.
    /// </summary>
    private static string GenerateDiExtensionContent(
        OpenApiDocument? openApiDoc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Extension methods for registering API handlers in the dependency injection container.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class ServiceCollectionEndpointHandlerExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all API handler implementations from this assembly.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddApiHandlersFromDomain(this IServiceCollection services)");
        sb.AppendLine("    {");

        if (openApiDoc?.Paths != null)
        {
            foreach (var pathKvp in openApiDoc.Paths)
            {
                if (pathKvp.Value is not OpenApiPathItem pathItem || pathItem.Operations == null)
                {
                    continue;
                }

                foreach (var operationKvp in pathItem.Operations)
                {
                    var operation = operationKvp.Value;
                    if (operation == null)
                    {
                        continue;
                    }

                    var operationId = operation.OperationId ?? GenerateOperationId(pathKvp.Key, operationKvp.Key.ToString());
                    var handlerName = operationId.EnsureFirstCharacterToUpper() + "Handler";
                    var interfaceName = $"I{handlerName}";

                    sb.Append("        services.AddScoped<");
                    sb.Append(interfaceName);
                    sb.Append(", ");
                    sb.Append(handlerName);
                    sb.AppendLine(">();");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.Append('}');

        return sb.ToString();
    }

    /// <summary>
    /// Generates an operation ID from path and HTTP method.
    /// </summary>
    private static string GenerateOperationId(
        string path,
        string httpMethod)
    {
        var segments = path.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
        var name = string.Concat(
            segments.Select(s => s.StartsWith("{", StringComparison.Ordinal)
                ? "By" + s
                    .Trim('{', '}')
                    .EnsureFirstCharacterToUpper()
                : s.EnsureFirstCharacterToUpper()));

        return httpMethod.EnsureFirstCharacterToUpper() + name;
    }
}