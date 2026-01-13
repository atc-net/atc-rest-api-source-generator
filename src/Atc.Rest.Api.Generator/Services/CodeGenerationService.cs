namespace Atc.Rest.Api.Generator.Services;

/// <summary>
/// Service for generating code from OpenAPI documents.
/// Provides methods to generate individual types or combined files.
/// </summary>
public static class CodeGenerationService
{
    /// <summary>
    /// Generator types for subfolder calculation.
    /// </summary>
    public enum GeneratorType
    {
        Server,
        Client,
        ServerDomain,
    }

    /// <summary>
    /// Sanitizes a project name for use in C# identifiers (class names, method names).
    /// Removes dots since they're not valid in identifiers.
    /// </summary>
    /// <param name="projectName">The project name that may contain dots.</param>
    /// <returns>A sanitized name suitable for use in C# identifiers.</returns>
    public static string SanitizeProjectNameForIdentifier(string projectName)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            return projectName;
        }

        // Remove dots from project names for use in identifiers
        return projectName.Replace(".", string.Empty);
    }

    private static readonly string[] ModelUsings =
    [
        "System.ComponentModel.DataAnnotations",
    ];

    private static readonly string[] PolymorphicModelUsings =
    [
        "System.CodeDom.Compiler",
        "System.Text.Json.Serialization",
    ];

    private static readonly string[] ParameterUsings =
    [
        "System.CodeDom.Compiler",
        "System.ComponentModel",
        "System.ComponentModel.DataAnnotations",
        "Microsoft.AspNetCore.Mvc",
    ];

    private static readonly string[] ResultUsings =
    [
        "System.CodeDom.Compiler",
        "System.Collections.Generic",
        "Microsoft.AspNetCore.Http",
        "Microsoft.AspNetCore.Mvc",
    ];

    private static readonly string[] HandlerUsings =
    [
        "System.CodeDom.Compiler",
        "System.Threading",
        "System.Threading.Tasks",
    ];

    private static readonly string[] EndpointUsings =
    [
        "System.Threading",
        "Microsoft.AspNetCore.Builder",
        "Microsoft.AspNetCore.Http",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Routing",
    ];

    private static readonly string[] DependencyInjectionUsings =
    [
        "Microsoft.Extensions.DependencyInjection",
    ];

    private static readonly string[] HttpClientUsings =
    [
        "System",
        "System.Net.Http",
        "System.Net.Http.Json",
        "System.Threading",
        "System.Threading.Tasks",
    ];

    /// <summary>
    /// Gets the grouping name for an operation based on the specified strategy.
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="path">The path of the operation.</param>
    /// <param name="strategy">The sub-folder strategy to use.</param>
    /// <returns>The grouping name, or null if strategy is None.</returns>
    public static string? GetGroupingForOperation(
        OpenApiOperation operation,
        string path,
        SubFolderStrategyType strategy)
        => strategy switch
        {
            SubFolderStrategyType.None => null,
            SubFolderStrategyType.FirstPathSegment => GetGroupNameFromPath(path),
            SubFolderStrategyType.OpenApiTag => GetOpenApiTagForOperation(operation) ?? GetGroupNameFromPath(path),
            _ => null,
        };

    /// <summary>
    /// Gets the OpenAPI tag from an operation (first tag only).
    /// </summary>
    private static string? GetOpenApiTagForOperation(OpenApiOperation operation)
    {
        if (operation.Tags is not { Count: > 0 })
        {
            return null;
        }

        var firstTag = operation.Tags.FirstOrDefault()?.Name;
        return string.IsNullOrEmpty(firstTag)
            ? null
            : firstTag!.ToPascalCaseForDotNet();
    }

    /// <summary>
    /// Gets the group name from an OpenAPI operation (using tag) or derives one from the path.
    /// Used internally for schema/operation group name lookup.
    /// </summary>
    private static string GetGroupNameForOperation(
        OpenApiOperation operation,
        string path)
    {
        if (operation.Tags is not { Count: > 0 })
        {
            return GetGroupNameFromPath(path);
        }

        var firstTag = operation.Tags.FirstOrDefault()?.Name;
        return string.IsNullOrEmpty(firstTag)
            ? GetGroupNameFromPath(path)
            : firstTag!.ToPascalCaseForDotNet();
    }

    /// <summary>
    /// Gets a group name from a path by extracting the first significant segment.
    /// </summary>
    public static string GetGroupNameFromPath(string path)
    {
        var segments = path.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            // Skip version segments like v1, v2
            if (segment.StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
                segment.Length > 1 &&
                char.IsDigit(segment[1]))
            {
                continue;
            }

            // Skip parameter segments like {id}
            if (segment.StartsWith("{", StringComparison.Ordinal))
            {
                continue;
            }

            // Capitalize and return
            return segment.ToPascalCaseForDotNet();
        }

        return "Api";
    }

    /// <summary>
    /// Gets the group name for a schema by finding which operations reference it.
    /// </summary>
    public static string? GetGroupNameForSchema(
        OpenApiDocument openApiDoc,
        string schemaName)
    {
        if (openApiDoc.Paths == null)
        {
            return null;
        }

        // Search through all operations to find which one references this schema
        foreach (var path in openApiDoc.Paths)
        {
            var pathKey = path.Key;
            if (path.Value is not OpenApiPathItem pathItem || pathItem.Operations == null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations)
            {
                var op = operation.Value;
                if (op == null)
                {
                    continue;
                }

                // Check request body
                if (op.RequestBody?.Content != null)
                {
                    foreach (var mediaType in op.RequestBody.Content.Values)
                    {
                        if (SchemaReferencesName(mediaType.Schema, schemaName))
                        {
                            return GetGroupNameForOperation(op, pathKey);
                        }
                    }
                }

                // Check responses
                if (op.Responses != null)
                {
                    foreach (var response in op.Responses.Values)
                    {
                        if (response is OpenApiResponse resp && resp.Content != null)
                        {
                            foreach (var mediaType in resp.Content.Values)
                            {
                                if (SchemaReferencesName(mediaType.Schema, schemaName))
                                {
                                    return GetGroupNameForOperation(op, pathKey);
                                }
                            }
                        }
                    }
                }

                // Check parameters
                if (op.Parameters != null)
                {
                    foreach (var param in op.Parameters)
                    {
                        if (param is OpenApiParameter p && SchemaReferencesName(p.Schema, schemaName))
                        {
                            return GetGroupNameForOperation(op, pathKey);
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Calculates the subfolder path for a generated type.
    /// </summary>
    public static string GetSubFolder(
        string category,
        string? groupName,
        GeneratorType generatorType)
    {
        var groupNamePart = string.IsNullOrEmpty(groupName) ? "Common" : groupName;

        return (generatorType, category) switch
        {
            // Server paths
            (GeneratorType.Server, "Models") => $"Contracts\\{groupNamePart}/Models",
            (GeneratorType.Server, "Parameters") => $"Contracts\\{groupNamePart}/Parameters",
            (GeneratorType.Server, "Results") => $"Contracts\\{groupNamePart}/Results",
            (GeneratorType.Server, "Handlers") => $"Contracts\\{groupNamePart}/Interfaces",
            (GeneratorType.Server, "Endpoints") => "Endpoints",
            (GeneratorType.Server, "DependencyInjection") => "Extensions",

            // Client paths
            (GeneratorType.Client, "Models") => $"Contracts\\{groupNamePart}",
            (GeneratorType.Client, "Parameters") => $"Contracts\\{groupNamePart}/RequestParameters",
            (GeneratorType.Client, "Client") => $"Endpoints\\{groupNamePart}",

            // ServerDomain paths
            (GeneratorType.ServerDomain, "Handlers") => $"Handlers\\{groupNamePart}",
            (GeneratorType.ServerDomain, "DependencyInjection") => "Extensions",

            // Default
            _ => category,
        };
    }

    private static bool SchemaReferencesName(
        IOpenApiSchema? schema,
        string schemaName)
    {
        if (schema == null)
        {
            return false;
        }

        // Check if this is a reference to the schema
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refId = schemaRef.Reference.Id;
            if (string.Equals(refId, schemaName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check array items
        if (schema is OpenApiSchema { Items: OpenApiSchemaReference itemsRef })
        {
            var refId = itemsRef.Reference.Id;
            if (string.Equals(refId, schemaName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the group name for an operation by its operation ID.
    /// </summary>
    private static string? GetGroupNameForOperationId(
        OpenApiDocument openApiDoc,
        string operationId)
    {
        if (openApiDoc.Paths == null || string.IsNullOrEmpty(operationId))
        {
            return null;
        }

        foreach (var path in openApiDoc.Paths)
        {
            if (path.Value is not OpenApiPathItem pathItem || pathItem.Operations == null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations)
            {
                var op = operation.Value;
                if (op == null)
                {
                    continue;
                }

                var opId = op.OperationId;
                if (string.Equals(opId, operationId, StringComparison.OrdinalIgnoreCase))
                {
                    return GetGroupNameForOperation(op, path.Key);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the operation ID from a generated type name.
    /// E.g., "ListPetsParameters" -> "ListPets", "ICreatePetsHandler" -> "CreatePets"
    /// </summary>
    private static string? ExtractOperationIdFromTypeName(
        string typeName,
        string suffix)
    {
        if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(suffix))
        {
            return null;
        }

        // Remove I prefix for interfaces
        var name = typeName;
        if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name.Substring(1);
        }

        // Remove suffix
        if (name.EndsWith(suffix, StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - suffix.Length);
        }

        return name;
    }

    /// <summary>
    /// Generates model types from OpenAPI schemas.
    /// Includes support for polymorphic types - variant records will inherit from their base types.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="generatorType">The generator type (Server or Client).</param>
    /// <param name="generatePartialModels">Whether to generate partial records for extensibility.</param>
    public static List<GeneratedType> GenerateModels(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Server,
        bool generatePartialModels = false)
    {
        var result = new List<GeneratedType>();

        // Extract polymorphic configurations for inheritance tracking
        var polymorphicConfigs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(openApiDoc);

        // Use polymorphism-aware extraction so variant types get inheritance
        var records = SchemaExtractor.ExtractIndividualWithPolymorphism(openApiDoc, polymorphicConfigs, generatePartialModels: generatePartialModels);

        if (records == null || records.Count == 0)
        {
            return result;
        }

        var @namespace = $"{projectName}.Generated.Models";
        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        foreach (var recordParams in records)
        {
            var content = GenerateRecordContentOnly(codeDocGenerator, recordParams);
            var usings = new List<string>(ModelUsings);

            // Add System.Collections.Generic if any property uses Dictionary type
            var usesDictionary = recordParams.Parameters.Any(p =>
                p.TypeName.StartsWith("Dictionary<", StringComparison.Ordinal));
            if (usesDictionary)
            {
                usings.Add("System.Collections.Generic");
            }

            // Determine group name from schema usage in operations
            var groupName = GetGroupNameForSchema(openApiDoc, recordParams.Name);
            var subFolder = GetSubFolder("Models", groupName, generatorType);

            result.Add(new GeneratedType(
                TypeName: recordParams.Name,
                Category: "Models",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: usings,
                GroupName: groupName,
                SubFolder: subFolder));
        }

        return result;
    }

    /// <summary>
    /// Generates tuple record types from OpenAPI schemas with prefixItems (JSON Schema 2020-12 / OpenAPI 3.1).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="generatorType">The generator type (Server or Client).</param>
    public static List<GeneratedType> GenerateTuples(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Server)
    {
        var result = new List<GeneratedType>();

        var tupleRecords = TupleExtractor.Extract(openApiDoc, projectName);

        if (tupleRecords == null || tupleRecords.Count == 0)
        {
            return result;
        }

        var @namespace = $"{projectName}.Generated.Models";
        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        foreach (var tupleParams in tupleRecords)
        {
            var content = GenerateRecordContentOnly(codeDocGenerator, tupleParams);

            // Determine group name from schema usage in operations
            var groupName = GetGroupNameForSchema(openApiDoc, tupleParams.Name);
            var subFolder = GetSubFolder("Models", groupName, generatorType);

            result.Add(new GeneratedType(
                TypeName: tupleParams.Name,
                Category: "Models",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: ModelUsings,
                GroupName: groupName,
                SubFolder: subFolder));
        }

        return result;
    }

    /// <summary>
    /// Generates record types for inline schemas discovered during endpoint extraction.
    /// Inline schemas are object types defined directly in responses/requests rather than as $ref.
    /// </summary>
    /// <param name="inlineSchemas">Dictionary of inline schemas discovered during endpoint extraction.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="generatorType">The generator type (Server or Client).</param>
    /// <returns>List of generated types for inline schemas.</returns>
    public static List<GeneratedType> GenerateInlineModels(
        Dictionary<string, EndpointPerOperationExtractor.InlineSchemaInfo> inlineSchemas,
        string projectName,
        GeneratorType generatorType = GeneratorType.Client)
        => GenerateInlineModelsInternal(
            inlineSchemas.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.PathSegment, kvp.Value.RecordParameters),
                StringComparer.Ordinal),
            projectName,
            generatorType);

    /// <summary>
    /// Generates record types for inline schemas from HttpClientExtractor.
    /// </summary>
    /// <param name="inlineSchemas">The dictionary of inline schemas discovered during extraction.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="generatorType">The generator type (Server or Client).</param>
    /// <returns>List of generated types for inline schemas.</returns>
    public static List<GeneratedType> GenerateInlineModels(
        Dictionary<string, HttpClientInlineSchemaInfo> inlineSchemas,
        string projectName,
        GeneratorType generatorType = GeneratorType.Client)
        => GenerateInlineModelsInternal(
            inlineSchemas.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.PathSegment, kvp.Value.RecordParameters),
                StringComparer.Ordinal),
            projectName,
            generatorType);

    /// <summary>
    /// Generates record types for inline schemas from ResultClassExtractor.
    /// </summary>
    /// <param name="inlineSchemas">The dictionary of inline schemas discovered during extraction.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="generatorType">The generator type (Server or Client).</param>
    /// <returns>List of generated types for inline schemas.</returns>
    public static List<GeneratedType> GenerateInlineModels(
        Dictionary<string, ResultClassInlineSchemaInfo> inlineSchemas,
        string projectName,
        GeneratorType generatorType = GeneratorType.Server)
        => GenerateInlineModelsInternal(
            inlineSchemas.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.PathSegment, kvp.Value.RecordParameters),
                StringComparer.Ordinal),
            projectName,
            generatorType);

    private static List<GeneratedType> GenerateInlineModelsInternal(
        Dictionary<string, (string PathSegment, RecordParameters RecordParameters)> inlineSchemas,
        string projectName,
        GeneratorType generatorType)
    {
        var result = new List<GeneratedType>();

        if (inlineSchemas == null || inlineSchemas.Count == 0)
        {
            return result;
        }

        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        foreach (var kvp in inlineSchemas)
        {
            var typeName = kvp.Key;
            var (pathSegment, recordParams) = kvp.Value;

            var @namespace = $"{projectName}.Generated.{pathSegment}.Models";
            var content = GenerateRecordContentOnly(codeDocGenerator, recordParams);
            var usings = new List<string>(ModelUsings);

            // Add System.Collections.Generic if any property uses Dictionary type
            var usesDictionary = recordParams.Parameters.Any(p =>
                p.TypeName.StartsWith("Dictionary<", StringComparison.Ordinal));
            if (usesDictionary)
            {
                usings.Add("System.Collections.Generic");
            }

            var subFolder = GetSubFolder("Models", pathSegment, generatorType);

            result.Add(new GeneratedType(
                TypeName: typeName,
                Category: "InlineModels",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: usings,
                GroupName: pathSegment,
                SubFolder: subFolder));
        }

        return result;
    }

    /// <summary>
    /// Generates polymorphic base types from OpenAPI oneOf/anyOf schemas.
    /// These are abstract records with [JsonPolymorphic] and [JsonDerivedType] attributes.
    /// </summary>
    public static List<GeneratedType> GeneratePolymorphicTypes(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Server)
    {
        var result = new List<GeneratedType>();
        var polymorphicConfigs = PolymorphicTypeExtractor.ExtractPolymorphicConfigs(openApiDoc);

        if (polymorphicConfigs == null || polymorphicConfigs.Count == 0)
        {
            return result;
        }

        var @namespace = $"{projectName}.Generated.Models";

        foreach (var kvp in polymorphicConfigs)
        {
            var schemaName = kvp.Key;
            var config = kvp.Value;

            // Generate the polymorphic base type content
            var content = PolymorphicTypeExtractor.GeneratePolymorphicBaseType(config, projectName);

            // Extract just the record declaration part (after the namespace declaration)
            var recordContent = ExtractRecordContentFromFullCode(content);

            // Determine group name from schema usage in operations
            var groupName = GetGroupNameForSchema(openApiDoc, schemaName);
            var subFolder = GetSubFolder("Models", groupName, generatorType);

            result.Add(new GeneratedType(
                TypeName: schemaName,
                Category: "Models",
                Namespace: @namespace,
                Content: recordContent,
                RequiredUsings: new List<string>(PolymorphicModelUsings),
                GroupName: groupName,
                SubFolder: subFolder));
        }

        return result;
    }

    /// <summary>
    /// Extracts the record declaration from full generated code (removes headers and namespace).
    /// </summary>
    private static string ExtractRecordContentFromFullCode(string fullCode)
    {
        // Find the namespace line and extract everything after it
        var lines = fullCode.Split('\n');
        var inNamespace = false;
        var contentLines = new List<string>();

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("namespace ", StringComparison.Ordinal))
            {
                inNamespace = true;
                continue;
            }

            if (inNamespace && !string.IsNullOrWhiteSpace(line))
            {
                contentLines.Add(line.TrimEnd('\r'));
            }
        }

        return string
            .Join("\n", contentLines)
            .TrimEnd();
    }

    /// <summary>
    /// Generates parameter types from OpenAPI operations.
    /// </summary>
    public static List<GeneratedType> GenerateParameters(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Server)
    {
        var result = new List<GeneratedType>();
        var records = OperationParameterExtractor.ExtractIndividual(openApiDoc, projectName);

        if (records == null || records.Count == 0)
        {
            return result;
        }

        var @namespace = $"{projectName}.Generated.Parameters";
        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        foreach (var recordParams in records)
        {
            var content = GenerateRecordContentOnly(codeDocGenerator, recordParams);
            var usings = new List<string>(ParameterUsings)
            {
                $"{projectName}.Generated.Models",
            };

            // Extract operation ID from type name to look up group name
            var operationId = ExtractOperationIdFromTypeName(recordParams.Name, "Parameters");
            var groupName = GetGroupNameForOperationId(openApiDoc, operationId ?? string.Empty);
            var subFolder = GetSubFolder("Parameters", groupName, generatorType);

            result.Add(new GeneratedType(
                TypeName: recordParams.Name,
                Category: "Parameters",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: usings,
                GroupName: groupName,
                SubFolder: subFolder));
        }

        return result;
    }

    /// <summary>
    /// Generates result types from OpenAPI operations.
    /// </summary>
    public static List<GeneratedType> GenerateResults(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Server)
    {
        var result = new List<GeneratedType>();
        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);

        // Build conflict registry to handle types like "Task" that conflict with System.Threading.Tasks.Task
        var registry = TypeConflictRegistry.Build(openApiDoc, projectName);

        // Use ExtractWithInlineSchemas to also capture inline object schemas in responses
        var (classes, inlineSchemas) = ResultClassExtractor.ExtractWithInlineSchemas(
            openApiDoc,
            projectName,
            pathSegment: null,
            registry,
            systemTypeResolver,
            includeDeprecated: false);

        // Add inline model types first (they may be referenced by result classes)
        if (inlineSchemas.Count > 0)
        {
            var inlineTypes = GenerateInlineModels(inlineSchemas, projectName, generatorType);
            result.AddRange(inlineTypes);
        }

        if (classes == null || classes.Count == 0)
        {
            return result;
        }

        var @namespace = $"{projectName}.Generated.Results";
        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        foreach (var classParams in classes)
        {
            var content = GenerateClassContentOnly(codeDocGenerator, classParams);
            var usings = new List<string>(ResultUsings)
            {
                $"{projectName}.Generated.Models",
            };

            // Extract operation ID from type name to look up group name
            var operationId = ExtractOperationIdFromTypeName(classParams.ClassTypeName, "Result");
            var groupName = GetGroupNameForOperationId(openApiDoc, operationId ?? string.Empty);
            var subFolder = GetSubFolder("Results", groupName, generatorType);

            // If there are inline schemas for this group, add the Models namespace
            if (inlineSchemas.Values.Any(s => s.PathSegment.Equals(groupName, StringComparison.OrdinalIgnoreCase)))
            {
                usings.Add($"{projectName}.Generated.{groupName}.Models");
            }

            result.Add(new GeneratedType(
                TypeName: classParams.ClassTypeName,
                Category: "Results",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: usings,
                GroupName: groupName,
                SubFolder: subFolder));
        }

        return result;
    }

    /// <summary>
    /// Generates handler interfaces (e.g., IListPetsHandler) from OpenAPI operations.
    /// </summary>
    public static List<GeneratedType> GenerateHandlerInterfaces(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Server)
    {
        var result = new List<GeneratedType>();
        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);
        var interfaces = HandlerExtractor.Extract(openApiDoc, projectName, systemTypeResolver);

        if (interfaces == null || interfaces.Count == 0)
        {
            return result;
        }

        var @namespace = $"{projectName}.Generated.Handlers";
        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        foreach (var interfaceParams in interfaces)
        {
            var content = GenerateInterfaceContentOnly(codeDocGenerator, interfaceParams);
            var usings = new List<string>(HandlerUsings)
            {
                $"{projectName}.Generated.Models",
                $"{projectName}.Generated.Parameters",
                $"{projectName}.Generated.Results",
            };

            // Extract operation ID from type name to look up group name
            var operationId = ExtractOperationIdFromTypeName(interfaceParams.InterfaceTypeName, "Handler");
            var groupName = GetGroupNameForOperationId(openApiDoc, operationId ?? string.Empty);
            var subFolder = GetSubFolder("Handlers", groupName, generatorType);

            result.Add(new GeneratedType(
                TypeName: interfaceParams.InterfaceTypeName,
                Category: "Handlers",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: usings,
                GroupName: groupName,
                SubFolder: subFolder));
        }

        return result;
    }

    /// <summary>
    /// Generates endpoint registration class from OpenAPI operations.
    /// </summary>
    public static GeneratedType? GenerateEndpoints(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Server)
    {
        var classParams = EndpointRegistrationExtractor.Extract(openApiDoc, projectName);

        if (classParams == null)
        {
            return null;
        }

        var @namespace = $"{projectName}.Generated.Endpoints";
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var content = GenerateClassContentOnly(codeDocGenerator, classParams);

        var usings = new List<string>(EndpointUsings)
        {
            $"{projectName}.Generated.Handlers",
            $"{projectName}.Generated.Models",
            $"{projectName}.Generated.Parameters",
        };

        var subFolder = GetSubFolder("Endpoints", null, generatorType);

        return new GeneratedType(
            TypeName: classParams.ClassTypeName,
            Category: "Endpoints",
            Namespace: @namespace,
            Content: content,
            RequiredUsings: usings,
            GroupName: null,
            SubFolder: subFolder);
    }

    /// <summary>
    /// Generates per-segment endpoint registration files and a combined mapping file.
    /// Each path segment gets its own file with IEndpointDefinition, definition classes, and extension method.
    /// Also generates a combined EndpointMappingExtensions file that calls all segment mappings.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="generatorType">The generator type for subfolder calculation.</param>
    /// <returns>A collection of GeneratedType for each segment and the combined mapping.</returns>
    public static IEnumerable<GeneratedType> GenerateEndpointsPerSegment(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Server)
    {
        if (openApiDoc?.Paths == null || openApiDoc.Paths.Count == 0)
        {
            yield break;
        }

        // Get all unique path segments
        var pathSegments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);

        if (pathSegments.Count == 0)
        {
            yield break;
        }

        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);

        // Generate per-segment endpoint files
        foreach (var pathSegment in pathSegments)
        {
            var content = GeneratePerSegmentEndpointContent(
                openApiDoc,
                projectName,
                pathSegment,
                codeDocGenerator,
                systemTypeResolver);

            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            var subFolder = GetSubFolder("Endpoints", pathSegment, generatorType);

            yield return new GeneratedType(
                TypeName: $"{pathSegment}Endpoints",
                Category: "Endpoints",
                Namespace: $"{projectName}.Generated.{pathSegment}.Endpoints",
                Content: content,
                RequiredUsings: [], // Content already includes usings
                GroupName: pathSegment,
                SubFolder: subFolder);
        }

        // Generate combined endpoint mapping file
        var mappingContent = GenerateCombinedEndpointMappingContent(projectName, pathSegments);

        if (!string.IsNullOrEmpty(mappingContent))
        {
            var mappingSubFolder = GetSubFolder("Endpoints", null, generatorType);

            yield return new GeneratedType(
                TypeName: "EndpointMappingExtensions",
                Category: "EndpointMapping",
                Namespace: $"{projectName}.Generated.Endpoints",
                Content: mappingContent,
                RequiredUsings: [], // Content already includes usings
                GroupName: null,
                SubFolder: mappingSubFolder);
        }
    }

    /// <summary>
    /// Generates the content for a per-segment endpoint file.
    /// Includes IEndpointDefinition interface, endpoint definition classes, and extension method.
    /// </summary>
    private static string GeneratePerSegmentEndpointContent(
        OpenApiDocument openApiDoc,
        string projectName,
        string pathSegment,
        ICodeDocumentationTagsGenerator codeDocGenerator,
        SystemTypeConflictResolver systemTypeResolver)
    {
        // Extract endpoint definitions for this segment
        // Note: useServersBasePath: false for backward compatibility with CLI/test harness
        // Source generators pass the config value from marker files
        var (interfaceParams, classParameters) = EndpointDefinitionExtractor.Extract(
            openApiDoc,
            projectName,
            pathSegment,
            registry: null,
            systemTypeResolver,
            SubFolderStrategyType.FirstPathSegment,
            includeDeprecated: false,
            useMinimalApiPackage: false,
            useValidationFilter: false,
            versioningStrategy: VersioningStrategyType.None,
            defaultApiVersion: null,
            useServersBasePath: false);

        if (interfaceParams == null && (classParameters == null || classParameters.Count == 0))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        // Add header
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        // Get namespace availability for this segment
        var namespaces = PathSegmentHelper.GetPathSegmentNamespaces(openApiDoc, pathSegment);

        builder.AppendLine("using System.CodeDom.Compiler;");
        builder.AppendLine("using System.Threading;");
        builder.AppendLine("using System.Threading.Tasks;");
        builder.AppendLine("using Microsoft.AspNetCore.Builder;");
        builder.AppendLine("using Microsoft.AspNetCore.Http;");
        builder.AppendLine("using Microsoft.AspNetCore.Mvc;");

        // Add conditional segment namespace usings
        builder.AppendSegmentUsings(projectName, pathSegment, namespaces);

        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.{pathSegment}.Endpoints;");
        builder.AppendLine();

        // Generate IEndpointDefinition interface
        if (interfaceParams != null)
        {
            var interfaceContent = GenerateInterfaceContentOnly(codeDocGenerator, interfaceParams);
            builder.AppendLine(interfaceContent);
            builder.AppendLine();
        }

        // Generate endpoint definition classes and collect class names
        var endpointDefinitionClassNames = new List<string>();
        if (classParameters != null)
        {
            foreach (var classParams in classParameters)
            {
                endpointDefinitionClassNames.Add(classParams.ClassTypeName);
                var classContent = GenerateClassContentOnly(codeDocGenerator, classParams);
                builder.AppendLine(classContent);
                builder.AppendLine();
            }
        }

        // Generate extension method class
        var extensionParams = EndpointRegistrationExtractor.ExtractEndpointMappingExtension(
            projectName,
            pathSegment,
            endpointDefinitionClassNames);

        if (extensionParams != null)
        {
            var extensionContent = GenerateClassContentOnly(codeDocGenerator, extensionParams);
            builder.Append(extensionContent);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Generates the content for the combined endpoint mapping file.
    /// This calls all per-segment Map{Segment}Endpoints methods.
    /// </summary>
    private static string GenerateCombinedEndpointMappingContent(
        string projectName,
        List<string> pathSegments)
    {
        if (pathSegments.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.CodeDom.Compiler;");
        builder.AppendLine("using Microsoft.AspNetCore.Builder;");
        builder.AppendLine();

        // Add usings for each path segment's endpoints namespace
        foreach (var segment in pathSegments.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"using {projectName}.Generated.{segment}.Endpoints;");
        }

        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.Endpoints;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Extension methods for mapping all API endpoints.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class EndpointMappingExtensions");
        builder.AppendLine("{");
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Maps all API endpoints from all path segments.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"app\">The web application.</param>");
        builder.AppendLine(4, "/// <returns>The web application for method chaining.</returns>");
        builder.AppendLine(4, "public static WebApplication MapEndpoints(this WebApplication app)");
        builder.AppendLine(4, "{");

        // Call each path segment's endpoint mapping method
        foreach (var segment in pathSegments.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(8, $"app.Map{segment}Endpoints();");
        }

        builder.AppendLine();
        builder.AppendLine(8, "return app;");
        builder.AppendLine(4, "}");
        builder.Append('}');

        return builder.ToString();
    }

    /// <summary>
    /// Generates dependency injection registration class from OpenAPI operations.
    /// </summary>
    public static GeneratedType? GenerateDependencyInjection(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Server)
    {
        var classParams = ServerDependencyInjectionExtractor.Extract(openApiDoc, projectName);

        if (classParams == null)
        {
            return null;
        }

        var @namespace = $"{projectName}.Generated.DependencyInjection";
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var content = GenerateClassContentOnly(codeDocGenerator, classParams);

        var usings = new List<string>(DependencyInjectionUsings)
        {
            $"{projectName}.Generated.Handlers",
        };

        var subFolder = GetSubFolder("DependencyInjection", null, generatorType);

        return new GeneratedType(
            TypeName: classParams.ClassTypeName,
            Category: "DependencyInjection",
            Namespace: @namespace,
            Content: content,
            RequiredUsings: usings,
            GroupName: null,
            SubFolder: subFolder);
    }

    /// <summary>
    /// Generates HTTP client class from OpenAPI operations, including inline model types.
    /// </summary>
    public static List<GeneratedType> GenerateHttpClient(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Client)
    {
        var result = new List<GeneratedType>();
        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);

        // Use ExtractWithInlineSchemas to also capture inline object schemas in responses
        // Note: useServersBasePath: false for backward compatibility with CLI/test harness
        var (classParams, inlineSchemas) = HttpClientExtractor.ExtractWithInlineSchemas(
            openApiDoc,
            projectName,
            pathSegment: null,
            registry: null,
            systemTypeResolver,
            includeDeprecated: false,
            useServersBasePath: false);

        // Add inline model types first (they may be referenced by the client class)
        if (inlineSchemas.Count > 0)
        {
            var inlineTypes = GenerateInlineModels(inlineSchemas, projectName, generatorType);
            result.AddRange(inlineTypes);
        }

        if (classParams == null)
        {
            return result;
        }

        var @namespace = $"{projectName}.Generated.Client";
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var content = GenerateClassContentOnly(codeDocGenerator, classParams);

        var usings = new List<string>(HttpClientUsings)
        {
            $"{projectName}.Generated.Models",
        };

        // Add using for inline models namespace if any
        var inlineNamespaces = inlineSchemas.Values
            .Select(s => $"{projectName}.Generated.{s.PathSegment}.Models")
            .Distinct(StringComparer.Ordinal);
        usings.AddRange(inlineNamespaces);

        var subFolder = GetSubFolder("Client", null, generatorType);

        result.Add(new GeneratedType(
            TypeName: classParams.ClassTypeName,
            Category: "Client",
            Namespace: @namespace,
            Content: content,
            RequiredUsings: usings,
            GroupName: null,
            SubFolder: subFolder));

        return result;
    }

    /// <summary>
    /// Generates EndpointPerOperation files for all operations in the OpenAPI document.
    /// Returns endpoint interfaces, endpoint classes, result interfaces, and result classes.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="generatorType">The generator type.</param>
    /// <param name="customErrorTypeName">Optional custom error type name (replaces ProblemDetails).</param>
    /// <param name="customHttpClientName">Optional custom HTTP client name.</param>
    public static List<GeneratedType> GenerateEndpointPerOperationFiles(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Client,
        string? customErrorTypeName = null,
        string? customHttpClientName = null)
    {
        var result = new List<GeneratedType>();

        if (openApiDoc.Paths == null || openApiDoc.Paths.Count == 0)
        {
            return result;
        }

        // Get all unique path segments
        var pathSegments = openApiDoc.Paths
            .Select(p => PathSegmentHelper.GetFirstPathSegment(p.Key))
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var pathSegment in pathSegments)
        {
            // Note: useServersBasePath: false for backward compatibility with CLI/test harness
            var (operationFiles, inlineSchemas) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
                openApiDoc,
                projectName,
                pathSegment,
                registry: null,
                includeDeprecated: false,
                customErrorTypeName: customErrorTypeName,
                customHttpClientName: customHttpClientName,
                useServersBasePath: false);

            // Add inline model types first
            if (inlineSchemas.Count > 0)
            {
                var inlineTypes = GenerateInlineModels(inlineSchemas, projectName, generatorType);
                result.AddRange(inlineTypes);
            }

            foreach (var opFiles in operationFiles)
            {
                // Endpoint Interface
                result.Add(new GeneratedType(
                    TypeName: $"I{opFiles.OperationName}Endpoint",
                    Category: "EndpointInterface",
                    Namespace: $"{projectName}.Generated.{pathSegment}.Endpoints.Interfaces",
                    Content: opFiles.EndpointInterfaceContent,
                    RequiredUsings: [],
                    GroupName: pathSegment,
                    SubFolder: $"Endpoints\\{pathSegment}\\Interfaces"));

                // Endpoint Class
                result.Add(new GeneratedType(
                    TypeName: $"{opFiles.OperationName}Endpoint",
                    Category: "EndpointClass",
                    Namespace: $"{projectName}.Generated.{pathSegment}.Endpoints",
                    Content: opFiles.EndpointClassContent,
                    RequiredUsings: [],
                    GroupName: pathSegment,
                    SubFolder: $"Endpoints\\{pathSegment}"));

                // Result Interface (null for binary endpoints that use BinaryEndpointResponse)
                if (opFiles.ResultInterfaceContent != null)
                {
                    result.Add(new GeneratedType(
                        TypeName: $"I{opFiles.OperationName}EndpointResult",
                        Category: "ResultInterface",
                        Namespace: $"{projectName}.Generated.{pathSegment}.Endpoints.Interfaces",
                        Content: opFiles.ResultInterfaceContent,
                        RequiredUsings: [],
                        GroupName: pathSegment,
                        SubFolder: $"Endpoints\\{pathSegment}\\Interfaces"));
                }

                // Result Class (null for binary endpoints that use BinaryEndpointResponse)
                if (opFiles.ResultClassContent != null)
                {
                    result.Add(new GeneratedType(
                        TypeName: $"{opFiles.OperationName}EndpointResult",
                        Category: "ResultClass",
                        Namespace: $"{projectName}.Generated.{pathSegment}.Endpoints.Results",
                        Content: opFiles.ResultClassContent,
                        RequiredUsings: [],
                        GroupName: pathSegment,
                        SubFolder: $"Endpoints\\{pathSegment}\\Results"));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Generates DI extension for EndpointPerOperation mode.
    /// </summary>
    public static GeneratedType? GenerateEndpointPerOperationDI(
        OpenApiDocument openApiDoc,
        string projectName,
        GeneratorType generatorType = GeneratorType.Client)
    {
        if (openApiDoc.Paths == null || openApiDoc.Paths.Count == 0)
        {
            return null;
        }

        // Get all unique path segments
        var pathSegments = openApiDoc.Paths
            .Select(p => PathSegmentHelper.GetFirstPathSegment(p.Key))
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (pathSegments.Count == 0)
        {
            return null;
        }

        // Generate consolidated DI extension content directly
        var diContent = GenerateConsolidatedDiExtensionContent(projectName, pathSegments);
        var sanitizedProjectName = SanitizeProjectNameForIdentifier(projectName);

        return new GeneratedType(
            TypeName: $"{sanitizedProjectName}EndpointsServiceCollectionExtensions",
            Category: "DependencyInjection",
            Namespace: $"{projectName}.Generated",
            Content: diContent,
            RequiredUsings: [],
            GroupName: null,
            SubFolder: "DependencyInjection");
    }

    /// <summary>
    /// Generates the consolidated DI extension content for EndpointPerOperation mode.
    /// </summary>
    private static string GenerateConsolidatedDiExtensionContent(
        string projectName,
        List<string> pathSegments)
    {
        var sanitizedProjectName = SanitizeProjectNameForIdentifier(projectName);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");

        // Add using statements for each path segment namespace
        foreach (var pathSegment in pathSegments)
        {
            sb.AppendLine($"using {projectName}.Generated.{pathSegment};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {projectName}.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Extension methods for registering all {projectName} API endpoints in the dependency injection container.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public static class {sanitizedProjectName}EndpointsServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, $"/// Registers all {projectName} API endpoint implementations from all path segments.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        sb.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        sb.AppendLine(4, $"public static IServiceCollection Add{sanitizedProjectName}Endpoints(this IServiceCollection services)");
        sb.AppendLine(4, "{");

        // Call each path segment's extension method
        foreach (var pathSegment in pathSegments)
        {
            sb.AppendLine(8, $"services.Add{pathSegment}Endpoints();");
        }

        sb.AppendLine();
        sb.AppendLine(8, "return services;");
        sb.AppendLine(4, "}");
        sb.Append('}');

        return sb.ToString();
    }

    /// <summary>
    /// Combines multiple generated types into a single file content.
    /// </summary>
    public static string CombineTypes(
        IEnumerable<GeneratedType> types,
        string @namespace)
    {
        var typesList = types.ToList();
        if (typesList.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Collect all unique usings
        var allUsings = typesList
            .SelectMany(t => t.RequiredUsings)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(u => u, StringComparer.Ordinal);

        foreach (var usingStatement in allUsings)
        {
            sb.AppendLine($"using {usingStatement};");
        }

        sb.AppendLine();

        // Namespace
        sb.AppendLine($"namespace {@namespace};");
        sb.AppendLine();

        // Types
        for (var i = 0; i < typesList.Count; i++)
        {
            sb.Append(typesList[i].Content);

            if (i < typesList.Count - 1)
            {
                sb.AppendLine();
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a single generated type as a complete file.
    /// </summary>
    public static string FormatAsFile(GeneratedType type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        // If content already contains full file structure (header, usings, namespace),
        // return it as-is to avoid duplication
        if (type.Content.StartsWith("// <auto-generated />", StringComparison.Ordinal))
        {
            return type.Content;
        }

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Usings
        foreach (var usingStatement in type.RequiredUsings.OrderBy(u => u, StringComparer.Ordinal))
        {
            sb.AppendLine($"using {usingStatement};");
        }

        sb.AppendLine();

        // Namespace
        sb.AppendLine($"namespace {type.Namespace};");
        sb.AppendLine();

        // Type content
        sb.Append(type.Content);

        return sb.ToString();
    }

    /// <summary>
    /// Formats a single generated type as a test file (no #nullable enable, no usings).
    /// Used for verified test output files.
    /// </summary>
    public static string FormatAsTestFile(GeneratedType type)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        // If content already contains full file structure (header, usings, namespace),
        // return it as-is to avoid duplication
        if (type.Content.StartsWith("// <auto-generated />", StringComparison.Ordinal))
        {
            return type.Content;
        }

        var sb = new StringBuilder();

        // Header (without #nullable enable)
        sb.AppendLine("// <auto-generated />");

        // Namespace
        sb.AppendLine($"namespace {type.Namespace};");
        sb.AppendLine();

        // Type content
        sb.Append(type.Content);

        return sb.ToString();
    }

    /// <summary>
    /// Generates class content without header/namespace.
    /// </summary>
    private static string GenerateClassContentOnly(
        ICodeDocumentationTagsGenerator codeDocGenerator,
        ClassParameters classParams)
    {
        // Create a copy with no header/namespace (just generate the class body)
        var modifiedParams = classParams with { HeaderContent = null, Namespace = "TEMP" };

        var contentGenerator = new GenerateContentForClass(codeDocGenerator, modifiedParams);
        var fullContent = contentGenerator.Generate();

        return ExtractContentAfterNamespace(fullContent);
    }

    /// <summary>
    /// Generates interface content without header/namespace.
    /// </summary>
    private static string GenerateInterfaceContentOnly(
        ICodeDocumentationTagsGenerator codeDocGenerator,
        InterfaceParameters interfaceParams)
    {
        // Create a copy with no header/namespace (just generate the interface body)
        var modifiedParams = interfaceParams with { HeaderContent = null, Namespace = "TEMP" };

        var contentGenerator = new GenerateContentForInterface(codeDocGenerator, modifiedParams);
        var fullContent = contentGenerator.Generate();

        return ExtractContentAfterNamespace(fullContent);
    }

    /// <summary>
    /// Generates record content without header/namespace.
    /// </summary>
    private static string GenerateRecordContentOnly(
        ICodeDocumentationTagsGenerator codeDocGenerator,
        RecordParameters recordParams)
    {
        // Wrap single record in RecordsParameters container
        var recordsContainer = new RecordsParameters(
            HeaderContent: null,
            Namespace: "TEMP",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.Public,
            Parameters: [recordParams]);

        var contentGenerator = new GenerateContentForRecords(codeDocGenerator, recordsContainer);
        var fullContent = contentGenerator.Generate();

        return ExtractContentAfterNamespace(fullContent);
    }

    /// <summary>
    /// Extracts content after the namespace declaration, preserving blank lines and formatting.
    /// </summary>
    private static string ExtractContentAfterNamespace(string fullContent)
    {
        // Find the namespace line ending with ";\r\n" or ";\n"
        const string namespaceMarker = "namespace TEMP;";
        var namespaceIndex = fullContent.IndexOf(namespaceMarker, StringComparison.Ordinal);

        if (namespaceIndex < 0)
        {
            return fullContent;
        }

        // Skip past the namespace line
        var startIndex = namespaceIndex + namespaceMarker.Length;

        // Skip any line endings after namespace
        while (startIndex < fullContent.Length &&
               (fullContent[startIndex] == '\r' || fullContent[startIndex] == '\n'))
        {
            startIndex++;
        }

        return startIndex < fullContent.Length
            ? fullContent.Substring(startIndex)
            : fullContent;
    }

    /// <summary>
    /// Generates Output Cache policy constants from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the Output Cache policies class, or null if no policies needed.</returns>
    public static string? GenerateOutputCachePolicies(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
        => OutputCachePoliciesExtractor.Extract(openApiDoc, projectName, includeDeprecated);

    /// <summary>
    /// Generates Output Caching DI extension from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the Output Caching DI extension class, or null if no policies needed.</returns>
    public static string? GenerateOutputCacheDependencyInjection(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
        => OutputCacheDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);

    /// <summary>
    /// Generates HybridCache policy constants from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the HybridCache policies class, or null if no policies needed.</returns>
    public static string? GenerateHybridCachePolicies(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
        => HybridCachePoliciesExtractor.Extract(openApiDoc, projectName, includeDeprecated);

    /// <summary>
    /// Generates HybridCache DI extension from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the HybridCache DI extension class, or null if no policies needed.</returns>
    public static string? GenerateHybridCacheDependencyInjection(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
        => HybridCacheDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);

    /// <summary>
    /// Generates webhook handler interfaces from OpenAPI webhooks.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="includeDeprecated">Whether to include deprecated webhooks.</param>
    /// <returns>List of generated webhook handler interface types.</returns>
    public static List<GeneratedType> GenerateWebhookHandlerInterfaces(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        var result = new List<GeneratedType>();

        if (!openApiDoc.HasWebhooks())
        {
            return result;
        }

        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);
        var interfaces = WebhookHandlerExtractor.Extract(openApiDoc, projectName, systemTypeResolver, includeDeprecated);

        if (interfaces == null || interfaces.Count == 0)
        {
            return result;
        }

        var @namespace = NamespaceBuilder.ForWebhookHandlers(projectName);
        var codeDocGenerator = new CodeDocumentationTagsGenerator();

        foreach (var interfaceParams in interfaces)
        {
            var content = GenerateInterfaceContentOnly(codeDocGenerator, interfaceParams);

            // Handler interfaces reference Parameter and Result types (in Webhooks namespace),
            // not model types directly - models are referenced by Parameter classes
            var usings = new List<string>(HandlerUsings)
            {
                NamespaceBuilder.ForWebhookParameters(projectName),
                NamespaceBuilder.ForWebhookResults(projectName),
            };

            result.Add(new GeneratedType(
                TypeName: interfaceParams.InterfaceTypeName,
                Category: "WebhookHandlers",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: usings,
                GroupName: "Webhooks",
                SubFolder: "Webhooks\\Handlers"));
        }

        return result;
    }

    /// <summary>
    /// Generates webhook parameter types from OpenAPI webhooks.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="includeDeprecated">Whether to include deprecated webhooks.</param>
    /// <returns>List of generated webhook parameter types.</returns>
    public static List<GeneratedType> GenerateWebhookParameters(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        var result = new List<GeneratedType>();

        if (!openApiDoc.HasWebhooks())
        {
            return result;
        }

        var modelNames = openApiDoc.Components?.Schemas?.Keys ?? [];
        var systemTypeResolver = new SystemTypeConflictResolver(modelNames);
        var webhookParameters = WebhookParameterExtractor.Extract(openApiDoc, projectName, systemTypeResolver, includeDeprecated);

        if (webhookParameters == null || webhookParameters.Count == 0)
        {
            return result;
        }

        var @namespace = NamespaceBuilder.ForWebhookParameters(projectName);

        foreach (var (className, content) in webhookParameters)
        {
            // The content is already complete, so we use it directly
            result.Add(new GeneratedType(
                TypeName: className,
                Category: "WebhookParameters",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: [], // Content is self-contained
                GroupName: "Webhooks",
                SubFolder: "Webhooks\\Parameters"));
        }

        return result;
    }

    /// <summary>
    /// Generates webhook result types from OpenAPI webhooks.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="includeDeprecated">Whether to include deprecated webhooks.</param>
    /// <returns>List of generated webhook result types.</returns>
    public static List<GeneratedType> GenerateWebhookResults(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        var result = new List<GeneratedType>();

        if (!openApiDoc.HasWebhooks())
        {
            return result;
        }

        var webhookResults = WebhookResultExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (webhookResults == null || webhookResults.Count == 0)
        {
            return result;
        }

        var @namespace = NamespaceBuilder.ForWebhookResults(projectName);

        foreach (var (className, content) in webhookResults)
        {
            // The content is already complete, so we use it directly
            result.Add(new GeneratedType(
                TypeName: className,
                Category: "WebhookResults",
                Namespace: @namespace,
                Content: content,
                RequiredUsings: [], // Content is self-contained
                GroupName: "Webhooks",
                SubFolder: "Webhooks\\Results"));
        }

        return result;
    }

    /// <summary>
    /// Generates webhook endpoint registration from OpenAPI webhooks.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="config">The server configuration (used for webhook base path and includeDeprecated).</param>
    /// <returns>Generated webhook endpoint registration type, or null if no webhooks.</returns>
    public static GeneratedType? GenerateWebhookEndpoints(
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config)
    {
        if (!openApiDoc.HasWebhooks())
        {
            return null;
        }

        var classParams = WebhookEndpointExtractor.Extract(openApiDoc, projectName, config);

        if (classParams == null)
        {
            return null;
        }

        var @namespace = NamespaceBuilder.ForWebhookEndpoints(projectName);
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var content = GenerateClassContentOnly(codeDocGenerator, classParams);

        var usings = new List<string>(EndpointUsings)
        {
            "System.CodeDom.Compiler",
            NamespaceBuilder.ForWebhookHandlers(projectName),
            NamespaceBuilder.ForWebhookParameters(projectName),
            NamespaceBuilder.ForWebhookResults(projectName),
        };

        return new GeneratedType(
            TypeName: classParams.ClassTypeName,
            Category: "WebhookEndpoints",
            Namespace: @namespace,
            Content: content,
            RequiredUsings: usings,
            GroupName: "Webhooks",
            SubFolder: "Webhooks\\Endpoints");
    }

    /// <summary>
    /// Generates webhook dependency injection registration from OpenAPI webhooks.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The project name for namespace generation.</param>
    /// <param name="includeDeprecated">Whether to include deprecated webhooks.</param>
    /// <returns>Generated webhook DI registration type, or null if no webhooks.</returns>
    public static GeneratedType? GenerateWebhookDependencyInjection(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        if (!openApiDoc.HasWebhooks())
        {
            return null;
        }

        var classParams = WebhookDependencyInjectionExtractor.Extract(openApiDoc, projectName, includeDeprecated);

        if (classParams == null)
        {
            return null;
        }

        var @namespace = $"{projectName}.Generated.DependencyInjection";
        var codeDocGenerator = new CodeDocumentationTagsGenerator();
        var content = GenerateClassContentOnly(codeDocGenerator, classParams);

        var usings = new List<string>(DependencyInjectionUsings)
        {
            "System.CodeDom.Compiler",
            NamespaceBuilder.ForWebhookHandlers(projectName),
        };

        return new GeneratedType(
            TypeName: classParams.ClassTypeName,
            Category: "WebhookDependencyInjection",
            Namespace: @namespace,
            Content: content,
            RequiredUsings: usings,
            GroupName: "Webhooks",
            SubFolder: "Extensions");
    }
}