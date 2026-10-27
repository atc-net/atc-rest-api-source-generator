// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI operations and converts them to ClassParameters for HTTP client class generation.
/// </summary>
public static partial class HttpClientExtractor
{
    /// <summary>
    /// The typed client's serializer defaults, as the members of a <c>JsonSerializerOptions</c> object initializer
    /// (one per line, each ending in a comma): the client's own default options and the generated
    /// <c>ApiClientJsonOptions</c> the DI registration starts from are built from the same list.
    /// </summary>
    public static IReadOnlyList<string> DefaultJsonSerializerOptionsInitializers(
        OpenApiDocument openApiDoc,
        string projectName)
        =>
        [
            "PropertyNamingPolicy = JsonNamingPolicy.CamelCase,",
            "PropertyNameCaseInsensitive = true,",
            "AllowOutOfOrderMetadataProperties = true,",
            .. EnumWireConverterExtractor.HasEnums(openApiDoc)
                ? new[] { $"Converters = {{ new global::{NamespaceBuilder.BuildBase(projectName)}.{EnumWireConverterExtractor.ClassName}() }}," }
                : [],
        ];

    /// <summary>
    /// Extracts HTTP client class from OpenAPI document paths and operations.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace and class name).</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <param name="useServersBasePath">Whether to prepend the base path from OpenAPI servers[0].url to URLs. Default: true.</param>
    /// <param name="resultStyle">Whether operations throw on a non-success status or return an EndpointResponse envelope.</param>
    /// <returns>ClassParameters for the HTTP client class, or null if no paths exist.</returns>
    public static ClassParameters? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false,
        bool useServersBasePath = true,
        TypedClientResultStyleType resultStyle = TypedClientResultStyleType.Throw)
        => Extract(openApiDoc, projectName, pathSegment: null, registry: registry, systemTypeResolver: systemTypeResolver, includeDeprecated: includeDeprecated, useServersBasePath: useServersBasePath, resultStyle: resultStyle);

    /// <summary>
    /// Extracts HTTP client class from OpenAPI document paths and operations filtered by path segment.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace and class name).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all operations.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <param name="useServersBasePath">Whether to prepend the base path from OpenAPI servers[0].url to URLs. Default: true.</param>
    /// <param name="resultStyle">Whether operations throw on a non-success status or return an EndpointResponse envelope.</param>
    /// <returns>ClassParameters for the HTTP client class, or null if no paths exist.</returns>
    public static ClassParameters? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false,
        bool useServersBasePath = true,
        TypedClientResultStyleType resultStyle = TypedClientResultStyleType.Throw)
        => ExtractInternal(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated, inlineSchemas: null, useServersBasePath: useServersBasePath, resultStyle: resultStyle);

    /// <summary>
    /// Extracts HTTP client class from OpenAPI document along with any inline schemas discovered.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace and class name).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all operations.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <param name="useServersBasePath">Whether to prepend the base path from OpenAPI servers[0].url to URLs. Default: true.</param>
    /// <param name="hasSegmentModels">Whether the segment has segment-specific models.</param>
    /// <param name="hasSharedModels">Whether there are shared models in the project.</param>
    /// <param name="namespaceSegment">The segment used for namespaces and the client class name. When null, <paramref name="pathSegment"/> is used. Pass an explicitly resolved value (possibly empty) to omit the segment.</param>
    /// <param name="clientSuffix">The client class name suffix. Defaults to "Client" when null or empty.</param>
    /// <param name="clientName">Explicit client type name, used verbatim when supplied.</param>
    /// <returns>A tuple containing the ClassParameters and a dictionary of discovered inline schemas.</returns>
    public static (ClassParameters? ClientClass, Dictionary<string, InlineSchemaInfo> InlineSchemas) ExtractWithInlineSchemas(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false,
        bool useServersBasePath = true,
        bool? hasSegmentModels = null,
        bool? hasSharedModels = null,
        string? namespaceSegment = null,
        string? clientSuffix = null,
        string? clientName = null,
        TypedClientResultStyleType resultStyle = TypedClientResultStyleType.Throw,
        bool preferXml = false)
    {
        var inlineSchemas = new Dictionary<string, InlineSchemaInfo>(StringComparer.Ordinal);
        var clientClass = ExtractInternal(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated, inlineSchemas, useServersBasePath, hasSegmentModels, hasSharedModels, namespaceSegment, clientSuffix, clientName, resultStyle, preferXml);
        return (clientClass, inlineSchemas);
    }

    /// <summary>
    /// Extracts client parameter classes from OpenAPI document operations filtered by path segment.
    /// Uses OperationParameterExtractor with binding attributes disabled.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing path and operation definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="pathSegment">The path segment to filter by (e.g., "Pets"). If null, extracts all operations.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>List of RecordParameters for parameter DTOs.</returns>
    public static List<RecordParameters>? ExtractParameters(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        ValidateSpecificationStrategy validateStrategy = ValidateSpecificationStrategy.Strict)
        => OperationParameterExtractor.ExtractIndividual(
            openApiDoc,
            projectName,
            pathSegment,
            registry: registry,
            includeBindingAttributes: false,
            includeDeprecated: includeDeprecated,
            validateStrategy: validateStrategy);

    /// <summary>
    /// Inline-enum-aware variant of <see cref="ExtractParameters"/>. Returns both the
    /// parameter records and any inline enums discovered on parameter schemas, so the
    /// Roslyn client generator can emit them as separate <c>.g.cs</c> files.
    /// </summary>
    public static (List<RecordParameters>? Records, List<InlineEnumInfo> InlineEnums) ExtractParametersWithInlineEnums(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry = null,
        bool includeDeprecated = false,
        ValidateSpecificationStrategy validateStrategy = ValidateSpecificationStrategy.Strict,
        string? namespaceSegment = null)
        => OperationParameterExtractor.ExtractIndividualWithInlineEnums(
            openApiDoc,
            projectName,
            pathSegment,
            registry,
            includeBindingAttributes: false,
            includeDeprecated: includeDeprecated,
            validateStrategy: validateStrategy,
            namespaceSegment: namespaceSegment);

    /// <summary>
    /// Builds a Uri.EscapeDataString expression for a parameter access expression.
    /// String types use Uri.EscapeDataString directly.
    /// Other types use .ToString() before encoding.
    /// Returns the original expression if encoding is not needed.
    /// </summary>
    /// <param name="accessExpression">The expression that reads the parameter value.</param>
    /// <param name="paramType">The parameter's C# type name.</param>
    /// <returns>The expression that yields the URL-safe text.</returns>
    public static string BuildEncodedExpression(
        string accessExpression,
        string paramType)
    {
        // Dates, times and decimal numbers: formatted for the wire, never with the machine's culture
        if (WireValueHelper.TryBuildWireExpression(accessExpression, paramType) is { } wireValue)
        {
            return $"Uri.EscapeDataString({wireValue} ?? string.Empty)";
        }

        if (!NeedsUrlEncoding(paramType))
        {
            return accessExpression;
        }

        var baseType = paramType.TrimEnd('?');
        if (baseType == "string")
        {
            return $"Uri.EscapeDataString({accessExpression})";
        }

        // Use string interpolation for non-string types to avoid nullable ToString() warnings
        return $"Uri.EscapeDataString($\"{{{accessExpression}}}\")";
    }

    /// <summary>
    /// Builds the string interpolation replacement for a path parameter.
    /// String types use Uri.EscapeDataString directly.
    /// Other types that need encoding use .ToString() + Uri.EscapeDataString.
    /// Value types known to produce URL-safe output are used directly.
    /// </summary>
    public static string BuildPathParameterReplacement(
        string propName,
        string paramType)
    {
        // Dates, times and decimal numbers: formatted for the wire, never with the machine's culture
        if (WireValueHelper.TryBuildWireExpression($"parameters.{propName}", paramType) is { } wireValue)
        {
            return $"{{Uri.EscapeDataString({wireValue} ?? string.Empty)}}";
        }

        if (!NeedsUrlEncoding(paramType))
        {
            return $"{{parameters.{propName}}}";
        }

        var baseType = paramType.TrimEnd('?');
        if (baseType == "string")
        {
            return $"{{Uri.EscapeDataString(parameters.{propName})}}";
        }

        // Use string interpolation for non-string types to avoid nullable ToString() warnings
        return $"{{Uri.EscapeDataString($\"{{parameters.{propName}}}\")}}";
    }

    /// <summary>
    /// Whether a value of the C# type has to be percent-encoded when it is written into a URL: a string (or a string-like
    /// value such as a Uri) can carry reserved characters; a number, a boolean, a GUID or a date cannot.
    /// </summary>
    /// <param name="csharpType">The C# type of the parameter, with or without <c>?</c>.</param>
    /// <returns><see langword="true"/> when the value must go through <c>Uri.EscapeDataString</c>.</returns>
    public static bool NeedsUrlEncoding(string csharpType)
    {
        // Remove nullable indicator for comparison
        var baseType = csharpType.TrimEnd('?');

        // Arrays need special handling (encode each element) - not supported yet
        if (baseType.EndsWith("[]", StringComparison.Ordinal))
        {
            return false;
        }

        // Types whose ToString() always produces URL-safe output (digits, hex, true/false)
        return baseType switch
        {
            "int" => false,
            "long" => false,
            "short" => false,
            "byte" => false,
            "bool" => false,
            "float" => false,
            "double" => false,
            "decimal" => false,
            "Guid" => false,

            // String and DateTimeOffset need encoding (DateTimeOffset contains '+' for timezone)
            "string" => true,
            "DateTimeOffset" => true,
            "DateTime" => true,
            "DateOnly" => true,
            "TimeOnly" => true,

            // Custom/enum types: encode defensively since ToString() may produce URL-unsafe characters
            _ => true,
        };
    }

    /// <summary>
    /// Whether the typed client hands an operation's download over as it arrives: a file download (not a model or a
    /// file) marked <c>x-return-stream</c>.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>True for a <c>StreamedFile</c>.</returns>
    public static bool IsStreamedFileDownload(OpenApiOperation operation)
        => operation is not null &&
           operation.IsStreamedDownloadOperation() &&
           operation.GetModelOrFileMediaTypes().Count == 0;

    /// <summary>
    /// The typed client class of a segment (or of the whole document): its serializer field, constructors, one method
    /// per operation, the helper methods and the usings the methods need - or <see langword="null"/> when the segment
    /// has no operation.
    /// </summary>
    private static ClassParameters? ExtractInternal(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated,
        Dictionary<string, InlineSchemaInfo>? inlineSchemas,
        bool useServersBasePath = true,
        bool? hasSegmentModels = null,
        bool? hasSharedModels = null,
        string? namespaceSegment = null,
        string? clientSuffix = null,
        string? clientName = null,
        TypedClientResultStyleType resultStyle = TypedClientResultStyleType.Throw,
        bool preferXml = false)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (openApiDoc.Paths is null || openApiDoc.Paths.Count == 0)
        {
            return null;
        }

        var effectiveNamespaceSegment = namespaceSegment ?? pathSegment;

        // An explicit client name is the author stating the full type name, so it wins outright.
        var className = !string.IsNullOrWhiteSpace(clientName)
            ? clientName!.Trim()
            : string.IsNullOrEmpty(effectiveNamespaceSegment)
                ? CasingHelper.BuildClientTypeName(projectName, clientSuffix)
                : CasingHelper.BuildClientTypeName(effectiveNamespaceSegment, clientSuffix);

        // An empty namespace segment means Single granularity: the client is flattened into
        // "{projectName}.Generated" rather than the per-area "{projectName}.Generated.Client".
        var namespaceValue = string.IsNullOrEmpty(effectiveNamespaceSegment)
            ? NamespaceBuilder.BuildBase(projectName)
            : NamespaceBuilder.ForClient(projectName, effectiveNamespaceSegment);
        var modelsNamespace = NamespaceBuilder.ForModels(projectName, effectiveNamespaceSegment);

        var methods = ExtractMethods(openApiDoc, projectName, pathSegment, registry, systemTypeResolver, includeDeprecated, inlineSchemas, useServersBasePath, resultStyle, preferXml);

        // Return null if no methods were extracted for this segment
        if (methods.Count == 0)
        {
            return null;
        }

        var contentForAnalysis = BuildContentForAnalysis(methods);
        AddHelperMethods(methods, openApiDoc, projectName, resultStyle);

        return new ClassParameters(
            HeaderContent: BuildHeaderContent(contentForAnalysis, projectName, pathSegment, modelsNamespace, hasSegmentModels, hasSharedModels, resultStyle),
            Namespace: namespaceValue,
            DocumentationTags: null,
            Attributes:
            [
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            ],
            DeclarationModifier: DeclarationModifiers.PublicSealedClass,
            ClassTypeName: className,
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: HttpClientInterfaceExtractor.BuildInterfaceName(className),
            Constructors: BuildConstructors(className),
            Properties: null,
            Methods: methods,
            GenerateToStringMethod: false,
            AdditionalFieldDeclarations: BuildJsonSerializerOptionsField(openApiDoc, projectName));
    }

    /// <summary>
    /// The client's default serializer options, as a static field (from <see cref="DefaultJsonSerializerOptionsInitializers"/>).
    /// </summary>
    private static List<string> BuildJsonSerializerOptionsField(
        OpenApiDocument openApiDoc,
        string projectName)
    {
        var additionalFieldDeclarations = new List<string>
        {
            "private static readonly JsonSerializerOptions defaultJsonSerializerOptions = new JsonSerializerOptions",
            "{",
        };
        additionalFieldDeclarations.AddRange(DefaultJsonSerializerOptionsInitializers(openApiDoc, projectName).Select(member => "    " + member));
        additionalFieldDeclarations.Add("};");

        return additionalFieldDeclarations;
    }

    /// <summary>
    /// The client's constructors: from an <c>HttpClient</c> (with the default serializer options), and from an
    /// <c>HttpClient</c> and the caller's <c>JsonSerializerOptions</c>.
    /// </summary>
    private static List<ConstructorParameters> BuildConstructors(
        string className)
    {
        var constructor1Params = new List<ConstructorParameterBaseParameters>
        {
            new(
                GenericTypeName: null,
                TypeName: "HttpClient",
                IsNullableType: false,
                Name: "httpClient",
                DefaultValue: null,
                PassToInheritedClass: false,
                CreateAsPrivateReadonlyMember: true,
                CreateAaOneLiner: false),
        };

        var constructor1 = new ConstructorParameters(
            DocumentationTags: null,
            DeclarationModifier: DeclarationModifiers.Public,
            GenericTypeName: null,
            TypeName: className,
            InheritedClassTypeName: null,
            Parameters: constructor1Params,
            AdditionalStatements: ["this.jsonSerializerOptions = defaultJsonSerializerOptions;"]);

        var constructor2Params = new List<ConstructorParameterBaseParameters>
        {
            new(
                GenericTypeName: null,
                TypeName: "HttpClient",
                IsNullableType: false,
                Name: "httpClient",
                DefaultValue: null,
                PassToInheritedClass: false,
                CreateAsPrivateReadonlyMember: false,
                CreateAaOneLiner: false),
            new(
                GenericTypeName: null,
                TypeName: "JsonSerializerOptions",
                IsNullableType: false,
                Name: "jsonSerializerOptions",
                DefaultValue: null,
                PassToInheritedClass: false,
                CreateAsPrivateReadonlyMember: true,
                CreateAaOneLiner: false),
        };

        var constructor2 = new ConstructorParameters(
            DocumentationTags: null,
            DeclarationModifier: DeclarationModifiers.Public,
            GenericTypeName: null,
            TypeName: className,
            InheritedClassTypeName: null,
            Parameters: constructor2Params);

        return [constructor1, constructor2];
    }

    /// <summary>
    /// One method per operation of the segment (every operation without one), deprecated ones only when asked for.
    /// </summary>
    private static List<MethodParameters> ExtractMethods(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated,
        Dictionary<string, InlineSchemaInfo>? inlineSchemas,
        bool useServersBasePath,
        TypedClientResultStyleType resultStyle,
        bool preferXml)
    {
        var methods = new List<MethodParameters>();

        foreach (var path in openApiDoc.Paths!)
        {
            var pathKey = path.Key;
            var pathItemInterface = path.Value;

            // Apply path segment filter if provided
            if (!string.IsNullOrEmpty(pathSegment))
            {
                var currentSegment = PathSegmentHelper.GetFirstPathSegment(pathKey);
                if (!currentSegment.Equals(pathSegment, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            if (pathItemInterface is not IOpenApiPathItem pathItem)
            {
                continue;
            }

            if (pathItem.Operations is not null)
            {
                // Get path-level parameters (defined on the path, not the operation)
                var pathLevelParameters = pathItem.Parameters;

                foreach (var operation in pathItem.Operations)
                {
                    // Skip deprecated operations if not including them
                    if (!includeDeprecated && operation.Value?.Deprecated == true)
                    {
                        continue;
                    }

                    var httpMethod = operation
                        .Key
                        .ToString()
                        .ToUpperInvariant();

                    var currentPathSegment = PathSegmentHelper.GetFirstPathSegment(pathKey);
                    var methodParams = ExtractMethod(pathKey, httpMethod, operation.Value, pathLevelParameters, openApiDoc, projectName, registry, systemTypeResolver, currentPathSegment, inlineSchemas, useServersBasePath, resultStyle, preferXml);

                    if (methodParams is not null)
                    {
                        methods.Add(methodParams);
                    }
                }
            }
        }

        return methods;
    }

    /// <summary>
    /// The text of the methods' signatures and bodies, which the usings are chosen from.
    /// </summary>
    private static string BuildContentForAnalysis(
        List<MethodParameters> methods)
    {
        var contentPreview = new StringBuilder();
        foreach (var method in methods)
        {
            contentPreview.AppendLine(method.ReturnTypeName);
            contentPreview.AppendLine(method.ReturnGenericTypeName);
            contentPreview.AppendLine(method.Content);
            if (method.Parameters is not null)
            {
                foreach (var param in method.Parameters)
                {
                    contentPreview.AppendLine(param.TypeName);
                    if (param.Attributes is not null)
                    {
                        foreach (var attr in param.Attributes)
                        {
                            contentPreview.AppendLine($"[{attr.Name}]");
                        }
                    }
                }
            }
        }

        contentPreview.AppendLine("JsonSerializerOptions");
        contentPreview.AppendLine("JsonStringEnumConverter");

        return contentPreview.ToString();
    }

    /// <summary>
    /// The helper methods the operation methods call: EnsureSuccessAsync, and with the Result style the envelope
    /// builders (the binary one only with a file download, the streamed-file one only with a streamed download).
    /// </summary>
    private static void AddHelperMethods(
        List<MethodParameters> methods,
        OpenApiDocument openApiDoc,
        string projectName,
        TypedClientResultStyleType resultStyle)
    {
        // Add the EnsureSuccessAsync helper method that reads error body before throwing
        methods.Add(CreateEnsureSuccessMethod($"global::{NamespaceBuilder.BuildBase(projectName)}.{ApiExceptionExtractor.ClassNameFor(openApiDoc.Components?.Schemas?.Keys)}"));

        // Result style replaces the throw-on-status behaviour with envelope builders.
        if (resultStyle == TypedClientResultStyleType.Result)
        {
            // The binary builder only when an operation downloads a file (it refers to DownloadedFile)
            var hasFileDownload = methods.Any(m =>
                (m.ReturnTypeName?.Contains("DownloadedFile") ?? false) ||
                (m.ReturnGenericTypeName?.Contains("DownloadedFile") ?? false));
            methods.AddRange(CreateResultStyleHelperMethods(hasFileDownload));

            // The streamed-file builder only when an operation streams a download (it refers to StreamedFile)
            if (methods.Any(m => (m.ReturnTypeName?.Contains("StreamedFile") ?? false) || (m.ReturnGenericTypeName?.Contains("StreamedFile") ?? false)))
            {
                methods.Add(CreateBuildStreamedFileResponseMethod());
            }
        }
    }

    /// <summary>
    /// The client file's header: the usings its methods need, the models' namespaces, and the helpers' namespaces
    /// (streaming, resilience) when a method uses them.
    /// </summary>
    private static string BuildHeaderContent(
        string contentForAnalysis,
        string projectName,
        string? pathSegment,
        string modelsNamespace,
        bool? hasSegmentModels,
        bool? hasSharedModels,
        TypedClientResultStyleType resultStyle)
    {
        // Build header content with only required usings
        var usings = UsingStatementHelper.GetRequiredUsings(
            contentForAnalysis,
            NamespaceConstants.SystemCodeDomCompiler);

        // Result style returns EndpointResponse/EndpointResponse<T>/StreamingEndpointResponse<T>,
        // which all live in Atc.Rest.Client.
        if (resultStyle == TypedClientResultStyleType.Result)
        {
            usings.Add(NamespaceConstants.AtcRestClient);
        }

        // Models usings. When the caller knows which models exist (Roslyn per-segment client),
        // reference the shared and/or segment namespaces precisely — emitting a using for a
        // namespace that has no types (e.g. a segment that only references shared models) would
        // not compile. The legacy/CodeGenerationService callers pass null and keep the single
        // resolved namespace as before.
        if (!string.IsNullOrEmpty(pathSegment) && (hasSegmentModels.HasValue || hasSharedModels.HasValue))
        {
            if (hasSegmentModels == true)
            {
                usings.Add(modelsNamespace);
            }

            if (hasSharedModels == true)
            {
                usings.Add(NamespaceBuilder.ForModels(projectName));
            }
        }
        else
        {
            usings.Add(modelsNamespace);
        }

        // Reference the emitted StreamReaders helper namespace when this segment's methods use it
        // (Server-Sent Events reads). NOTE: a typed bool ("does any operation use StreamReaders")
        // would be cleaner than this content scan, but it must be computed PER SEGMENT — the
        // doc-wide StreamReadersExtractor.DocumentRequiresStreamReaders would over-inject the using
        // into a sibling segment client that has no SSE op. Until a per-segment framing signal is
        // threaded through, the content scan keeps the using precisely scoped to where it is used.
        if (contentForAnalysis.IndexOf("StreamReaders", StringComparison.Ordinal) >= 0 ||
            contentForAnalysis.IndexOf(StreamedRequestContentExtractor.TypeName, StringComparison.Ordinal) >= 0)
        {
            usings.Add($"{projectName}.Generated.Streaming");
        }

        // Operations with their own retry settings (x-retry-*) tell the generated resilience handler.
        if (contentForAnalysis.IndexOf("ApiResilienceRequest.", StringComparison.Ordinal) >= 0)
        {
            usings.Add($"{projectName}.Generated.Resilience");
        }

        // multipart/mixed extracts the response Content-Type boundary via LINQ (.FirstOrDefault) —
        // ensure System.Linq is imported (the content-scan type map doesn't cover LINQ calls).
        if (contentForAnalysis.IndexOf(".FirstOrDefault(", StringComparison.Ordinal) >= 0)
        {
            usings.Add(NamespaceConstants.SystemLinq);
        }

        var headerBuilder = new StringBuilder();
        headerBuilder.AppendLine("// <auto-generated />");
        headerBuilder.AppendLine("#nullable enable");
        headerBuilder.AppendLine();
        UsingStatementHelper.AppendUsings(headerBuilder, usings);
        headerBuilder.AppendLine();

        return headerBuilder.ToString();
    }

    private static MethodParameters? ExtractMethod(
        string path,
        string httpMethod,
        OpenApiOperation? operation,
        IList<IOpenApiParameter>? pathLevelParameters,
        OpenApiDocument openApiDoc,
        string projectName,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        string pathSegment,
        Dictionary<string, InlineSchemaInfo>? inlineSchemas,
        bool useServersBasePath = true,
        TypedClientResultStyleType resultStyle = TypedClientResultStyleType.Throw,
        bool preferXml = false)
    {
        if (operation is null)
        {
            return null;
        }

        // Check if this is an async enumerable streaming operation (x-* annotation or 3.2 itemSchema)
        var isAsyncEnumerable = operation.IsStreamingResponse();
        var operationId = OperationIdHelper.Effective(operation, httpMethod, path);
        var methodName = operationId.ToPascalCaseForDotNet() + "Async";
        var parametersClassName = $"{operationId.ToPascalCaseForDotNet()}Parameters";

        var response = SuccessResponseOf(operation);
        var (returnType, streamingItemType, xmlRead, hasLocationHeader, isTextResponse) = ResolveReturnType(operation, response, openApiDoc, registry, operationId, pathSegment, inlineSchemas, isAsyncEnumerable, resultStyle, preferXml);

        // Check if operation has parameters or request body (including path-level parameters)
        var hasQueryRouteParams = operation.Parameters is { Count: > 0 } || pathLevelParameters is { Count: > 0 };
        var hasRequestBody = operation.RequestBody is { Content: not null };
        var hasParameters = hasQueryRouteParams || hasRequestBody;

        // A method that returns IAsyncEnumerable<T> (a stream with an item type): its cancellation token gets
        // [EnumeratorCancellation]. Under Result style the method returns Task<StreamingEndpointResponse<T>> instead.
        var returnsAsyncEnumerable = isAsyncEnumerable && streamingItemType is not null && resultStyle != TypedClientResultStyleType.Result;
        var parameters = BuildMethodParameters(hasParameters, parametersClassName, enumeratorCancellation: returnsAsyncEnumerable);

        // Generate method body content
        var hasReturnType = returnType != nameof(Task);
        var methodContent = GenerateMethodBody(path, httpMethod, operation, pathLevelParameters, openApiDoc, projectName, returnType, hasParameters, isAsyncEnumerable, streamingItemType, hasReturnType, hasLocationHeader, useServersBasePath, parametersClassName, resultStyle, xmlRead, isTextResponse);

        // For async enumerable methods, return IAsyncEnumerable<T> directly.
        // Under Result style the operation instead returns Task<StreamingEndpointResponse<T>>, which
        // resolves the status envelope once the response headers arrive and exposes the lazily
        // consumed IAsyncEnumerable<T> as its content - matching EndpointPerOperation mode.
        if (returnsAsyncEnumerable)
        {
            return new MethodParameters(
                DocumentationTags: null,
                Attributes: ObsoleteAttributeHelper.For(operation.Deprecated),
                DeclarationModifier: DeclarationModifiers.PublicAsync,
                ReturnGenericTypeName: "IAsyncEnumerable",
                ReturnTypeName: streamingItemType,
                Name: methodName,
                Parameters: parameters,
                AlwaysBreakDownParameters: false,
                UseExpressionBody: false,
                Content: methodContent);
        }

        var (returnGenericTypeName, returnTypeName) = TaskReturnTypeOf(returnType, streamingItemType, isAsyncEnumerable, resultStyle, systemTypeResolver);

        return new MethodParameters(
            DocumentationTags: null,
            Attributes: ObsoleteAttributeHelper.For(operation.Deprecated),
            DeclarationModifier: DeclarationModifiers.PublicAsync,
            ReturnGenericTypeName: returnGenericTypeName,
            ReturnTypeName: returnTypeName,
            Name: methodName,
            Parameters: parameters,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: methodContent);
    }

    /// <summary>
    /// The success response of an operation: 200, else 201, else the operation's other 2xx (202, 203, 206, ... - an
    /// operation declares one, ATC_API_OPR018) - or <see langword="null"/> without one.
    /// </summary>
    private static IOpenApiResponse? SuccessResponseOf(
        OpenApiOperation operation)
    {
        IOpenApiResponse? response = null;
        if (operation.Responses is not null)
        {
            var successStatus = operation.Responses.ContainsKey("200")
                ? "200"
                : operation.Responses.ContainsKey("201")
                    ? "201"
                    : operation.Responses.Keys
                        .Where(k => k.Length == 3 && k[0] == '2')
                        .OrderBy(k => k, StringComparer.Ordinal)
                        .FirstOrDefault();
            if (successStatus is not null)
            {
                response = operation.Responses[successStatus];
            }
        }

        return response;
    }

    /// <summary>
    /// What an operation's method returns and how it reads it: the return type (Task without a body), the item type
    /// of a stream, the XML read (and request) plan, whether the answer is the Location header, and whether it is text.
    /// </summary>
    private static (string ReturnType, string? StreamingItemType, XmlReadPlan? XmlRead, bool HasLocationHeader, bool IsTextResponse) ResolveReturnType(
        OpenApiOperation operation,
        IOpenApiResponse? response,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string operationId,
        string pathSegment,
        Dictionary<string, InlineSchemaInfo>? inlineSchemas,
        bool isAsyncEnumerable,
        TypedClientResultStyleType resultStyle,
        bool preferXml)
    {
        var returnType = nameof(Task);
        string? streamingItemType = null;
        XmlReadPlan? xmlRead = null;
        var hasLocationHeader = false;
        var isTextResponse = false;

        // Check for JSON content first
        if (response?.Content is not null && response.Content.JsonBody is { } mediaType1)
        {
            var contentType = GetSchemaTypeName(mediaType1.Schema, openApiDoc, registry, operationId, pathSegment, "Response", inlineSchemas);
            if (!string.IsNullOrEmpty(contentType))
            {
                returnType = contentType;

                // A stream's element: the items of an array response, else the response itself - the server
                // streams a sequence of what the schema describes either way
                if (isAsyncEnumerable)
                {
                    streamingItemType = TryGetListElementType(contentType, out var itemType)
                        ? itemType
                        : contentType;
                }

                // Offered as XML too: ask for JSON first (preferXml: XML), read whatever Content-Type comes back
                if (!isAsyncEnumerable && response.Content.XmlAlternative is { } alternative)
                {
                    xmlRead = new XmlReadPlan(XmlBodyHelper.ReadMethod(contentType, alternative), ByContentType: true, XmlBodyHelper.AcceptFor([alternative], XmlBodyHelper.ErrorMediaTypes(operation), preferXml)!);
                }
                else if (!isAsyncEnumerable && response.Content.Keys.Any(key => ResponseBodyClassifier.Classify(key, response.Content[key]?.Schema) != ResponseBodyKind.Json))
                {
                    // Offered as text or XML too, which this method does not read: it asks for JSON (a server that
                    // negotiates would else answer with the first media type it declares)
                    xmlRead = new XmlReadPlan(ReadMethod: null, ByContentType: false, string.Join(", ", new[] { "application/json" }.Concat(XmlBodyHelper.ErrorMediaTypes(operation)).Append("application/problem+json").Distinct(StringComparer.OrdinalIgnoreCase)));
                }
            }
        }
        else if (response?.Content is not null &&
                 response.Content.XmlOnlyBody is { } xmlBody &&
                 GetSchemaTypeName(response.Content[xmlBody.MediaType].Schema, openApiDoc, registry, operationId, pathSegment, "Response", inlineSchemas) is { Length: > 0 } xmlType)
        {
            // An XML-only model body: read by the model's generated XML mapping
            returnType = xmlType;
            xmlRead = new XmlReadPlan(XmlBodyHelper.ReadMethod(xmlType, xmlBody), ByContentType: false, XmlBodyHelper.AcceptFor([xmlBody], XmlBodyHelper.ErrorMediaTypes(operation), preferXml)!);
        }
        else if (response?.Content is not null && IsBinaryResponseContent(response.Content))
        {
            // A file download (the primary body is binary): the bytes, content type and file name as a DownloadedFile -
            // or, marked x-return-stream, the open content as a StreamedFile the caller disposes
            returnType = IsStreamedFileDownload(operation) ? "StreamedFile" : "DownloadedFile";
        }
        else if (response?.Content is not null && IsTextResponseContent(response.Content))
        {
            // Plain text content returns string, read as text on every verb
            returnType = "string";
            isTextResponse = true;
        }
        else if (response is { } openApiResp &&
                 openApiResp.Headers is not null &&
                 openApiResp.Headers.TryGetValue("Location", out var locationHeader) &&
                 locationHeader.Schema is OpenApiSchema { Format: "uri" })
        {
            returnType = "Uri";
            hasLocationHeader = true;
        }

        // A model or a file (a 200 that declares both): a ContentOrFile<T>, told apart by the answer's Content-Type -
        // the method asks for the model's and the file's media types
        if (!isAsyncEnumerable &&
            returnType != nameof(Task) &&
            returnType is not ("DownloadedFile" or "StreamedFile") &&
            operation.GetModelOrFileMediaTypes() is { Count: > 0 } fileMediaTypes)
        {
            var xmlBodies = xmlRead?.ReadMethod is null
                ? []
                : new[] { xmlRead.ByContentType ? response!.Content.XmlAlternative : response!.Content.XmlOnlyBody }.Where(b => b is not null).Select(b => b!).ToList();
            xmlRead = (xmlRead ?? new XmlReadPlan(ReadMethod: null, ByContentType: false, Accept: null)) with
            {
                Accept = XmlBodyHelper.AcceptFor(xmlBodies, XmlBodyHelper.ErrorMediaTypes(operation), preferXml, fileMediaTypes),
                ModelOrFileType = returnType,
            };
            returnType = $"ContentOrFile<{returnType}>";
        }

        // A success without a body beside the model (a 204 next to the 200 - Standard mode; Strict reports
        // ATC_API_OPR018): the Throw style returns null for it, so the model is nullable
        if (resultStyle == TypedClientResultStyleType.Throw &&
            !isAsyncEnumerable &&
            response is not null &&
            returnType is not (nameof(Task) or "DownloadedFile" or "StreamedFile" or "Uri" or "string") &&
            !returnType.StartsWith("ContentOrFile<", StringComparison.Ordinal) &&
            !returnType.EndsWith("?", StringComparison.Ordinal) &&
            operation.Responses!.Any(r => r.Key.Length == 3 && r.Key[0] == '2' && r.Value != response && r.Value?.Content is not { Count: > 0 }))
        {
            returnType += "?";
        }

        // OpenAPI 3.2 streaming: when the response declares an itemSchema, the element
        // type comes from it directly (not from an application/json array body).
        if (isAsyncEnumerable && streamingItemType is null)
        {
            var streamingItemSchema = operation.GetStreamingItemSchema();
            if (streamingItemSchema is not null)
            {
                streamingItemType = GetSchemaTypeName(streamingItemSchema, openApiDoc, registry, operationId, pathSegment, "Response", inlineSchemas);
            }
        }

        // An XML request body (XML-only, or with preferXml the XML alternative): sent as the model's XML
        if (XmlBodyHelper.ClientRequestBody(operation, preferXml) is { } xmlRequestBody)
        {
            var bytes = XmlBodyHelper.ToUtf8BytesExpression(XmlBodyHelper.ModelTypeName(operation.RequestBody!.Content!, xmlRequestBody), xmlRequestBody, "parameters.Request");
            xmlRead = (xmlRead ?? new XmlReadPlan(ReadMethod: null, ByContentType: false, Accept: null)) with
            {
                RequestContent = $"XmlModelSupport.ToContent({bytes}, \"{xmlRequestBody.MediaType}\")",
            };
        }

        return (returnType, streamingItemType, xmlRead, hasLocationHeader, isTextResponse);
    }

    /// <summary>
    /// The method's parameters: the operation's parameters object (when it has parameters or a body), and the
    /// cancellation token - with [EnumeratorCancellation] for a method that returns IAsyncEnumerable.
    /// </summary>
    private static List<ParameterBaseParameters> BuildMethodParameters(
        bool hasParameters,
        string parametersClassName,
        bool enumeratorCancellation)
    {
        var parameters = new List<ParameterBaseParameters>();

        // If operation has parameters, use a parameters object
        if (hasParameters)
        {
            parameters.Add(new ParameterBaseParameters(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: parametersClassName,
                IsNullableType: false,
                IsReferenceType: true,
                Name: "parameters",
                DefaultValue: null));
        }

        var cancellationTokenAttrs = enumeratorCancellation
            ? new List<AttributeParameters> { new("EnumeratorCancellation", null) }
            : null;

        parameters.Add(new ParameterBaseParameters(
            Attributes: cancellationTokenAttrs,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "CancellationToken",
            IsNullableType: false,
            IsReferenceType: false,
            Name: "cancellationToken",
            DefaultValue: "default"));

        return parameters;
    }

    /// <summary>
    /// The return type of a method that returns a Task: with the Result style a Task of its envelope, else a Task of
    /// the return type (a plain Task without a body).
    /// </summary>
    private static (string? ReturnGenericTypeName, string ReturnTypeName) TaskReturnTypeOf(
        string returnType,
        string? streamingItemType,
        bool isAsyncEnumerable,
        TypedClientResultStyleType resultStyle,
        SystemTypeConflictResolver systemTypeResolver)
    {
        var taskTypeName = systemTypeResolver.EnsureFullNamespaceIfNeeded(nameof(Task));

        string? returnGenericTypeName = null;
        string returnTypeName;

        if (resultStyle == TypedClientResultStyleType.Result)
        {
            // Result style always yields a Task<TEnvelope>, so callers can inspect the status
            // without exception handling. Transport-level failures still throw.
            returnGenericTypeName = taskTypeName;
            returnTypeName = isAsyncEnumerable && streamingItemType is not null
                ? $"StreamingEndpointResponse<{streamingItemType}>"
                : returnType == nameof(Task)
                    ? "EndpointResponse"
                    : $"EndpointResponse<{returnType}>";
        }
        else if (returnType == nameof(Task))
        {
            returnTypeName = taskTypeName;
        }
        else
        {
            returnGenericTypeName = taskTypeName;
            returnTypeName = returnType;
        }

        return (returnGenericTypeName, returnTypeName);
    }
}