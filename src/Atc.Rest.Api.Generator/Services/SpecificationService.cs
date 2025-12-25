namespace Atc.Rest.Api.Generator.Services;

/// <summary>
/// Service for handling OpenAPI specifications including multi-part file support.
/// Provides methods for reading, merging, splitting, and validating specifications.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Diagnostic reporting for all parsing errors.")]
public static class SpecificationService
{
    private const string MultiPartExtensionKey = "x-multipart";

    /// <summary>
    /// Reads and parses a single OpenAPI specification file.
    /// </summary>
    /// <param name="filePath">Path to the YAML file.</param>
    /// <returns>The specification file with parsed document.</returns>
    public static SpecificationFile ReadSingle(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return SpecificationFile.Empty(filePath);
        }

        var content = File.ReadAllText(filePath);
        return SpecificationFile.FromContent(filePath, content);
    }

    /// <summary>
    /// Reads and parses a single OpenAPI specification from YAML content.
    /// </summary>
    /// <param name="yamlContent">The YAML content.</param>
    /// <param name="virtualPath">A virtual path for error reporting.</param>
    /// <returns>The specification file with parsed document.</returns>
    public static SpecificationFile ReadFromContent(
        string yamlContent,
        string virtualPath = "inline.yaml")
        => SpecificationFile.FromContent(virtualPath, yamlContent);

    /// <summary>
    /// Reads and parses a single OpenAPI specification file.
    /// Alias for ReadSingle for convenience.
    /// </summary>
    /// <param name="filePath">Path to the YAML file.</param>
    /// <returns>The specification file with parsed document.</returns>
    public static SpecificationFile ReadFromFile(string filePath)
        => ReadSingle(filePath);

    /// <summary>
    /// Serializes an OpenAPI document to YAML format.
    /// </summary>
    /// <param name="document">The OpenAPI document to serialize.</param>
    /// <returns>YAML string representation of the document.</returns>
    public static string SerializeToYaml(OpenApiDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        using var writer = new StringWriter();
        var yamlWriter = new OpenApiYamlWriter(writer);
        document.SerializeAsV3(yamlWriter);
        return writer.ToString();
    }

    /// <summary>
    /// Serializes an OpenAPI document to JSON format.
    /// </summary>
    /// <param name="document">The OpenAPI document to serialize.</param>
    /// <returns>JSON string representation of the document.</returns>
    public static string SerializeToJson(OpenApiDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        using var writer = new StringWriter();
        var jsonWriter = new OpenApiJsonWriter(writer);
        document.SerializeAsV3(jsonWriter);
        return writer.ToString();
    }

    /// <summary>
    /// Discovers and reads all part files for a multi-part specification.
    /// Uses auto-discovery based on naming convention: {BaseName}_{PartName}.yaml
    /// </summary>
    /// <param name="baseFilePath">Path to the base YAML file.</param>
    /// <returns>List of discovered part files.</returns>
    public static IReadOnlyList<SpecificationFile> DiscoverPartFiles(
        string baseFilePath)
    {
        var directory = Path.GetDirectoryName(baseFilePath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(baseFilePath);
        var extension = Path.GetExtension(baseFilePath);

        var partFiles = new List<SpecificationFile>();

        // Look for files matching the pattern {BaseName}_{PartName}.yaml
        var pattern = $"{baseName}_*{extension}";
        var matchingFiles = Directory
            .GetFiles(directory, pattern)
            .Where(f => !f.Equals(baseFilePath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var partFilePath in matchingFiles)
        {
            var partFile = SpecificationFile.FromFile(partFilePath, baseName);
            partFiles.Add(partFile);
        }

        return partFiles;
    }

    /// <summary>
    /// Reads and merges a multi-part specification into a single OpenAPI document.
    /// Supports both auto-discovery and explicit file lists.
    /// </summary>
    /// <param name="baseFilePath">Path to the base YAML file.</param>
    /// <param name="config">Optional configuration for multi-part handling.</param>
    /// <returns>The merge result with combined document and diagnostics.</returns>
    public static MergeResult ReadAndMerge(
        string baseFilePath,
        MultiPartConfiguration? config = null)
    {
        config ??= MultiPartConfiguration.Default;

        // Read base file
        if (!File.Exists(baseFilePath))
        {
            return MergeResult.Failed(
                RuleIdentifiers.BaseFileNotFound,
                $"Base file not found: {baseFilePath}",
                baseFilePath);
        }

        var baseFile = SpecificationFile.FromFile(baseFilePath);
        if (baseFile.Document == null)
        {
            return MergeResult.Failed(
                RuleIdentifiers.ServerParsingError,
                $"Failed to parse base file: {baseFilePath}",
                baseFilePath);
        }

        // Check for x-multipart extension in base file
        var docConfig = ExtractMultiPartConfig(baseFile.Document);
        if (docConfig != null)
        {
            config = docConfig;
        }

        // If multi-part is disabled, return single file result
        if (!config.Enabled)
        {
            return MergeResult.SingleFile(baseFile);
        }

        // Discover or load part files
        var partFiles = config.Discovery.Equals("explicit", StringComparison.OrdinalIgnoreCase)
            ? LoadExplicitPartFiles(baseFilePath, config.Parts)
            : DiscoverPartFiles(baseFilePath);

        // If no part files found, return single file result
        if (partFiles.Count == 0)
        {
            return MergeResult.SingleFile(baseFile);
        }

        // Merge all files
        return MergeSpecifications(baseFile, partFiles, config);
    }

    /// <summary>
    /// Merges multiple specification files into a single OpenAPI document.
    /// </summary>
    /// <param name="baseFile">The base file.</param>
    /// <param name="partFiles">The part files to merge.</param>
    /// <param name="config">The merge configuration.</param>
    /// <returns>The merge result.</returns>
    public static MergeResult MergeSpecifications(
        SpecificationFile baseFile,
        IReadOnlyList<SpecificationFile> partFiles,
        MultiPartConfiguration? config = null)
    {
        config ??= MultiPartConfiguration.Default;
        var diagnostics = new List<DiagnosticMessage>();

        if (baseFile.Document == null)
        {
            return MergeResult.Failed(
                RuleIdentifiers.ServerParsingError,
                "Base file has no valid document",
                baseFile.FilePath);
        }

        // Clone the base document to work with
        var mergedDocument = CloneOpenApiDocument(baseFile.Document);

        // Ensure paths and components exist
        mergedDocument.Components ??= new OpenApiComponents();
        mergedDocument.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        mergedDocument.Components.Parameters ??= new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal);
        mergedDocument.Tags ??= new HashSet<OpenApiTag>();

        // Merge each part file
        foreach (var partFile in partFiles)
        {
            if (partFile.Document == null)
            {
                diagnostics.Add(DiagnosticMessage.Error(
                    RuleIdentifiers.ServerParsingError,
                    $"Failed to parse part file: {partFile.FileName}",
                    partFile.FilePath));
                continue;
            }

            // Check for prohibited sections in part files
            var partDiagnostics = ValidatePartFile(partFile);
            diagnostics.AddRange(partDiagnostics);

            // Merge paths
            MergePaths(mergedDocument, partFile.Document, partFile.FilePath, config, diagnostics);

            // Merge schemas
            MergeSchemas(mergedDocument, partFile.Document, partFile.FilePath, config, diagnostics);

            // Merge parameters
            MergeParameters(mergedDocument, partFile.Document, partFile.FilePath, config, diagnostics);

            // Merge tags
            MergeTags(mergedDocument, partFile.Document, config);
        }

        // Validate merged document for unresolved references
        var unresolvedRefs = ValidateReferences(mergedDocument);
        foreach (var unresolved in unresolvedRefs)
        {
            diagnostics.Add(DiagnosticMessage.Warning(
                RuleIdentifiers.UnresolvedReferenceAfterMerge,
                $"Unresolved reference after merge: {unresolved}"));
        }

        // Add success info if no errors
        if (!diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            diagnostics.Add(DiagnosticMessage.Info(
                RuleIdentifiers.MultiPartMergeSuccessful,
                $"Successfully merged {partFiles.Count} part file(s) with base file"));
        }

        var hasErrors = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        return hasErrors
            ? MergeResult.Failed(diagnostics)
            : MergeResult.Success(mergedDocument, baseFile, partFiles, diagnostics);
    }

    /// <summary>
    /// Splits an OpenAPI specification into multiple files based on the specified strategy.
    /// </summary>
    /// <param name="document">The OpenAPI document to split.</param>
    /// <param name="baseName">The base name for generated files.</param>
    /// <param name="strategy">The split strategy to use.</param>
    /// <param name="extractCommon">Whether to extract shared schemas to a common file.</param>
    /// <returns>The split result with file contents.</returns>
    public static SplitResult Split(
        OpenApiDocument document,
        string baseName,
        SplitStrategy strategy,
        bool extractCommon = true)
    {
        var diagnostics = new List<DiagnosticMessage>();

        // Group operations by the selected strategy
        var operationGroups = GroupOperationsByStrategy(document, strategy);

        // Identify shared schemas (used by multiple groups)
        var sharedSchemas = extractCommon
            ? IdentifySharedSchemas(document, operationGroups)
            : new HashSet<string>(StringComparer.Ordinal);

        var partFiles = new List<SplitFileContent>();

        // Generate part files for each group
        foreach (var group in operationGroups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var partContent = GeneratePartFileContent(
                document,
                group.Key,
                group.Value,
                sharedSchemas);

            partFiles.Add(new SplitFileContent(
                fileName: $"{baseName}_{group.Key}.yaml",
                content: partContent.Content,
                partName: group.Key,
                isBaseFile: false,
                isCommonFile: false,
                pathCount: partContent.PathCount,
                schemaCount: partContent.SchemaCount,
                parameterCount: partContent.ParameterCount));
        }

        // Generate common file for shared schemas
        SplitFileContent? commonFile = null;
        if (extractCommon && sharedSchemas.Count > 0)
        {
            var commonContent = GenerateCommonFileContent(document, sharedSchemas);
            commonFile = new SplitFileContent(
                fileName: $"{baseName}_Common.yaml",
                content: commonContent.Content,
                partName: "Common",
                isBaseFile: false,
                isCommonFile: true,
                pathCount: 0,
                schemaCount: commonContent.SchemaCount,
                parameterCount: commonContent.ParameterCount);
        }

        // Generate base file (info, servers, security, x-multipart config)
        var baseContent = GenerateBaseFileContent(
            document,
            baseName,
            partFiles
                .Select(p => p.PartName!)
                .ToList());

        var baseFile = new SplitFileContent(
            fileName: $"{baseName}.yaml",
            content: baseContent,
            partName: null,
            isBaseFile: true,
            isCommonFile: false,
            pathCount: 0,
            schemaCount: 0,
            parameterCount: 0);

        return new SplitResult(baseFile, partFiles, commonFile, diagnostics, strategy);
    }

    /// <summary>
    /// Analyzes an OpenAPI specification and recommends a split strategy.
    /// </summary>
    /// <param name="document">The OpenAPI document to analyze.</param>
    /// <param name="filePath">The file path for reporting.</param>
    /// <returns>The analysis result with recommendations.</returns>
    public static SpecificationAnalysis Analyze(
        OpenApiDocument document,
        string filePath)
    {
        // Count operations per tag
        var tagAnalysis = new Dictionary<string, TagAnalysis>(StringComparer.OrdinalIgnoreCase);
        var pathSegmentAnalysis = new Dictionary<string, PathSegmentAnalysis>(StringComparer.OrdinalIgnoreCase);

        if (document.Paths != null)
        {
            foreach (var path in document.Paths)
            {
                var segment = GetFirstPathSegment(path.Key);
                if (path.Value?.Operations == null)
                {
                    continue;
                }

                // Track path segment
                if (!pathSegmentAnalysis.TryGetValue(segment, out var segmentInfo))
                {
                    segmentInfo = new PathSegmentAnalysis(segment, 0, 0, new List<string>());
                    pathSegmentAnalysis[segment] = segmentInfo;
                }

                pathSegmentAnalysis[segment] = new PathSegmentAnalysis(
                    segment,
                    segmentInfo.PathCount + 1,
                    segmentInfo.OperationCount + path.Value.Operations.Count,
                    segmentInfo.Schemas);

                // Track tags
                foreach (var operation in path.Value.Operations.Values)
                {
                    if (operation.Tags == null)
                    {
                        continue;
                    }

                    foreach (var tag in operation.Tags)
                    {
                        if (string.IsNullOrEmpty(tag.Name))
                        {
                            continue;
                        }

                        if (!tagAnalysis.TryGetValue(tag.Name!, out var tagInfo))
                        {
                            tagInfo = new TagAnalysis(tag.Name!, 0, 0, new List<string>());
                            tagAnalysis[tag.Name!] = tagInfo;
                        }

                        var pathsList = new List<string>(tagInfo.Paths);
                        if (!pathsList.Contains(path.Key, StringComparer.Ordinal))
                        {
                            pathsList.Add(path.Key);
                        }

                        tagAnalysis[tag.Name!] = new TagAnalysis(
                            tag.Name!,
                            tagInfo.OperationCount + 1,
                            tagInfo.SchemaCount,
                            pathsList);
                    }
                }
            }
        }

        // Determine recommended strategy
        var (strategy, reason) = DetermineRecommendedStrategy(document, tagAnalysis, pathSegmentAnalysis);

        // Generate suggested splits
        var suggestedSplits = GenerateSuggestedSplits(document, strategy, tagAnalysis, pathSegmentAnalysis);

        // Identify shared schemas
        var sharedSchemas = IdentifySharedSchemasForAnalysis(document, strategy, tagAnalysis, pathSegmentAnalysis);

        var totalLines = 0; // Would need original content to calculate

        return new SpecificationAnalysis(
            filePath: filePath,
            totalLines: totalLines,
            totalPaths: document.Paths?.Count ?? 0,
            totalOperations: document.Paths?.Sum(p => p.Value?.Operations?.Count ?? 0) ?? 0,
            totalSchemas: document.Components?.Schemas?.Count ?? 0,
            totalParameters: document.Components?.Parameters?.Count ?? 0,
            tags: tagAnalysis,
            pathSegments: pathSegmentAnalysis,
            sharedSchemas: sharedSchemas,
            recommendedStrategy: strategy,
            recommendedStrategyReason: reason,
            suggestedSplits: suggestedSplits);
    }

    /// <summary>
    /// Validates an OpenAPI specification and returns diagnostics.
    /// </summary>
    /// <param name="document">The OpenAPI document to validate.</param>
    /// <param name="filePath">The file path for error reporting.</param>
    /// <param name="strategy">The validation strategy.</param>
    /// <returns>List of diagnostic messages.</returns>
    public static IReadOnlyList<DiagnosticMessage> Validate(
        OpenApiDocument document,
        string filePath,
        ValidateSpecificationStrategy strategy = ValidateSpecificationStrategy.Standard)
        => Validators.OpenApiDocumentValidator.Validate(
            strategy,
            document,
            Array.Empty<OpenApiError>(),
            filePath);

    /// <summary>
    /// Validates a part file for prohibited sections.
    /// </summary>
    /// <param name="partFile">The part file to validate.</param>
    /// <returns>List of diagnostic messages.</returns>
    public static IReadOnlyList<DiagnosticMessage> ValidatePartFile(
        SpecificationFile partFile)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (partFile.Document == null)
        {
            return diagnostics;
        }

        // Part files should not have info section with version
        if (partFile.Document.Info?.Version != null)
        {
            diagnostics.Add(DiagnosticMessage.Warning(
                RuleIdentifiers.PartFileMissingOpenApiVersion,
                $"Part file '{partFile.FileName}' has 'info' section - will be ignored during merge",
                partFile.FilePath));
        }

        // Part files should not have servers
        if (partFile.Document.Servers?.Count > 0)
        {
            diagnostics.Add(DiagnosticMessage.Warning(
                RuleIdentifiers.PartFileContainsProhibitedSection,
                $"Part file '{partFile.FileName}' has 'servers' section - will be ignored during merge",
                partFile.FilePath));
        }

        // Part files should not have securitySchemes
        if (partFile.Document.Components?.SecuritySchemes?.Count > 0)
        {
            diagnostics.Add(DiagnosticMessage.Warning(
                RuleIdentifiers.PartFileContainsProhibitedSection,
                $"Part file '{partFile.FileName}' has 'securitySchemes' - will be ignored during merge",
                partFile.FilePath));
        }

        return diagnostics;
    }

    /// <summary>
    /// Generates a merged YAML content string from the merged document.
    /// </summary>
    /// <param name="mergeResult">The merge result.</param>
    /// <returns>The merged YAML content.</returns>
    public static string GenerateMergedYaml(MergeResult mergeResult)
        => mergeResult.Document == null
            ? string.Empty
            : SerializeToYaml(mergeResult.Document);

    private static IReadOnlyList<SpecificationFile> LoadExplicitPartFiles(
        string baseFilePath,
        IReadOnlyList<string> parts)
    {
        var directory = Path.GetDirectoryName(baseFilePath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(baseFilePath);
        var partFiles = new List<SpecificationFile>();

        foreach (var part in parts)
        {
            var partPath = Path.Combine(directory, part);
            if (File.Exists(partPath))
            {
                partFiles.Add(SpecificationFile.FromFile(partPath, baseName));
            }
        }

        return partFiles;
    }

    private static MultiPartConfiguration? ExtractMultiPartConfig(
        OpenApiDocument document)
    {
        if (document.Extensions == null ||
            !document.Extensions.TryGetValue(MultiPartExtensionKey, out var extensionValue))
        {
            return null;
        }

        try
        {
            // The extension value needs to be serialized and parsed as JSON
            var jsonStr = JsonSerializer.Serialize(extensionValue);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<MultiPartConfiguration>(jsonStr, options);
        }
        catch
        {
            return null;
        }
    }

    private static OpenApiDocument CloneOpenApiDocument(OpenApiDocument source)
    {
        // Create a new document with copied properties
        var clone = new OpenApiDocument
        {
            Info = source.Info != null
                ? new OpenApiInfo
                {
                    Title = source.Info.Title,
                    Version = source.Info.Version,
                    Description = source.Info.Description,
                    TermsOfService = source.Info.TermsOfService,
                    Contact = source.Info.Contact,
                    License = source.Info.License,
                }
                : null!,
        };

        // Copy servers
        if (source.Servers != null)
        {
            clone.Servers = new List<OpenApiServer>(source.Servers);
        }

        // Copy paths (shallow copy of the dictionary, but each path item is shared)
        if (source.Paths != null)
        {
            clone.Paths = new OpenApiPaths();
            foreach (var path in source.Paths)
            {
                clone.Paths[path.Key] = path.Value;
            }
        }

        // Copy components
        if (source.Components != null)
        {
            clone.Components = new OpenApiComponents();

            if (source.Components.Schemas != null)
            {
                clone.Components.Schemas = new Dictionary<string, IOpenApiSchema>(
                    source.Components.Schemas,
                    StringComparer.Ordinal);
            }

            if (source.Components.Parameters != null)
            {
                clone.Components.Parameters = new Dictionary<string, IOpenApiParameter>(
                    source.Components.Parameters,
                    StringComparer.Ordinal);
            }

            if (source.Components.SecuritySchemes != null)
            {
                clone.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>(
                    source.Components.SecuritySchemes,
                    StringComparer.Ordinal);
            }

            if (source.Components.RequestBodies != null)
            {
                clone.Components.RequestBodies = new Dictionary<string, IOpenApiRequestBody>(
                    source.Components.RequestBodies,
                    StringComparer.Ordinal);
            }

            if (source.Components.Responses != null)
            {
                clone.Components.Responses = new Dictionary<string, IOpenApiResponse>(
                    source.Components.Responses,
                    StringComparer.Ordinal);
            }
        }

        // Copy tags
        if (source.Tags != null)
        {
            clone.Tags = new HashSet<OpenApiTag>(source.Tags);
        }

        // Copy security requirements
        if (source.Security != null)
        {
            clone.Security = new List<OpenApiSecurityRequirement>(source.Security);
        }

        // Copy extensions
        if (source.Extensions != null)
        {
            clone.Extensions = new Dictionary<string, IOpenApiExtension>(
                source.Extensions,
                StringComparer.Ordinal);
        }

        return clone;
    }

    private static void MergePaths(
        OpenApiDocument target,
        OpenApiDocument source,
        string sourceFilePath,
        MultiPartConfiguration config,
        List<DiagnosticMessage> diagnostics)
    {
        if (source.Paths == null)
        {
            return;
        }

        foreach (var path in source.Paths)
        {
            if (target.Paths.ContainsKey(path.Key))
            {
                switch (config.PathsMergeStrategy)
                {
                    case MergeStrategy.ErrorOnDuplicate:
                        diagnostics.Add(DiagnosticMessage.Error(
                            RuleIdentifiers.DuplicatePathInPart,
                            $"Duplicate path '{path.Key}' found in part file",
                            sourceFilePath));
                        break;

                    case MergeStrategy.FirstWins:
                        // Skip - already exists
                        break;

                    case MergeStrategy.LastWins:
                        target.Paths[path.Key] = path.Value;
                        break;
                }
            }
            else
            {
                target.Paths[path.Key] = path.Value;
            }
        }
    }

    private static void MergeSchemas(
        OpenApiDocument target,
        OpenApiDocument source,
        string sourceFilePath,
        MultiPartConfiguration config,
        List<DiagnosticMessage> diagnostics)
    {
        if (source.Components?.Schemas == null)
        {
            return;
        }

        foreach (var schema in source.Components.Schemas)
        {
            if (target.Components!.Schemas!.ContainsKey(schema.Key))
            {
                switch (config.SchemasMergeStrategy)
                {
                    case MergeStrategy.ErrorOnDuplicate:
                        diagnostics.Add(DiagnosticMessage.Error(
                            RuleIdentifiers.DuplicateSchemaInPart,
                            $"Duplicate schema '{schema.Key}' found in part file",
                            sourceFilePath));
                        break;

                    case MergeStrategy.MergeIfIdentical:
                        // Check if schemas are identical - for now just skip if exists
                        break;

                    case MergeStrategy.FirstWins:
                        // Skip - already exists
                        break;

                    case MergeStrategy.LastWins:
                        target.Components.Schemas[schema.Key] = schema.Value;
                        break;
                }
            }
            else
            {
                target.Components!.Schemas![schema.Key] = schema.Value;
            }
        }
    }

    private static void MergeParameters(
        OpenApiDocument target,
        OpenApiDocument source,
        string sourceFilePath,
        MultiPartConfiguration config,
        List<DiagnosticMessage> diagnostics)
    {
        if (source.Components?.Parameters == null)
        {
            return;
        }

        foreach (var parameter in source.Components.Parameters)
        {
            if (target.Components!.Parameters!.ContainsKey(parameter.Key))
            {
                switch (config.ParametersMergeStrategy)
                {
                    case MergeStrategy.ErrorOnDuplicate:
                        diagnostics.Add(DiagnosticMessage.Error(
                            RuleIdentifiers.DuplicateSchemaInPart,
                            $"Duplicate parameter '{parameter.Key}' found in part file",
                            sourceFilePath));
                        break;

                    case MergeStrategy.MergeIfIdentical:
                    case MergeStrategy.FirstWins:
                        // Skip - already exists
                        break;

                    case MergeStrategy.LastWins:
                        target.Components.Parameters[parameter.Key] = parameter.Value;
                        break;
                }
            }
            else
            {
                target.Components!.Parameters![parameter.Key] = parameter.Value;
            }
        }
    }

    private static void MergeTags(
        OpenApiDocument target,
        OpenApiDocument source,
        MultiPartConfiguration config)
    {
        if (source.Tags == null || source.Tags.Count == 0)
        {
            return;
        }

        var existingTags = target.Tags != null
            ? new HashSet<string>(
                target
                    .Tags
                    .Select(t => t.Name)
                    .Where(n => n != null)
                    .Cast<string>(),
                StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in source.Tags)
        {
            if (string.IsNullOrEmpty(tag.Name) ||
                existingTags.Contains(tag.Name!))
            {
                continue;
            }

            target.Tags ??= new HashSet<OpenApiTag>();
            target.Tags.Add(tag);
            existingTags.Add(tag.Name!);
        }
    }

    private static IReadOnlyList<string> ValidateReferences(
        OpenApiDocument document)
    {
        var unresolvedRefs = new List<string>();
        var validSchemaNames = document.Components?.Schemas?.Keys != null
            ? new HashSet<string>(document.Components.Schemas.Keys, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        // Check for unresolved schema references in paths
        if (document.Paths != null)
        {
            foreach (var path in document.Paths)
            {
                if (path.Value?.Operations == null)
                {
                    continue;
                }

                foreach (var operation in path.Value.Operations.Values)
                {
                    // Check request body
                    if (operation.RequestBody?.Content != null)
                    {
                        foreach (var content in operation.RequestBody.Content.Values)
                        {
                            CheckSchemaReference(content.Schema, validSchemaNames, unresolvedRefs);
                        }
                    }

                    // Check responses
                    if (operation.Responses != null)
                    {
                        foreach (var response in operation.Responses.Values)
                        {
                            if (response.Content != null)
                            {
                                foreach (var content in response.Content.Values)
                                {
                                    CheckSchemaReference(content.Schema, validSchemaNames, unresolvedRefs);
                                }
                            }
                        }
                    }
                }
            }
        }

        return unresolvedRefs
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void CheckSchemaReference(
        IOpenApiSchema? schema,
        HashSet<string> validSchemaNames,
        List<string> unresolvedRefs)
    {
        if (schema == null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refId = schemaRef.Reference?.Id;
            if (!string.IsNullOrEmpty(refId) && !validSchemaNames.Contains(refId!))
            {
                unresolvedRefs.Add(refId!);
            }
        }

        // Check array items
        if (schema is OpenApiSchema actualSchema && actualSchema.Items != null)
        {
            CheckSchemaReference(actualSchema.Items, validSchemaNames, unresolvedRefs);
        }
    }

    private static Dictionary<string, List<(string Path, OpenApiOperation Operation)>> GroupOperationsByStrategy(
        OpenApiDocument document,
        SplitStrategy strategy)
    {
        var groups = new Dictionary<string, List<(string, OpenApiOperation)>>(StringComparer.OrdinalIgnoreCase);

        if (document.Paths == null)
        {
            return groups;
        }

        foreach (var path in document.Paths)
        {
            if (path.Value?.Operations == null)
            {
                continue;
            }

            foreach (var operation in path.Value.Operations.Values)
            {
                var groupName = strategy switch
                {
                    SplitStrategy.ByTag => operation.Tags?.FirstOrDefault()?.Name ?? "Untagged",
                    SplitStrategy.ByPathSegment => GetFirstPathSegment(path.Key),
                    SplitStrategy.ByDomain => GetDomainFromOperation(path.Key, operation),
                    _ => "Default",
                };

                groupName = groupName.ToPascalCaseForDotNet();

                if (!groups.TryGetValue(groupName, out var list))
                {
                    list = new List<(string, OpenApiOperation)>();
                    groups[groupName] = list;
                }

                list.Add((path.Key, operation));
            }
        }

        return groups;
    }

    private static string GetFirstPathSegment(string path)
    {
        var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            // Skip version segments
            if (segment.StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
                segment.Length > 1 && char.IsDigit(segment[1]))
            {
                continue;
            }

            // Skip parameter segments
            if (segment.StartsWith("{", StringComparison.Ordinal))
            {
                continue;
            }

            return segment;
        }

        return "Api";
    }

    private static string GetDomainFromOperation(
        string path,
        OpenApiOperation operation)
    {
        // First try tag, then fall back to path segment
        var tagName = operation.Tags?.FirstOrDefault()?.Name;
        return string.IsNullOrEmpty(tagName)
            ? GetFirstPathSegment(path)
            : tagName!;
    }

    private static HashSet<string> IdentifySharedSchemas(
        OpenApiDocument document,
        Dictionary<string, List<(string Path, OpenApiOperation Operation)>> operationGroups)
    {
        var schemaUsage = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        // Track which groups use each schema
        foreach (var group in operationGroups)
        {
            foreach (var (path, operation) in group.Value)
            {
                var usedSchemas = GetUsedSchemas(operation);
                foreach (var schema in usedSchemas)
                {
                    if (!schemaUsage.TryGetValue(schema, out var groups))
                    {
                        groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        schemaUsage[schema] = groups;
                    }

                    groups.Add(group.Key);
                }
            }
        }

        // Return schemas used by more than one group
        var sharedSchemaNames = schemaUsage
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => kvp.Key);
        return new HashSet<string>(sharedSchemaNames, StringComparer.Ordinal);
    }

    private static IEnumerable<string> GetUsedSchemas(
        OpenApiOperation operation)
    {
        var schemas = new HashSet<string>(StringComparer.Ordinal);

        // Check request body
        if (operation.RequestBody?.Content != null)
        {
            foreach (var content in operation.RequestBody.Content.Values)
            {
                AddSchemaReferences(content.Schema, schemas);
            }
        }

        // Check responses
        if (operation.Responses != null)
        {
            foreach (var response in operation.Responses.Values)
            {
                if (response.Content != null)
                {
                    foreach (var content in response.Content.Values)
                    {
                        AddSchemaReferences(content.Schema, schemas);
                    }
                }
            }
        }

        return schemas;
    }

    private static void AddSchemaReferences(
        IOpenApiSchema? schema,
        HashSet<string> schemas)
    {
        if (schema == null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference schemaRef && !string.IsNullOrEmpty(schemaRef.Reference?.Id))
        {
            schemas.Add(schemaRef.Reference!.Id!);
        }

        if (schema is OpenApiSchema actualSchema)
        {
            // Check array items
            if (actualSchema.Items != null)
            {
                AddSchemaReferences(actualSchema.Items, schemas);
            }

            // Check allOf/oneOf/anyOf
            if (actualSchema.AllOf != null)
            {
                foreach (var item in actualSchema.AllOf)
                {
                    AddSchemaReferences(item, schemas);
                }
            }
        }
    }

    private static (string Content, int PathCount, int SchemaCount, int ParameterCount) GeneratePartFileContent(
        OpenApiDocument document,
        string groupName,
        List<(string Path, OpenApiOperation Operation)> operations,
        HashSet<string> sharedSchemas)
    {
        var sb = new StringBuilder();

        // Get unique paths for this group
        var paths = operations
            .Select(o => o.Path).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Get schemas used by this group (excluding shared schemas)
        var usedSchemas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, operation) in operations)
        {
            foreach (var schema in GetUsedSchemas(operation))
            {
                if (!sharedSchemas.Contains(schema))
                {
                    usedSchemas.Add(schema);
                }
            }
        }

        sb.AppendLine("# Part file for " + groupName);
        sb.AppendLine("# This file is auto-merged with the base file");
        sb.AppendLine();
        sb.AppendLine("paths:");

        int pathCount = 0;
        foreach (var path in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (document.Paths.TryGetValue(path, out var pathItem))
            {
                pathCount++;
                sb.AppendLine($"  {path}:");

                // Would serialize path item here - simplified for now
                SerializePathItem(sb, pathItem, "    ");
            }
        }

        var schemaCount = 0;
        if (usedSchemas.Count <= 0 || document.Components?.Schemas == null)
        {
            return (sb.ToString(), pathCount, schemaCount, 0);
        }

        sb.AppendLine();
        sb.AppendLine("components:");
        sb.AppendLine("  schemas:");

        foreach (var schemaName in usedSchemas.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            if (document.Components.Schemas.TryGetValue(schemaName, out var schema))
            {
                schemaCount++;
                sb.AppendLine($"    {schemaName}:");
                SerializeSchema(sb, schema, "      ");
            }
        }

        return (sb.ToString(), pathCount, schemaCount, 0);
    }

    private static (string Content, int SchemaCount, int ParameterCount) GenerateCommonFileContent(
        OpenApiDocument document,
        HashSet<string> sharedSchemas)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Common schemas shared across multiple domains");
        sb.AppendLine("# This file is auto-merged with the base file");
        sb.AppendLine();
        sb.AppendLine("components:");
        sb.AppendLine("  schemas:");

        int schemaCount = 0;
        foreach (var schemaName in sharedSchemas.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            if (document.Components?.Schemas?.TryGetValue(schemaName, out var schema) == true)
            {
                schemaCount++;
                sb.AppendLine($"    {schemaName}:");
                SerializeSchema(sb, schema, "      ");
            }
        }

        return (sb.ToString(), schemaCount, 0);
    }

    private static string GenerateBaseFileContent(
        OpenApiDocument document,
        string baseName,
        IReadOnlyList<string> partNames)
    {
        var sb = new StringBuilder();

        sb.AppendLine("openapi: \"3.1.0\"");
        sb.AppendLine("info:");
        sb.AppendLine($"  title: {document.Info?.Title ?? baseName}");
        sb.AppendLine($"  version: {document.Info?.Version ?? "1.0.0"}");

        if (!string.IsNullOrEmpty(document.Info?.Description))
        {
            sb.AppendLine($"  description: {document.Info!.Description}");
        }

        if (document.Servers?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("servers:");
            foreach (var server in document.Servers)
            {
                sb.AppendLine($"  - url: {server.Url}");
                if (!string.IsNullOrEmpty(server.Description))
                {
                    sb.AppendLine($"    description: {server.Description}");
                }
            }
        }

        // Add x-multipart configuration
        sb.AppendLine();
        sb.AppendLine("x-multipart:");
        sb.AppendLine("  enabled: true");
        sb.AppendLine("  discovery: explicit");
        sb.AppendLine("  parts:");
        foreach (var partName in partNames.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"    - {baseName}_{partName}.yaml");
        }

        if (document.Security?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("security:");
            foreach (var security in document.Security)
            {
                foreach (var scheme in security.Keys)
                {
                    sb.AppendLine($"  - {scheme}: []");
                }
            }
        }

        if (document.Components?.SecuritySchemes?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("components:");
            sb.AppendLine("  securitySchemes:");
            foreach (var scheme in document.Components.SecuritySchemes)
            {
                sb.AppendLine($"    {scheme.Key}:");
                SerializeSecurityScheme(sb, scheme.Value, "      ");
            }
        }

        return sb.ToString();
    }

    private static void SerializePathItem(
        StringBuilder sb,
        IOpenApiPathItem pathItem,
        string indent)
    {
        if (pathItem is not OpenApiPathItem item)
        {
            return;
        }

        if (item.Operations == null)
        {
            return;
        }

        foreach (var operation in item.Operations)
        {
            var method = operation
                .Key
                .ToString()
                .ToLowerInvariant();
            sb.AppendLine($"{indent}{method}:");
            SerializeOperation(sb, operation.Value, indent + "  ");
        }
    }

    private static void SerializeOperation(
        StringBuilder sb,
        OpenApiOperation operation,
        string indent)
    {
        if (!string.IsNullOrEmpty(operation.Summary))
        {
            sb.AppendLine($"{indent}summary: {operation.Summary}");
        }

        if (!string.IsNullOrEmpty(operation.OperationId))
        {
            sb.AppendLine($"{indent}operationId: {operation.OperationId}");
        }

        if (operation.Tags?.Count > 0)
        {
            sb.AppendLine($"{indent}tags:");
            foreach (var tag in operation.Tags)
            {
                sb.AppendLine($"{indent}  - {tag.Name}");
            }
        }

        if (operation.Parameters?.Count > 0)
        {
            sb.AppendLine($"{indent}parameters:");
            foreach (var param in operation.Parameters)
            {
                sb.AppendLine($"{indent}  - name: {param.Name}");
                sb.AppendLine($"{indent}    in: {param.In?.ToStringLowerCase()}");
                if (param.Required)
                {
                    sb.AppendLine($"{indent}    required: true");
                }

                if (param.Schema != null)
                {
                    sb.AppendLine($"{indent}    schema:");
                    SerializeSchema(sb, param.Schema, indent + "      ");
                }
            }
        }

        if (operation.RequestBody != null)
        {
            sb.AppendLine($"{indent}requestBody:");
            if (operation.RequestBody.Required)
            {
                sb.AppendLine($"{indent}  required: true");
            }

            if (operation.RequestBody.Content?.Count > 0)
            {
                sb.AppendLine($"{indent}  content:");
                foreach (var content in operation.RequestBody.Content)
                {
                    sb.AppendLine($"{indent}    {content.Key}:");
                    sb.AppendLine($"{indent}      schema:");
                    SerializeSchema(sb, content.Value.Schema, indent + "        ");
                }
            }
        }

        if (operation.Responses?.Count > 0)
        {
            sb.AppendLine($"{indent}responses:");
            foreach (var response in operation.Responses)
            {
                sb.AppendLine($"{indent}  '{response.Key}':");
                sb.AppendLine($"{indent}    description: {response.Value.Description ?? "Response"}");
                if (response.Value.Content?.Count > 0)
                {
                    sb.AppendLine($"{indent}    content:");
                    foreach (var content in response.Value.Content)
                    {
                        sb.AppendLine($"{indent}      {content.Key}:");
                        sb.AppendLine($"{indent}        schema:");
                        SerializeSchema(sb, content.Value.Schema, indent + "          ");
                    }
                }
            }
        }
    }

    private static void SerializeSchema(
        StringBuilder sb,
        IOpenApiSchema? schema,
        string indent)
    {
        if (schema == null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference schemaRef)
        {
            sb.AppendLine($"{indent}$ref: '#/components/schemas/{schemaRef.Reference?.Id}'");
            return;
        }

        if (schema is not OpenApiSchema actualSchema)
        {
            return;
        }

        if (actualSchema.Type != null)
        {
            sb.AppendLine($"{indent}type: {actualSchema.Type}");
        }

        if (!string.IsNullOrEmpty(actualSchema.Format))
        {
            sb.AppendLine($"{indent}format: {actualSchema.Format}");
        }

        if (!string.IsNullOrEmpty(actualSchema.Title))
        {
            sb.AppendLine($"{indent}title: {actualSchema.Title}");
        }

        if (actualSchema.Items != null)
        {
            sb.AppendLine($"{indent}items:");
            SerializeSchema(sb, actualSchema.Items, indent + "  ");
        }

        if (actualSchema.Properties?.Count > 0)
        {
            sb.AppendLine($"{indent}properties:");
            foreach (var prop in actualSchema.Properties)
            {
                sb.AppendLine($"{indent}  {prop.Key}:");
                SerializeSchema(sb, prop.Value, indent + "    ");
            }
        }

        if (actualSchema.Required?.Count > 0)
        {
            sb.AppendLine($"{indent}required:");
            foreach (var req in actualSchema.Required)
            {
                sb.AppendLine($"{indent}  - {req}");
            }
        }
    }

    private static void SerializeSecurityScheme(
        StringBuilder sb,
        IOpenApiSecurityScheme scheme,
        string indent)
    {
        if (scheme is not OpenApiSecurityScheme actualScheme)
        {
            return;
        }

        var lowerType = actualScheme
            .Type
            .ToString()
            .ToLowerInvariant();
        sb.AppendLine($"{indent}type: {lowerType}");

        if (!string.IsNullOrEmpty(actualScheme.Scheme))
        {
            sb.AppendLine($"{indent}scheme: {actualScheme.Scheme}");
        }

        if (!string.IsNullOrEmpty(actualScheme.BearerFormat))
        {
            sb.AppendLine($"{indent}bearerFormat: {actualScheme.BearerFormat}");
        }
    }

    private static (SplitStrategy Strategy, string Reason) DetermineRecommendedStrategy(
        OpenApiDocument document,
        Dictionary<string, TagAnalysis> tagAnalysis,
        Dictionary<string, PathSegmentAnalysis> pathSegmentAnalysis)
    {
        // If well-tagged (most operations have tags), recommend ByTag
        var totalOperations = document.Paths?.Sum(p => p.Value?.Operations?.Count ?? 0) ?? 0;
        var taggedOperations = tagAnalysis.Values.Sum(t => t.OperationCount);

        if (taggedOperations > totalOperations * 0.8)
        {
            return (SplitStrategy.ByTag, "Most operations are well-tagged, grouping by tag recommended");
        }

        // If path segments are distinct and roughly balanced, recommend ByPathSegment
        if (pathSegmentAnalysis.Count > 1 && pathSegmentAnalysis.Count < 10)
        {
            var avgOps = (double)totalOperations / pathSegmentAnalysis.Count;
            var allBalanced = pathSegmentAnalysis.Values.All(p =>
                p.OperationCount >= avgOps * 0.3 && p.OperationCount <= avgOps * 3);

            if (allBalanced)
            {
                return (SplitStrategy.ByPathSegment, "Path segments are well-balanced, grouping by path segment recommended");
            }
        }

        // Default to ByDomain which uses a combination approach
        return (SplitStrategy.ByDomain, "Mixed organization, using domain-based grouping");
    }

    private static IReadOnlyList<SuggestedSplit> GenerateSuggestedSplits(
        OpenApiDocument document,
        SplitStrategy strategy,
        Dictionary<string, TagAnalysis> tagAnalysis,
        Dictionary<string, PathSegmentAnalysis> pathSegmentAnalysis)
    {
        var suggestions = new List<SuggestedSplit>();

        var groups = strategy == SplitStrategy.ByTag
            ? tagAnalysis.ToDictionary(t => t.Key, t => t.Value.OperationCount, StringComparer.Ordinal)
            : pathSegmentAnalysis.ToDictionary(p => p.Key, p => p.Value.OperationCount, StringComparer.Ordinal);

        foreach (var group in groups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            suggestions.Add(new SuggestedSplit(
                fileName: $"{{BaseName}}_{group.Key}.yaml",
                description: $"Contains {group.Value} operation(s) for {group.Key}",
                partName: group.Key,
                estimatedOperations: group.Value,
                estimatedLines: group.Value * 50)); // Rough estimate
        }

        return suggestions;
    }

    private static IReadOnlyList<SharedSchemaAnalysis> IdentifySharedSchemasForAnalysis(
        OpenApiDocument document,
        SplitStrategy strategy,
        Dictionary<string, TagAnalysis> tagAnalysis,
        Dictionary<string, PathSegmentAnalysis> pathSegmentAnalysis)
    {
        var schemaUsage = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        if (document.Paths == null)
        {
            return Array.Empty<SharedSchemaAnalysis>();
        }

        foreach (var path in document.Paths)
        {
            if (path.Value?.Operations == null)
            {
                continue;
            }

            foreach (var operation in path.Value.Operations.Values)
            {
                var groupName = strategy switch
                {
                    SplitStrategy.ByTag => operation.Tags?.FirstOrDefault()?.Name ?? "Untagged",
                    SplitStrategy.ByPathSegment => GetFirstPathSegment(path.Key),
                    _ => GetDomainFromOperation(path.Key, operation),
                };

                var usedSchemas = GetUsedSchemas(operation);
                foreach (var schema in usedSchemas)
                {
                    if (!schemaUsage.TryGetValue(schema, out var groups))
                    {
                        groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        schemaUsage[schema] = groups;
                    }

                    groups.Add(groupName);
                }
            }
        }

        return schemaUsage
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => new SharedSchemaAnalysis(
                name: kvp.Key,
                usedByDomains: kvp
                    .Value
                    .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}