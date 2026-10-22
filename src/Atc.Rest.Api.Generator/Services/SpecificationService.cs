// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
namespace Atc.Rest.Api.Generator.Services;

/// <summary>
/// Service for handling OpenAPI specifications including multi-part file support.
/// Provides methods for reading, merging, splitting, and validating specifications.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Diagnostic reporting for all parsing errors.")]
public static class SpecificationService
{
    private const string MultiPartExtensionKey = "x-multipart";

    // The owner of a schema the base file keeps: a split group name is never empty.
    private const string SplitBaseOwner = "";

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
    /// Reads a specification and merges its part files (<c>{Base}_{Part}.yaml</c> or <c>x-multipart</c>) like
    /// the build does. When parts were merged, every <c>$ref</c> of the merged document - also one written in a
    /// part file - resolves against the merged document.
    /// </summary>
    /// <param name="baseFilePath">Path to the base YAML file.</param>
    /// <returns>
    /// The merge result, and the merged specification (the base file itself when it has no parts), or null
    /// when the merge failed.
    /// </returns>
    public static (MergeResult Merge, SpecificationFile? Specification) ReadAndMergeResolved(
        string baseFilePath)
    {
        var merge = ReadAndMerge(baseFilePath);
        if (!merge.IsSuccess || merge.Document is null)
        {
            return (merge, null);
        }

        if (!merge.IsMultiPart)
        {
            return (merge, merge.BaseFile);
        }

        // The merge already points every reference at the merged document.
        return (merge, SpecificationFile.FromDocument(baseFilePath, SerializeToYaml(merge.Document), merge.Document));
    }

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
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        using var writer = new StringWriter();
        Serialize(document, new OpenApiYamlWriter(writer));
        return writer.ToString();
    }

    /// <summary>
    /// Serializes an OpenAPI document to JSON format.
    /// </summary>
    /// <param name="document">The OpenAPI document to serialize.</param>
    /// <returns>JSON string representation of the document.</returns>
    public static string SerializeToJson(OpenApiDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        using var writer = new StringWriter();
        Serialize(document, new OpenApiJsonWriter(writer));
        return writer.ToString();
    }

    /// <summary>
    /// Writes the document in the OpenAPI version it was parsed as (3.0 when unknown): writing a 3.1 / 3.2
    /// document as 3.0 would turn type arrays into `nullable`, and drop `query:` operations and webhooks.
    /// </summary>
    private static void Serialize(
        OpenApiDocument document,
        IOpenApiWriter writer)
    {
        var version = document.GetOpenApiSpecVersion();
        if (version is OpenApiSpecVersion.OpenApi3_1 or OpenApiSpecVersion.OpenApi3_2)
        {
            // The 3.0 `nullable: true` in a 3.1+ document is kept as an unrecognized keyword (and honored by the
            // generator); written back it would become `unrecognizedKeywords: { nullable: true }`. Write it the
            // 3.1 way, as a `null` type - the same meaning.
            new OpenApiWalker(new NullableKeywordNormalizer()).Walk(document);
        }

        // The library writes a schema's unrecognized keywords (JSON Schema keywords it has no property for, such as
        // `prefixItems`, or `summary` on a schema) nested under an `unrecognizedKeywords` key, which reads back as
        // a different schema. Write them in place instead, and leave the document as it was.
        var inliner = new UnrecognizedKeywordInliner();
        new OpenApiWalker(inliner).Walk(document);
        try
        {
            switch (version)
            {
                case OpenApiSpecVersion.OpenApi3_2:
                    document.SerializeAsV32(writer);
                    break;
                case OpenApiSpecVersion.OpenApi3_1:
                    document.SerializeAsV31(writer);
                    break;
                default:
                    document.SerializeAsV3(writer);
                    break;
            }
        }
        finally
        {
            inliner.Restore();
        }
    }

    /// <summary>
    /// Moves each schema's unrecognized keywords into its extensions (written under their own name) until
    /// <see cref="Restore"/>.
    /// </summary>
    private sealed class UnrecognizedKeywordInliner : OpenApiVisitorBase
    {
        private readonly List<(OpenApiSchema Schema, IDictionary<string, JsonNode> Keywords, IDictionary<string, IOpenApiExtension>? Extensions)> moved = [];

        public override void Visit(IOpenApiSchema schema)
        {
            if (schema is not OpenApiSchema { UnrecognizedKeywords.Count: > 0 } actualSchema ||
                moved.Exists(m => ReferenceEquals(m.Schema, actualSchema)))
            {
                return;
            }

            var extensions = actualSchema.Extensions is null
                ? new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
                : new Dictionary<string, IOpenApiExtension>(actualSchema.Extensions, StringComparer.Ordinal);
            foreach (var keyword in actualSchema.UnrecognizedKeywords)
            {
                if (keyword.Value is not null && !extensions.ContainsKey(keyword.Key))
                {
                    extensions[keyword.Key] = new JsonNodeExtension(keyword.Value);
                }
            }

            moved.Add((actualSchema, actualSchema.UnrecognizedKeywords, actualSchema.Extensions));
            actualSchema.Extensions = extensions;
            actualSchema.UnrecognizedKeywords = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        }

        public void Restore()
        {
            foreach (var (schema, keywords, extensions) in moved)
            {
                schema.UnrecognizedKeywords = keywords;
                schema.Extensions = extensions;
            }
        }
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
        var baseName = Path.GetFileNameWithoutExtension(baseFilePath);
        return DiscoverPartFilePaths(baseFilePath)
            .Select(partFilePath => SpecificationFile.FromFile(partFilePath, baseName))
            .ToList();
    }

    /// <summary>
    /// The part files of a multi-part specification, by the one convention the build and every CLI command use: the
    /// files named <c>{BaseName}_{PartName}</c> next to the base file with a specification extension (<c>.yaml</c>,
    /// <c>.yml</c> or <c>.json</c>, whatever the base file's own), sorted by name.
    /// </summary>
    /// <param name="baseFilePath">Path to the base file.</param>
    /// <returns>The part files' paths, sorted.</returns>
    public static IReadOnlyList<string> DiscoverPartFilePaths(
        string baseFilePath)
    {
        var directory = Path.GetDirectoryName(baseFilePath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(baseFilePath);
        return Directory
            .GetFiles(directory, $"{baseName}_*")
            .Where(f => !f.Equals(baseFilePath, StringComparison.OrdinalIgnoreCase) &&
                        Path.GetExtension(f) is { } extension &&
                        (extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                         extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
                         extension.Equals(".json", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
        if (baseFile.Document is null)
        {
            return MergeResult.Failed(
                RuleIdentifiers.ServerParsingError,
                $"Failed to parse base file: {baseFilePath}",
                baseFilePath);
        }

        // Check for x-multipart extension in base file
        var docConfig = ExtractMultiPartConfig(baseFile.Document);
        if (docConfig is not null)
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

        if (baseFile.Document is null)
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
            if (partFile.Document is null)
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
            diagnostics.AddRange(ValidatePartAgainstBase(partFile, baseFile));

            // Merge paths
            MergePaths(mergedDocument, partFile.Document, partFile.FilePath, config, diagnostics);

            // Merge schemas
            MergeSchemas(mergedDocument, partFile.Document, partFile.FilePath, config, diagnostics);

            // Merge parameters
            MergeParameters(mergedDocument, partFile.Document, partFile.FilePath, config, diagnostics);

            // Merge tags
            MergeTags(mergedDocument, partFile.Document);
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
        if (diagnostics.All(d => d.Severity != DiagnosticSeverity.Error))
        {
            diagnostics.Add(DiagnosticMessage.Info(
                RuleIdentifiers.MultiPartMergeSuccessful,
                $"Successfully merged {partFiles.Count} part file(s) with base file"));
        }

        var hasErrors = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        if (hasErrors)
        {
            return MergeResult.Failed(diagnostics);
        }

        // A $ref written in a part file points at the part's own document: point every reference at the merged
        // document, so it resolves there (array aliases, allOf bases and the like are followed by reference target).
        // Done in memory - a YAML round trip would lose what the parser keeps as-is (e.g. `nullable` in 3.1).
        var walker = new OpenApiWalker(new MergedDocumentReferenceSetter(mergedDocument));
        walker.Walk(mergedDocument);
        mergedDocument.RegisterComponents();

        return MergeResult.Success(mergedDocument, baseFile, partFiles, diagnostics);
    }

    /// <summary>
    /// Turns a 3.0 <c>nullable: true</c> (an unrecognized keyword in a 3.1+ document) into a <c>null</c> type.
    /// </summary>
    private sealed class NullableKeywordNormalizer : OpenApiVisitorBase
    {
        public override void Visit(IOpenApiSchema schema)
        {
            if (schema is OpenApiSchema { UnrecognizedKeywords: { } keywords } actualSchema &&
                keywords.TryGetValue("nullable", out var value) &&
                value is not null &&
                string.Equals(value.ToString(), "true", StringComparison.OrdinalIgnoreCase))
            {
                keywords.Remove("nullable");
                if (actualSchema.Type is { } type)
                {
                    actualSchema.Type = type | JsonSchemaType.Null;
                }
            }
        }
    }

    /// <summary>
    /// Points every reference of a merged document at that document.
    /// </summary>
    private sealed class MergedDocumentReferenceSetter : OpenApiVisitorBase
    {
        // HostDocument is public but init-only: the parser sets it once, to the document it read.
        private static readonly System.Reflection.PropertyInfo HostDocumentProperty =
            typeof(BaseOpenApiReference).GetProperty(nameof(BaseOpenApiReference.HostDocument))!;

        private readonly OpenApiDocument document;

        public MergedDocumentReferenceSetter(OpenApiDocument document)
            => this.document = document;

        public override void Visit(IOpenApiReferenceHolder referenceHolder)
        {
            if (referenceHolder?.GetType().GetProperty("Reference")?.GetValue(referenceHolder) is BaseOpenApiReference reference)
            {
                HostDocumentProperty.SetValue(reference, document);
            }
        }
    }

    /// <summary>
    /// Splits an OpenAPI specification into a base file and part files (<c>{Base}_{Part}.yaml</c>) that the
    /// build and <c>spec merge</c> merge back into the same document.
    /// </summary>
    /// <remarks>
    /// Each path goes to one part, whole. A component schema that only one part's paths reach (also through
    /// other schemas) moves to that part; a schema several parts share moves to <c>{Base}_Common.yaml</c> when
    /// <paramref name="extractCommon"/> is set. Everything else - the other components, schemas the base file's
    /// components or webhooks reach, unused schemas, webhooks, security, tags and extensions - stays in the base file.
    /// </remarks>
    /// <param name="document">The OpenAPI document to split.</param>
    /// <param name="baseName">The base name for generated files.</param>
    /// <param name="strategy">The split strategy to use.</param>
    /// <param name="extractCommon">Whether to extract shared schemas to a common file.</param>
    /// <param name="minOperations">
    /// The fewest operations a part file may have; smaller groups are put together in an <c>Other</c> part.
    /// </param>
    /// <returns>The split result with file contents.</returns>
    public static SplitResult Split(
        OpenApiDocument document,
        string baseName,
        SplitStrategy strategy,
        bool extractCommon = true,
        int minOperations = 1)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var diagnostics = new List<DiagnosticMessage>();
        var pathGroups = MergeSmallGroups(
            document,
            GroupPathsByStrategy(document, strategy, diagnostics),
            minOperations);
        var schemaOwners = FindSchemaOwners(document, pathGroups);

        var partSchemas = new Dictionary<string, Dictionary<string, IOpenApiSchema>>(StringComparer.OrdinalIgnoreCase);
        var commonSchemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        var baseSchemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        foreach (var schema in document.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            var owners = schemaOwners.TryGetValue(schema.Key, out var found)
                ? found
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (owners.Contains(SplitBaseOwner) || owners.Count == 0)
            {
                baseSchemas[schema.Key] = schema.Value;
            }
            else if (owners.Count == 1)
            {
                var owner = owners.First();
                if (!partSchemas.TryGetValue(owner, out var ownSchemas))
                {
                    ownSchemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
                    partSchemas[owner] = ownSchemas;
                }

                ownSchemas[schema.Key] = schema.Value;
            }
            else if (extractCommon)
            {
                commonSchemas[schema.Key] = schema.Value;
            }
            else
            {
                baseSchemas[schema.Key] = schema.Value;
            }
        }

        var partFiles = new List<SplitFileContent>();
        foreach (var group in pathGroups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var paths = new OpenApiPaths();
            foreach (var path in group.Value)
            {
                paths[path] = document.Paths[path];
            }

            var schemas = partSchemas.TryGetValue(group.Key, out var ownSchemas)
                ? ownSchemas
                : new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

            partFiles.Add(new SplitFileContent(
                fileName: $"{baseName}_{group.Key}.yaml",
                content: SerializePartDocument(document, paths, schemas, $"{group.Key} - part of {baseName}.yaml, merged with it at build time"),
                partName: group.Key,
                isBaseFile: false,
                isCommonFile: false,
                pathCount: paths.Count,
                schemaCount: schemas.Count,
                parameterCount: 0));
        }

        SplitFileContent? commonFile = null;
        if (commonSchemas.Count > 0)
        {
            commonFile = new SplitFileContent(
                fileName: $"{baseName}_Common.yaml",
                content: SerializePartDocument(document, new OpenApiPaths(), commonSchemas, $"Schemas shared by the parts of {baseName}.yaml"),
                partName: "Common",
                isBaseFile: false,
                isCommonFile: true,
                pathCount: 0,
                schemaCount: commonSchemas.Count,
                parameterCount: 0);
        }

        // The base file: the document without its paths and the schemas that moved to a part. Parts are found by
        // the {Base}_{Part}.yaml naming convention, which the build, `spec merge` and the CLI generators all use.
        var baseDocument = CloneOpenApiDocument(document);
        baseDocument.Paths = new OpenApiPaths();
        baseDocument.Extensions?.Remove(MultiPartExtensionKey);
        if (baseDocument.Components is not null)
        {
            baseDocument.Components.Schemas = baseSchemas.Count > 0 ? baseSchemas : null;
        }

        var baseFile = new SplitFileContent(
            fileName: $"{baseName}.yaml",
            content: SerializeToYaml(baseDocument),
            partName: null,
            isBaseFile: true,
            isCommonFile: false,
            pathCount: 0,
            schemaCount: baseSchemas.Count,
            parameterCount: baseDocument.Components?.Parameters?.Count ?? 0);

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

        if (document.Paths is not null)
        {
            foreach (var path in document.Paths)
            {
                var segment = GetFirstPathSegment(path.Key);
                if (path.Value?.Operations is null)
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
                    if (operation.Tags is null)
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
        var suggestedSplits = GenerateSuggestedSplits(strategy, tagAnalysis, pathSegmentAnalysis);

        // Identify shared schemas
        var sharedSchemas = IdentifySharedSchemasForAnalysis(document, strategy);

        const int totalLines = 0; // Would need original content to calculate

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
    /// <param name="sourceText">The raw specification text, when available. Rules that read the text rather than the parsed document need it.</param>
    /// <returns>List of diagnostic messages.</returns>
    public static IReadOnlyList<DiagnosticMessage> Validate(
        OpenApiDocument document,
        string filePath,
        ValidateSpecificationStrategy strategy = ValidateSpecificationStrategy.Standard,
        string? sourceText = null)
        => Validators.OpenApiDocumentValidator.Validate(
            strategy,
            document,
            Array.Empty<OpenApiError>(),
            filePath,
            sourceText);

    /// <summary>
    /// Validates a part file for prohibited sections.
    /// </summary>
    /// <param name="partFile">The part file to validate.</param>
    /// <returns>List of diagnostic messages.</returns>
    public static IReadOnlyList<DiagnosticMessage> ValidatePartFile(
        SpecificationFile partFile)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (partFile.Document is null)
        {
            return diagnostics;
        }

        // Part files should not have info section with version
        if (partFile.Document.Info?.Version is not null)
        {
            diagnostics.Add(DiagnosticMessage.Warning(
                RuleIdentifiers.PartFileInfoIgnored,
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
    /// What a part is next to its base: another OpenAPI version (ATC_API_MPT012 - the part is read by its own), or
    /// nothing to contribute (ATC_API_MPT013 - a <c>paths</c> that is no map, an empty file).
    /// </summary>
    private static List<DiagnosticMessage> ValidatePartAgainstBase(
        SpecificationFile partFile,
        SpecificationFile baseFile)
    {
        var diagnostics = new List<DiagnosticMessage>();
        if (partFile.Document is not { } part || baseFile.Document is not { } baseDocument)
        {
            return diagnostics;
        }

        var partVersion = part.GetOpenApiSpecVersion();
        var baseVersion = baseDocument.GetOpenApiSpecVersion();
        if (partVersion is { } declared && baseVersion is { } expected && declared != expected)
        {
            diagnostics.Add(DiagnosticMessage.Warning(
                RuleIdentifiers.PartFileOpenApiVersionDiffers,
                $"Part file '{partFile.FileName}' declares OpenAPI {VersionText(partVersion)} while the base file declares {VersionText(baseVersion)}; the part is read by its own version.",
                partFile.FilePath));
        }

        var components = part.Components;
        var contributes = part.Paths is { Count: > 0 } ||
                          part.Webhooks is { Count: > 0 } ||
                          part.Tags is { Count: > 0 } ||
                          components?.Schemas is { Count: > 0 } ||
                          components?.Parameters is { Count: > 0 } ||
                          components?.Responses is { Count: > 0 } ||
                          components?.RequestBodies is { Count: > 0 } ||
                          components?.Headers is { Count: > 0 } ||
                          components?.SecuritySchemes is { Count: > 0 };
        if (!contributes)
        {
            diagnostics.Add(DiagnosticMessage.Warning(
                RuleIdentifiers.PartFileContributesNothing,
                $"Part file '{partFile.FileName}' contributes nothing to the merge: no paths, no components (a 'paths' that is no map is read as none).",
                partFile.FilePath));
        }

        return diagnostics;

        static string VersionText(OpenApiSpecVersion? version)
            => version switch
            {
                OpenApiSpecVersion.OpenApi3_2 => "3.2",
                OpenApiSpecVersion.OpenApi3_1 => "3.1",
                OpenApiSpecVersion.OpenApi3_0 => "3.0",
                null => "no version",
                _ => version.ToString()!,
            };
    }

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
        if (document.Extensions is null ||
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
            Info = source.Info is not null
                ? new OpenApiInfo
                {
                    Title = source.Info.Title,
                    Version = source.Info.Version,
                    Description = source.Info.Description,
                    TermsOfService = source.Info.TermsOfService,
                    Contact = source.Info.Contact,
                    License = source.Info.License,
                    Summary = source.Info.Summary,
                    Extensions = source.Info.Extensions,
                }
                : null!,
            ExternalDocs = source.ExternalDocs,
            JsonSchemaDialect = source.JsonSchemaDialect,
        };

        // Copy servers
        if (source.Servers is not null)
        {
            clone.Servers = new List<OpenApiServer>(source.Servers);
        }

        // Copy paths (shallow copy of the dictionary, but each path item is shared)
        if (source.Paths is not null)
        {
            clone.Paths = new OpenApiPaths();
            foreach (var path in source.Paths)
            {
                clone.Paths[path.Key] = path.Value;
            }
        }

        // Copy components
        if (source.Components is not null)
        {
            clone.Components = new OpenApiComponents();

            if (source.Components.Schemas is not null)
            {
                clone.Components.Schemas = new Dictionary<string, IOpenApiSchema>(
                    source.Components.Schemas,
                    StringComparer.Ordinal);
            }

            if (source.Components.Parameters is not null)
            {
                clone.Components.Parameters = new Dictionary<string, IOpenApiParameter>(
                    source.Components.Parameters,
                    StringComparer.Ordinal);
            }

            if (source.Components.SecuritySchemes is not null)
            {
                clone.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>(
                    source.Components.SecuritySchemes,
                    StringComparer.Ordinal);
            }

            if (source.Components.RequestBodies is not null)
            {
                clone.Components.RequestBodies = new Dictionary<string, IOpenApiRequestBody>(
                    source.Components.RequestBodies,
                    StringComparer.Ordinal);
            }

            if (source.Components.Responses is not null)
            {
                clone.Components.Responses = new Dictionary<string, IOpenApiResponse>(
                    source.Components.Responses,
                    StringComparer.Ordinal);
            }

            if (source.Components.Headers is not null)
            {
                clone.Components.Headers = new Dictionary<string, IOpenApiHeader>(source.Components.Headers, StringComparer.Ordinal);
            }

            if (source.Components.Examples is not null)
            {
                clone.Components.Examples = new Dictionary<string, IOpenApiExample>(source.Components.Examples, StringComparer.Ordinal);
            }

            if (source.Components.Links is not null)
            {
                clone.Components.Links = new Dictionary<string, IOpenApiLink>(source.Components.Links, StringComparer.Ordinal);
            }

            if (source.Components.Callbacks is not null)
            {
                clone.Components.Callbacks = new Dictionary<string, IOpenApiCallback>(source.Components.Callbacks, StringComparer.Ordinal);
            }

            if (source.Components.PathItems is not null)
            {
                clone.Components.PathItems = new Dictionary<string, IOpenApiPathItem>(source.Components.PathItems, StringComparer.Ordinal);
            }

            if (source.Components.MediaTypes is not null)
            {
                clone.Components.MediaTypes = new Dictionary<string, IOpenApiMediaType>(source.Components.MediaTypes, StringComparer.Ordinal);
            }

            if (source.Components.Extensions is not null)
            {
                clone.Components.Extensions = new Dictionary<string, IOpenApiExtension>(source.Components.Extensions, StringComparer.Ordinal);
            }
        }

        // Copy webhooks (OpenAPI 3.1+)
        if (source.Webhooks is not null)
        {
            clone.Webhooks = new Dictionary<string, IOpenApiPathItem>(source.Webhooks, StringComparer.Ordinal);
        }

        // Copy tags
        if (source.Tags is not null)
        {
            clone.Tags = new HashSet<OpenApiTag>(source.Tags);
        }

        // Copy security requirements
        if (source.Security is not null)
        {
            clone.Security = new List<OpenApiSecurityRequirement>(source.Security);
        }

        // Copy extensions
        if (source.Extensions is not null)
        {
            clone.Extensions = new Dictionary<string, IOpenApiExtension>(
                source.Extensions,
                StringComparer.Ordinal);
        }

        // Copy metadata (non-serialized annotation bag) so the parsed OpenAPI spec
        // version stamped at parse time survives into the merged document.
        if (source.Metadata is not null)
        {
            clone.Metadata = new Dictionary<string, object>(
                source.Metadata,
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
        if (source.Paths is null)
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

                    case MergeStrategy.MergeIfIdentical:
                        if (!AreOpenApiElementsIdentical(
                            target.Paths[path.Key],
                            path.Value))
                        {
                            diagnostics.Add(DiagnosticMessage.Error(
                                RuleIdentifiers.NonIdenticalMergeConflict,
                                $"Path '{path.Key}' exists in both base and part file with different definitions",
                                sourceFilePath));
                        }

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
        if (source.Components?.Schemas is null)
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
                        if (!AreOpenApiElementsIdentical(
                            target.Components.Schemas[schema.Key],
                            schema.Value))
                        {
                            diagnostics.Add(DiagnosticMessage.Error(
                                RuleIdentifiers.NonIdenticalMergeConflict,
                                $"Schema '{schema.Key}' exists in both base and part file with different definitions",
                                sourceFilePath));
                        }

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
                target.Components.Schemas[schema.Key] = schema.Value;
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
        if (source.Components?.Parameters is null)
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
                            RuleIdentifiers.DuplicateParameterInPart,
                            $"Duplicate parameter '{parameter.Key}' found in part file",
                            sourceFilePath));
                        break;

                    case MergeStrategy.MergeIfIdentical:
                        if (!AreOpenApiElementsIdentical(
                            target.Components.Parameters[parameter.Key],
                            parameter.Value))
                        {
                            diagnostics.Add(DiagnosticMessage.Error(
                                RuleIdentifiers.NonIdenticalMergeConflict,
                                $"Parameter '{parameter.Key}' exists in both base and part file with different definitions",
                                sourceFilePath));
                        }

                        break;

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
                target.Components.Parameters[parameter.Key] = parameter.Value;
            }
        }
    }

    private static void MergeTags(
        OpenApiDocument target,
        OpenApiDocument source)
    {
        if (source.Tags is null || source.Tags.Count == 0)
        {
            return;
        }

        var existingTags = target.Tags is not null
            ? new HashSet<string>(
                target
                    .Tags
                    .Select(t => t.Name)
                    .Where(n => n is not null)
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

    private static bool AreOpenApiElementsIdentical(
        IOpenApiSerializable existing,
        IOpenApiSerializable incoming)
    {
        using var existingWriter = new StringWriter();
        existing.SerializeAsV3(new OpenApiJsonWriter(existingWriter));

        using var incomingWriter = new StringWriter();
        incoming.SerializeAsV3(new OpenApiJsonWriter(incomingWriter));

        return string.Equals(
            existingWriter.ToString(),
            incomingWriter.ToString(),
            StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ValidateReferences(
        OpenApiDocument document)
    {
        var unresolvedRefs = new List<string>();
        var validSchemaNames = document.Components?.Schemas?.Keys is not null
            ? new HashSet<string>(document.Components.Schemas.Keys, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        // Check for unresolved schema references in paths
        if (document.Paths is not null)
        {
            foreach (var path in document.Paths)
            {
                if (path.Value?.Operations is null)
                {
                    continue;
                }

                foreach (var operation in path.Value.Operations.Values)
                {
                    // Check request body
                    if (operation.RequestBody?.Content is not null)
                    {
                        foreach (var content in operation.RequestBody.Content.Values)
                        {
                            CheckSchemaReference(content.Schema, validSchemaNames, unresolvedRefs);
                        }
                    }

                    // Check responses
                    if (operation.Responses is not null)
                    {
                        foreach (var response in operation.Responses.Values)
                        {
                            if (response.Content is not null)
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

        return
        [
            .. unresolvedRefs
                .Distinct(StringComparer.Ordinal)
        ];
    }

    private static void CheckSchemaReference(
        IOpenApiSchema? schema,
        HashSet<string> validSchemaNames,
        List<string> unresolvedRefs)
    {
        if (schema is null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refId = schemaRef.Reference.Id;
            if (!string.IsNullOrEmpty(refId) && !validSchemaNames.Contains(refId!))
            {
                unresolvedRefs.Add(refId!);
            }
        }

        // Check array items
        if (schema is OpenApiSchema { Items: not null } actualSchema)
        {
            CheckSchemaReference(actualSchema.Items, validSchemaNames, unresolvedRefs);
        }
    }

    /// <summary>
    /// Groups the paths by the split strategy. A path is the unit the merge works with (the same path in two part
    /// files is a conflict), so each path goes to one group: the one most of its operations belong to.
    /// </summary>
    private static Dictionary<string, List<string>> GroupPathsByStrategy(
        OpenApiDocument document,
        SplitStrategy strategy,
        List<DiagnosticMessage> diagnostics)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (document.Paths is null)
        {
            return groups;
        }

        foreach (var path in document.Paths)
        {
            var operationGroups = (path.Value?.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
                .Select(operation => GetSplitGroupName(path.Key, operation, strategy))
                .ToList();

            // The most common group; on a tie the first operation's (GroupBy keeps the first-seen order).
            var groupName = operationGroups.Count == 0
                ? GetSplitGroupName(path.Key, operation: null, strategy)
                : operationGroups
                    .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Key;

            var distinctGroups = operationGroups
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (distinctGroups.Count > 1)
            {
                diagnostics.Add(DiagnosticMessage.Warning(
                    RuleIdentifiers.SplitPathSpansGroups,
                    $"Path '{path.Key}' has operations for {string.Join(", ", distinctGroups)}; a path cannot be split, so all of it goes to '{groupName}'"));
            }

            if (!groups.TryGetValue(groupName, out var paths))
            {
                paths = [];
                groups[groupName] = paths;
            }

            paths.Add(path.Key);
        }

        return groups;
    }

    /// <summary>
    /// Puts the groups with fewer than <paramref name="minOperations"/> operations together in an <c>Other</c> group.
    /// </summary>
    private static Dictionary<string, List<string>> MergeSmallGroups(
        OpenApiDocument document,
        Dictionary<string, List<string>> groups,
        int minOperations)
    {
        const string otherGroup = "Other";
        var small = groups
            .Where(g => !string.Equals(g.Key, otherGroup, StringComparison.OrdinalIgnoreCase) &&
                        g.Value.Sum(path => document.Paths[path]?.Operations?.Count ?? 0) < minOperations)
            .Select(g => g.Key)
            .ToList();

        // One small group on its own is not worth renaming.
        if (small.Count < 2 && !groups.ContainsKey(otherGroup))
        {
            return groups;
        }

        if (!groups.TryGetValue(otherGroup, out var otherPaths))
        {
            otherPaths = [];
            groups[otherGroup] = otherPaths;
        }

        foreach (var name in small)
        {
            otherPaths.AddRange(groups[name]);
            groups.Remove(name);
        }

        return groups;
    }

    private static string GetSplitGroupName(
        string path,
        OpenApiOperation? operation,
        SplitStrategy strategy)
    {
        var groupName = strategy switch
        {
            SplitStrategy.ByTag => operation?.Tags?.FirstOrDefault()?.Name ?? "Untagged",
            SplitStrategy.ByPathSegment => GetFirstPathSegment(path),
            SplitStrategy.ByDomain => operation is null ? GetFirstPathSegment(path) : GetDomainFromOperation(path, operation),
            _ => "Default",
        };

        // The group name becomes a file name ({Base}_{Group}.yaml): a tag may hold quotes, slashes
        // or other characters a file name cannot, so only letters and digits are kept.
        groupName = new string(groupName.ToPascalCaseForDotNet().Where(char.IsLetterOrDigit).ToArray());
        return groupName.Length == 0 ? "Default" : groupName;
    }

    /// <summary>
    /// Finds, for each component schema, the split groups whose paths reach it - directly or through other
    /// schemas. A schema reached from anything that stays in the base file (the other components, webhooks) is
    /// owned by the base (<see cref="SplitBaseOwner"/>).
    /// </summary>
    private static Dictionary<string, HashSet<string>> FindSchemaOwners(
        OpenApiDocument document,
        Dictionary<string, List<string>> pathGroups)
    {
        var owners = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var schemas = document.Components?.Schemas;
        if (schemas is null || schemas.Count == 0)
        {
            return owners;
        }

        foreach (var group in pathGroups)
        {
            var groupPaths = new OpenApiPaths();
            foreach (var path in group.Value)
            {
                groupPaths[path] = document.Paths[path];
            }

            MarkOwner(group.Key, CollectSchemaReferences(new OpenApiDocument { Info = new OpenApiInfo(), Paths = groupPaths }));
        }

        var baseRoots = new OpenApiDocument
        {
            Info = new OpenApiInfo(),
            Paths = new OpenApiPaths(),
            Webhooks = document.Webhooks,
            Components = document.Components is null
                ? null
                : new OpenApiComponents
                {
                    Parameters = document.Components.Parameters,
                    Responses = document.Components.Responses,
                    RequestBodies = document.Components.RequestBodies,
                    Headers = document.Components.Headers,
                    Examples = document.Components.Examples,
                    Links = document.Components.Links,
                    Callbacks = document.Components.Callbacks,
                    PathItems = document.Components.PathItems,
                    SecuritySchemes = document.Components.SecuritySchemes,
                    MediaTypes = document.Components.MediaTypes,
                },
        };
        MarkOwner(SplitBaseOwner, CollectSchemaReferences(baseRoots));

        return owners;

        void MarkOwner(
            string owner,
            IEnumerable<string> roots)
        {
            var pending = new Stack<string>(roots);
            while (pending.Count > 0)
            {
                var name = pending.Pop();
                if (!schemas.TryGetValue(name, out var schema))
                {
                    continue;
                }

                if (!owners.TryGetValue(name, out var schemaOwners))
                {
                    schemaOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    owners[name] = schemaOwners;
                }

                if (!schemaOwners.Add(owner))
                {
                    continue;
                }

                var single = new OpenApiDocument
                {
                    Info = new OpenApiInfo(),
                    Paths = new OpenApiPaths(),
                    Components = new OpenApiComponents
                    {
                        Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { [name] = schema },
                    },
                };

                foreach (var referenced in CollectSchemaReferences(single))
                {
                    pending.Push(referenced);
                }
            }
        }
    }

    private static HashSet<string> CollectSchemaReferences(
        OpenApiDocument document)
    {
        var collector = new SchemaReferenceCollector();
        new OpenApiWalker(collector).Walk(document);
        return collector.SchemaIds;
    }

    /// <summary>
    /// Serializes a part (or common) file: the version line, <c>paths</c> and <c>components</c> - no <c>info</c>,
    /// which the merge takes from the base file.
    /// </summary>
    private static string SerializePartDocument(
        OpenApiDocument source,
        OpenApiPaths paths,
        Dictionary<string, IOpenApiSchema> schemas,
        string header)
    {
        var part = new OpenApiDocument
        {
            Info = new OpenApiInfo(),
            Paths = paths,
        };

        if (source.Metadata is not null)
        {
            part.Metadata = new Dictionary<string, object>(source.Metadata, StringComparer.Ordinal);
        }

        if (schemas.Count > 0)
        {
            part.Components = new OpenApiComponents { Schemas = schemas };
        }

        var yaml = SerializeToYaml(part);
        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(header);
        var skippingInfo = false;
        foreach (var line in yaml.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("info:", StringComparison.Ordinal))
            {
                skippingInfo = true;
                continue;
            }

            if (skippingInfo && trimmed.StartsWith(" ", StringComparison.Ordinal))
            {
                continue;
            }

            skippingInfo = false;
            sb.Append(trimmed).Append('\n');
        }

        return sb.ToString().TrimEnd('\n') + "\n";
    }

    /// <summary>
    /// Collects the ids of the component schemas an element references.
    /// </summary>
    private sealed class SchemaReferenceCollector : OpenApiVisitorBase
    {
        public HashSet<string> SchemaIds { get; } = new(StringComparer.Ordinal);

        public override void Visit(IOpenApiReferenceHolder referenceHolder)
        {
            if (referenceHolder?.GetType().GetProperty("Reference")?.GetValue(referenceHolder) is BaseOpenApiReference
                {
                    Type: ReferenceType.Schema,
                    Id: { Length: > 0 } id,
                    IsExternal: false,
                })
            {
                SchemaIds.Add(id);
            }
        }
    }

    // The segment the generated namespaces are named after (an /api prefix, a version and a date skipped), so a
    // split part or an analysis group is the namespace its operations generate into
    private static string GetFirstPathSegment(string path)
        => PathSegmentHelper.GetFirstPathSegment(path);

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

    private static IEnumerable<string> GetUsedSchemas(
        OpenApiOperation operation)
    {
        var schemas = new HashSet<string>(StringComparer.Ordinal);

        // Check request body
        if (operation.RequestBody?.Content is not null)
        {
            foreach (var content in operation.RequestBody.Content.Values)
            {
                AddSchemaReferences(content.Schema, schemas);
            }
        }

        // Check responses
        if (operation.Responses is not null)
        {
            foreach (var response in operation.Responses.Values)
            {
                if (response.Content is not null)
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
        if (schema is null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference schemaRef &&
            !string.IsNullOrEmpty(schemaRef.Reference?.Id))
        {
            schemas.Add(schemaRef.Reference!.Id!);
        }

        if (schema is OpenApiSchema actualSchema)
        {
            // Check array items
            if (actualSchema.Items is not null)
            {
                AddSchemaReferences(actualSchema.Items, schemas);
            }

            // Check allOf/oneOf/anyOf
            if (actualSchema.AllOf is not null)
            {
                foreach (var item in actualSchema.AllOf)
                {
                    AddSchemaReferences(item, schemas);
                }
            }
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
        if (pathSegmentAnalysis.Count is > 1 and < 10)
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
        SplitStrategy strategy)
    {
        var schemaUsage = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        if (document.Paths is null)
        {
            return [];
        }

        foreach (var path in document.Paths)
        {
            if (path.Value?.Operations is null)
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

        return
        [
            .. schemaUsage
                .Where(kvp => kvp.Value.Count > 1)
                .Select(kvp => new SharedSchemaAnalysis(
                    name: kvp.Key,
                    usedByDomains:
                    [
                        .. kvp
                            .Value
                            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                    ]))
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
        ];
    }
}