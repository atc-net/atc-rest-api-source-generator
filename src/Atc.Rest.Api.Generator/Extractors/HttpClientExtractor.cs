// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI operations and converts them to ClassParameters for HTTP client class generation.
/// </summary>
public static class HttpClientExtractor
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
    public static (ClassParameters? ClientClass, Dictionary<string, HttpClientInlineSchemaInfo> InlineSchemas) ExtractWithInlineSchemas(
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
        var inlineSchemas = new Dictionary<string, HttpClientInlineSchemaInfo>(StringComparer.Ordinal);
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
    /// Determines if a type name is a pagination base type.
    /// Supports common naming conventions: PaginationResult, PaginatedResult, PagedResult.
    /// </summary>
    private static ClassParameters? ExtractInternal(
        OpenApiDocument openApiDoc,
        string projectName,
        string? pathSegment,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated,
        Dictionary<string, HttpClientInlineSchemaInfo>? inlineSchemas,
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

        var additionalFieldDeclarations = new List<string>
        {
            "private static readonly JsonSerializerOptions defaultJsonSerializerOptions = new JsonSerializerOptions",
            "{",
        };
        additionalFieldDeclarations.AddRange(DefaultJsonSerializerOptionsInitializers(openApiDoc, projectName).Select(member => "    " + member));
        additionalFieldDeclarations.Add("};");

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

        var methods = new List<MethodParameters>();

        foreach (var path in openApiDoc.Paths)
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
                    var methodParams = ExtractMethod(pathKey, httpMethod, operation.Value, pathLevelParameters, openApiDoc, registry, systemTypeResolver, currentPathSegment, inlineSchemas, useServersBasePath, resultStyle, preferXml);

                    if (methodParams is not null)
                    {
                        methods.Add(methodParams);
                    }
                }
            }
        }

        // Return null if no methods were extracted for this segment
        if (methods.Count == 0)
        {
            return null;
        }

        // Build content preview to analyze for required usings
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

        var contentForAnalysis = contentPreview.ToString();

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

        return new ClassParameters(
            HeaderContent: headerBuilder.ToString(),
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
            Constructors: [constructor1, constructor2],
            Properties: null,
            Methods: methods,
            GenerateToStringMethod: false,
            AdditionalFieldDeclarations: additionalFieldDeclarations);
    }

    private static MethodParameters? ExtractMethod(
        string path,
        string httpMethod,
        OpenApiOperation? operation,
        IList<IOpenApiParameter>? pathLevelParameters,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        SystemTypeConflictResolver systemTypeResolver,
        string pathSegment,
        Dictionary<string, HttpClientInlineSchemaInfo>? inlineSchemas,
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

        // Determine return type - check both 200 and 201 responses
        var returnType = nameof(Task);
        string? streamingItemType = null;
        XmlReadPlan? xmlRead = null;
        var hasLocationHeader = false;
        var isTextResponse = false;

        // The success response: 200, else 201, else the operation's other 2xx (202, 203, 206, ... - an operation
        // declares one, ATC_API_OPR018).
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

        // Check if operation has parameters or request body (including path-level parameters)
        var hasQueryRouteParams = operation.Parameters is { Count: > 0 } || pathLevelParameters is { Count: > 0 };
        var hasRequestBody = operation.RequestBody is { Content: not null };
        var hasParameters = hasQueryRouteParams || hasRequestBody;

        // Build method parameters
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

        // Add [EnumeratorCancellation] attribute only for methods that actually return IAsyncEnumerable<T>
        // (requires both the x-return-async-enumerable extension AND a streaming item type from an array response).
        // Under Result style the method returns Task<StreamingEndpointResponse<T>> instead, so the attribute
        // would be invalid there.
        var willReturnAsyncEnumerable = isAsyncEnumerable &&
                                        streamingItemType is not null &&
                                        resultStyle != TypedClientResultStyleType.Result;
        var cancellationTokenAttrs = willReturnAsyncEnumerable
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

        // Generate method body content
        var hasReturnType = returnType != nameof(Task);
        var methodContent = GenerateMethodBody(path, httpMethod, operation, pathLevelParameters, openApiDoc, returnType, hasParameters, isAsyncEnumerable, streamingItemType, hasReturnType, hasLocationHeader, useServersBasePath, parametersClassName, resultStyle, xmlRead, isTextResponse);

        // For async enumerable methods, return IAsyncEnumerable<T> directly.
        // Under Result style the operation instead returns Task<StreamingEndpointResponse<T>>, which
        // resolves the status envelope once the response headers arrive and exposes the lazily
        // consumed IAsyncEnumerable<T> as its content - matching EndpointPerOperation mode.
        if (isAsyncEnumerable && streamingItemType is not null && resultStyle != TypedClientResultStyleType.Result)
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

    private static string GenerateMethodBody(
        string path,
        string httpMethod,
        OpenApiOperation operation,
        IList<IOpenApiParameter>? pathLevelParameters,
        OpenApiDocument openApiDoc,
        string returnType,
        bool hasParameters,
        bool isAsyncEnumerable,
        string? streamingItemType,
        bool hasReturnType,
        bool hasLocationHeader,
        bool useServersBasePath = true,
        string parametersClassName = "",
        TypedClientResultStyleType resultStyle = TypedClientResultStyleType.Throw,
        XmlReadPlan? xmlRead = null,
        bool isTextResponse = false)
    {
        var builder = new StringBuilder();
        var propertyNames = ParameterPropertyNames.For(operation, pathLevelParameters);

        // Get server base path if enabled (e.g., "/api/v1" from servers[0].url)
        var serverBasePath = useServersBasePath ? ServerUrlHelper.GetServersBasePath(openApiDoc) : null;

        // Build the URL - optionally prepend server base path, then replace path parameters. The URL is relative
        // (no leading slash), so the HttpClient's BaseAddress keeps its path: https://gw/svc-a/ + prices is
        // /svc-a/prices; a leading slash would replace that path
        var urlBuilder = (serverBasePath is not null ? $"{serverBasePath}{path}" : path).TrimStart('/');

        // The parameters the operation is called with: its path item's, except one it redeclares, then its own
        var effectiveParameters = operation.EffectiveParameters(pathLevelParameters);

        // The path parameters into the URL
        foreach (var paramInterface in effectiveParameters)
            {
                var resolved = paramInterface.Resolve();
                var (param, referenceId) = (resolved.Parameter, resolved.ReferenceId);
                if (param is null || string.IsNullOrEmpty(param.Name))
                {
                    continue;
                }

                if (param.In == ParameterLocation.Path)
                {
                    var propName = propertyNames.Get(param, referenceId);
                    var paramType = GetParameterTypeWithInlineEnumAwareness(param, openApiDoc, parametersClassName, propName);
                    var replacement = param.GetArrayDelimiter() is { } delimiter
                        ? $"{{{JoinedItemsExpression(param, $"parameters.{propName}", paramType, delimiter, escapeItems: true)}}}"
                        : WireValueHelper.IsStringEnum(param.Schema)
                            ? $"{{Uri.EscapeDataString(parameters.{propName}.ToWireValue())}}"
                            : WireValueHelper.IsIntegerEnum(param.Schema)
                                ? $"{{{WireValueHelper.IntegerEnumWireExpression($"parameters.{propName}")}}}"
                                : BuildPathParameterReplacement(propName, paramType);
                    urlBuilder = urlBuilder.Replace($"{{{param.Name}}}", replacement);
                }
            }

        builder.AppendLine(
            urlBuilder.Contains('{')
                ? $"var url = $\"{urlBuilder}\";"
                : $"var url = \"{urlBuilder}\";");

        // Add query parameters - resolve parameter references first
        var queryParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        var headerParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        var cookieParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        var querystringParams = new List<(OpenApiParameter Param, string? ReferenceId)>();
        foreach (var paramInterface in effectiveParameters)
        {
            var resolved = paramInterface.Resolve();
            var (param, referenceId) = (resolved.Parameter, resolved.ReferenceId);

            // A value the client cannot send is left out (ATC_API_OPR037 warned)
            if (param?.IsUnbindable() == true)
            {
                continue;
            }

            switch (param)
            {
                case { In: ParameterLocation.Query }:
                    queryParams.Add((param, referenceId));
                    break;
                case { In: ParameterLocation.Header }:
                    headerParams.Add((param, referenceId));
                    break;
                case { In: ParameterLocation.Cookie }:
                    cookieParams.Add((param, referenceId));
                    break;
                case { In: ParameterLocation.QueryString }:
                    querystringParams.Add((param, referenceId));
                    break;
            }
        }

        if (queryParams.Count > 0)
        {
            builder.AppendLine("var queryParams = new List<string>();");
            foreach (var (param, referenceId) in queryParams)
            {
                var propName = propertyNames.Get(param, referenceId);
                var paramAccess = $"parameters.{propName}";
                var paramType = GetParameterTypeWithInlineEnumAwareness(param, openApiDoc, parametersClassName, propName);
                var isRequired = param.Required;
                var serialization = param.GetParameterSerialization();

                // An object: each property set by its own key - filter[name] (deepObject) or name (form, explode)
                if (param.GetQueryObject() is { } queryObject)
                {
                    AppendQueryObject(builder, queryObject, paramAccess);
                    continue;
                }

                // An array whose items are one value (form with explode: false, pipeDelimited, spaceDelimited): the
                // items joined on the declared delimiter, each as the specification writes it
                if (serialization.ValueKind == ParameterValueKind.Array &&
                    param.GetArrayDelimiter() is { } delimiter)
                {
                    var joined = JoinedItemsExpression(param, paramAccess, paramType, delimiter, escapeItems: false);
                    var value = serialization.AllowReserved ? joined : $"Uri.EscapeDataString({joined})";

                    builder.AppendLine();
                    builder.AppendLine($"if ({paramAccess} is not null && global::System.Linq.Enumerable.Any({paramAccess}))");
                    builder.AppendLine("{");
                    builder.AppendLine(4, $"queryParams.Add($\"{QueryKey(param.Name!)}={{{value}}}\");");
                    builder.AppendLine("}");
                    continue;
                }

                // An array: repeated keys (form, explode: true; pipeDelimited and spaceDelimited with explode: true are
                // the same) - never as the list's ToString()
                if (serialization.ValueKind == ParameterValueKind.Array)
                {
                    AppendQueryArrayForeach(builder, param.Name!, paramAccess, paramType, serialization.AllowReserved, isRequired, WireValueHelper.IsStringEnum(param.Schema), WireValueHelper.IsIntegerEnum(param.Schema.Dereferenced?.Items));
                    continue;
                }

                // allowReserved primitive: the reserved characters as they are, everything else (and #) encoded
                if (serialization.AllowReserved && serialization.ValueKind == ParameterValueKind.Primitive)
                {
                    var reservedValue = paramType.TrimEnd('?') == "string"
                        ? WireValueHelper.AllowReservedEscapeExpression(paramAccess + "!")
                        : BuildEncodedExpression(paramAccess, paramType);
                    if (isRequired)
                    {
                        builder.AppendLine($"queryParams.Add($\"{QueryKey(param.Name!)}={{{reservedValue}}}\");");
                    }
                    else
                    {
                        var nullCheck = BuildNullCheck(paramAccess, paramType);

                        builder.AppendLine();
                        builder.AppendLine($"if ({nullCheck})");
                        builder.AppendLine("{");
                        builder.AppendLine(4, $"queryParams.Add($\"{QueryKey(param.Name!)}={{{reservedValue}}}\");");
                        builder.AppendLine("}");
                    }

                    continue;
                }

                // An enum: the value as the specification writes it (an integer enum's number), not the member name
                if (WireValueHelper.IsStringEnum(param.Schema) || WireValueHelper.IsIntegerEnum(param.Schema))
                {
                    var wireValue = WireValueHelper.IsIntegerEnum(param.Schema)
                        ? WireValueHelper.IntegerEnumWireExpression(paramAccess)
                        : $"Uri.EscapeDataString({paramAccess}.ToWireValue()!)";
                    if (isRequired)
                    {
                        builder.AppendLine($"queryParams.Add($\"{QueryKey(param.Name!)}={{{wireValue}}}\");");
                    }
                    else
                    {
                        builder.AppendLine();
                        builder.AppendLine($"if ({paramAccess} is not null)");
                        builder.AppendLine("{");
                        builder.AppendLine(4, $"queryParams.Add($\"{QueryKey(param.Name!)}={{{wireValue}}}\");");
                        builder.AppendLine("}");
                    }

                    continue;
                }

                // Default path: scalar primitive with URL encoding (existing behavior)
                if (isRequired)
                {
                    var valueExpression = BuildEncodedExpression(paramAccess, paramType);
                    builder.AppendLine($"queryParams.Add($\"{QueryKey(param.Name!)}={{{valueExpression}}}\");");
                }
                else
                {
                    // Use appropriate null check based on type for optional parameters. A bare
                    // T[] that reaches this scalar path (not the form-explode foreach above) keeps
                    // its length guard; everything else delegates to the shared BuildNullCheck.
                    var nullCheck = paramType.EndsWith("[]", StringComparison.Ordinal)
                        ? $"{paramAccess} is not null && {paramAccess}.Length > 0"
                        : BuildNullCheck(paramAccess, paramType);

                    var valueExpression = BuildEncodedExpression(paramAccess, paramType);

                    builder.AppendLine();
                    builder.AppendLine($"if ({nullCheck})");
                    builder.AppendLine("{");
                    builder.AppendLine(4, $"queryParams.Add($\"{QueryKey(param.Name!)}={{{valueExpression}}}\");");
                    builder.AppendLine("}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("if (queryParams.Count > 0)");
            builder.AppendLine("{");
            builder.AppendLine(4, "url += \"?\" + string.Join(\"&\", queryParams);");
            builder.AppendLine("}");
            builder.AppendLine();
        }

        // OAS 3.2 in:querystring — raw query string appended to URL as-is (no key=value encoding).
        // Multiple querystring params on the same endpoint are unusual but handled by appending.
        foreach (var (param, referenceId) in querystringParams)
        {
            var propName = propertyNames.Get(param, referenceId);
            var paramAccess = $"parameters.{propName}";

            builder.AppendLine();
            builder.AppendLine($"if (!string.IsNullOrEmpty({paramAccess}))");
            builder.AppendLine("{");
            builder.AppendLine(4, $"url += (url.Contains('?') ? \"&\" : \"?\") + {paramAccess};");
            builder.AppendLine("}");

            builder.AppendLine();
        }

        // Per-operation retry (x-retry-*): the statement that tells the resilience handler how to run
        // this request, for a given request variable — null when the handler default already matches.
        IOpenApiPathItem? pathItem = null;
        _ = openApiDoc.Paths?.TryGetValue(path, out pathItem);
        string? Stamp(
            string requestVariable,
            bool streamed)
        {
            // What every send has to put on its request: the header and cookie parameters, then the retry stamp, and
            // for a response that is not streamed the one that has the resilience handler read its body in the attempt
            var statements = new[]
                {
                    BuildHeaderAndCookieStatements(headerParams, cookieParams, requestVariable, openApiDoc, parametersClassName, propertyNames),
                    ResilienceRequestHelper.BuildStampStatement(operation, pathItem, openApiDoc, httpMethod, requestVariable),
                    streamed ? null : ResilienceRequestHelper.BuildBufferStatement(openApiDoc, requestVariable),
                }
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            return statements.Count == 0
                ? null
                : string.Join(Environment.NewLine, statements);
        }

        // A streamed response on another verb (a POST that streams its answer, a body streamed in and out): the request
        // with its body, read as far as the headers, then the items as they arrive - as for a GET
        if (isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType) && !string.Equals(httpMethod, "GET", StringComparison.Ordinal))
        {
            var content = AppendRequestContent(builder, operation, openApiDoc, hasParameters, xmlRead);
            GenerateGetMethodBody(returnType, isAsyncEnumerable, streamingItemType, operation.GetStreamingFraming(), operation.GetServerSentEventItemNames(), hasReturnType, builder, resultStyle, Stamp, xmlRead, EndpointMapHelper.BuildHttpMethodExpression(httpMethod), content);
            return builder
                .ToString()
                .Trim();
        }

        switch (httpMethod)
        {
            case "GET":
                GenerateGetMethodBody(returnType, isAsyncEnumerable, streamingItemType, operation.GetStreamingFraming(), operation.GetServerSentEventItemNames(), hasReturnType, builder, resultStyle, Stamp, xmlRead);
                break;
            case "POST":
                GeneratePostMethodBody(operation, openApiDoc, returnType, hasParameters, hasReturnType, hasLocationHeader, builder, resultStyle, Stamp("requestMessage", streamed: false), xmlRead, isTextResponse);
                break;
            case "PUT":
                GeneratePutMethodBody(operation, openApiDoc, returnType, hasParameters, hasReturnType, builder, resultStyle, Stamp("requestMessage", streamed: false), xmlRead, isTextResponse);
                break;
            case "DELETE":
                GenerateDeleteMethodBody(returnType, hasReturnType, builder, resultStyle, Stamp("requestMessage", streamed: false), xmlRead, isTextResponse);
                break;
            default:
                // Non-standard verbs: OpenAPI 3.2 `query` and `additionalOperations`
                // (e.g. LINK), plus PATCH. No dedicated HttpClient JSON helper exists,
                // so build the request explicitly and dispatch by body/return presence.
                GenerateGenericMethodBody(httpMethod, operation, openApiDoc, returnType, hasParameters, hasReturnType, builder, resultStyle, Stamp("requestMessage", streamed: false), xmlRead, isTextResponse);
                break;
        }

        return builder
            .ToString()
            .Trim();
    }

    private static void GenerateGetMethodBody(
        string returnType,
        bool isAsyncEnumerable,
        string? streamingItemType,
        StreamingFraming streamingFraming,
        IReadOnlyList<string> sseItemEvents,
        bool hasReturnType,
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        Func<string, bool, string?> resilienceStamp,
        XmlReadPlan? xmlRead = null,
        string httpMethodExpression = "HttpMethod.Get",
        string? contentExpression = null)
    {
        // Special handling for async enumerable streaming
        if (isAsyncEnumerable && !string.IsNullOrEmpty(streamingItemType))
        {
            if (resultStyle == TypedClientResultStyleType.Result)
            {
                AppendStreamingResultBody(builder, streamingItemType!, streamingFraming, sseItemEvents, resilienceStamp("requestMessage", true), httpMethodExpression, contentExpression);
                return;
            }

            AppendSend(
                builder,
                "using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);",
                resilienceStamp("requestMessage", true),
                httpMethodExpression,
                contentExpression,
                responseHeadersRead: true,
                disposeResponse: true,
                forceRequestMessage: httpMethodExpression != "HttpMethod.Get" || contentExpression is not null);
            builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);");
            builder.AppendLine();
            builder.AppendLine("var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);");
            builder.AppendLine();

            // Server-Sent Events, JSON Lines and JSON Text Sequence use the emitted StreamReaders
            // helper to parse their wire framing; multipart/mixed additionally extracts the boundary
            // from the response Content-Type and passes it to the reader. The remaining framings
            // (JsonArray, ...) keep the legacy DeserializeAsyncEnumerable brace-scan path byte-for-byte.
            if (streamingFraming == StreamingFraming.ServerSentEvents)
            {
                builder.AppendLine($"await foreach (var item in {ServerSentEventsReader(streamingItemType!, sseItemEvents)}.ConfigureAwait(false))");
            }
            else if (streamingFraming == StreamingFraming.JsonLines)
            {
                builder.AppendLine($"await foreach (var item in StreamReaders.ReadJsonLinesAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken).ConfigureAwait(false))");
            }
            else if (streamingFraming == StreamingFraming.JsonSequence)
            {
                builder.AppendLine($"await foreach (var item in StreamReaders.ReadJsonSequenceAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken).ConfigureAwait(false))");
            }
            else if (streamingFraming == StreamingFraming.MultipartMixed)
            {
                builder.AppendLine("var boundary = response.Content.Headers.ContentType?.Parameters");
                builder.AppendLine(4, ".FirstOrDefault(p => string.Equals(p.Name, \"boundary\", StringComparison.OrdinalIgnoreCase))?.Value?.Trim('\"')");
                builder.AppendLine(4, "?? throw new global::System.IO.InvalidDataException(\"A multipart/mixed response without a boundary in its Content-Type cannot be split into its parts.\");");
                builder.AppendLine($"await foreach (var item in StreamReaders.ReadMultipartMixedAsync<{streamingItemType}>(stream, boundary, jsonSerializerOptions, cancellationToken).ConfigureAwait(false))");
            }
            else if (streamingFraming == StreamingFraming.JsonArray)
            {
                builder.AppendLine($"await foreach (var item in StreamReaders.ReadJsonArrayAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken).ConfigureAwait(false))");
            }
            else
            {
                // JsonArray is the legitimate legacy fallthrough above. Any other framing reaching
                // here is a StreamReaders-based wire framing that was added without an explicit
                // reader branch — fail loudly at generation time rather than silently emitting the
                // JSON-array brace-scan path (wrong bytes). Mirrors the per-op ReaderMethodName switch.
                throw new InvalidOperationException(
                    $"No typed-client stream reader is defined for framing '{streamingFraming}'.");
            }

            builder.AppendLine("{");
            builder.AppendLine(4, "if (item is not null)");
            builder.AppendLine(4, "{");
            builder.AppendLine(8, "yield return item;");
            builder.AppendLine(4, "}");
            builder.AppendLine("}");
        }
        else if (hasReturnType && returnType == "StreamedFile")
        {
            // A streamed download: read as far as the headers and handed over open - the StreamedFile owns the response
            AppendSend(
                builder,
                "var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);",
                resilienceStamp("requestMessage", false),
                "HttpMethod.Get",
                contentExpression: null,
                responseHeadersRead: true);
            if (resultStyle == TypedClientResultStyleType.Result)
            {
                builder.AppendLine("return await BuildStreamedFileResponseAsync(response, cancellationToken).ConfigureAwait(false);");
            }
            else
            {
                // On an error status the response is the exception's to describe, not the caller's to dispose
                builder.AppendLine("try");
                builder.AppendLine("{");
                builder.AppendLine(4, "await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);");
                builder.AppendLine(4, "return await StreamedFile.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false);");
                builder.AppendLine("}");
                builder.AppendLine("catch");
                builder.AppendLine("{");
                builder.AppendLine(4, "response.Dispose();");
                builder.AppendLine(4, "throw;");
                builder.AppendLine("}");
            }
        }
        else if (hasReturnType && returnType == "DownloadedFile")
        {
            // Binary content download - use ReadAsByteArrayAsync
            AppendSend(builder, "var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);", resilienceStamp("requestMessage", false), "HttpMethod.Get", contentExpression: null);
            AppendResponseCompletion(builder, resultStyle, ResponsePayloadKind.Binary, returnType, isLastStatement: true);
        }
        else if (hasReturnType && returnType == "string")
        {
            // Text content - use ReadAsStringAsync
            AppendSend(builder, "var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);", resilienceStamp("requestMessage", false), "HttpMethod.Get", contentExpression: null);
            AppendResponseCompletion(builder, resultStyle, ResponsePayloadKind.Text, returnType, isLastStatement: true);
        }
        else
        {
            // No GetFromJsonAsync shortcut for the Throw style: its exception drops the error body, whereas
            // EnsureSuccessAsync (emitted by AppendResponseCompletion) keeps it in the message.
            AppendSend(builder, "var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);", resilienceStamp("requestMessage", false), "HttpMethod.Get", contentExpression: null, accept: xmlRead?.Accept);

            AppendResponseCompletion(
                builder,
                resultStyle,
                hasReturnType ? (xmlRead?.ReadMethod is null || xmlRead.ByContentType ? ResponsePayloadKind.Json : ResponsePayloadKind.Xml) : ResponsePayloadKind.None,
                returnType,
                isLastStatement: true,
                xmlRead: xmlRead);
        }
    }

    /// <summary>
    /// Emits the Result-style body for a streaming operation.
    /// </summary>
    /// <remarks>
    /// The method returns <c>Task&lt;StreamingEndpointResponse&lt;T&gt;&gt;</c>, so it cannot be an iterator
    /// (<c>yield</c> is illegal in a Task-returning method). Instead the status envelope is resolved as soon
    /// as the response headers arrive and the still-unconsumed reader is handed to the envelope as its content,
    /// which preserves lazy streaming. This mirrors EndpointPerOperation mode.
    /// The response is deliberately NOT disposed here - <c>StreamingEndpointResponse&lt;T&gt;</c> owns it and
    /// disposes it once the caller disposes the envelope, otherwise the stream would be closed before it is read.
    /// </remarks>
    private static void AppendStreamingResultBody(
        StringBuilder builder,
        string streamingItemType,
        StreamingFraming streamingFraming,
        IReadOnlyList<string> sseItemEvents,
        string? resilienceStamp,
        string httpMethodExpression = "HttpMethod.Get",
        string? contentExpression = null)
    {
        AppendSend(
            builder,
            "var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);",
            resilienceStamp,
            httpMethodExpression,
            contentExpression,
            responseHeadersRead: true,
            forceRequestMessage: httpMethodExpression != "HttpMethod.Get" || contentExpression is not null);
        builder.AppendLine();
        builder.AppendLine("if (!response.IsSuccessStatusCode)");
        builder.AppendLine("{");
        builder.AppendLine(4, "var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        builder.AppendLine(4, $"return new StreamingEndpointResponse<{streamingItemType}>(false, response.StatusCode, content: null, errorContent, response);");
        builder.AppendLine("}");
        builder.AppendLine();

        // Until the envelope owns the response, a failure (the body cannot be opened, no multipart boundary) disposes it
        var reader = new StringBuilder();
        reader.AppendLine("var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);");
        reader.AppendLine();
        AppendMultipartBoundaryIfNeeded(reader, streamingFraming);
        if (streamingFraming == StreamingFraming.ServerSentEvents)
        {
            // An event stream: Atc.Rest.Client's reader, so the envelope's LastEventId follows the events as they are
            // read - what a caller sends as Last-Event-ID to continue a stream that dropped
            var itemEvents = sseItemEvents.Count == 0
                ? string.Empty
                : $", ItemEventNames = new[] {{ {string.Join(", ", sseItemEvents.Select(CSharpLiteralHelper.ToStringLiteral))} }}";
            reader.AppendLine($"return global::Atc.Rest.Client.ServerSentEventReader.Read<{streamingItemType}>(");
            reader.AppendLine("    response,");
            reader.AppendLine("    stream,");
            reader.AppendLine($"    json => JsonSerializer.Deserialize(json, jsonSerializerOptions.TypeInfo<{streamingItemType}>()),");
            reader.AppendLine($"    new global::Atc.Rest.Client.StreamReadOptions {{ MaxRecordLength = StreamReaders.MaxRecordLength{itemEvents} }},");
            reader.Append("    cancellationToken);");
        }
        else
        {
            reader.AppendLine($"var content = {GetStreamReaderExpression(streamingItemType, streamingFraming, sseItemEvents)};");
            reader.AppendLine();
            reader.Append($"return new StreamingEndpointResponse<{streamingItemType}>(true, response.StatusCode, content, errorContent: null, response);");
        }

        builder.AppendLine("try");
        builder.AppendLine("{");
        foreach (var line in reader.ToString().Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (line.Length == 0)
            {
                builder.AppendLine();
            }
            else
            {
                builder.AppendLine(4, line);
            }
        }

        builder.AppendLine("}");
        builder.AppendLine("catch");
        builder.AppendLine("{");
        builder.AppendLine(4, "response.Dispose();");
        builder.AppendLine(4, "throw;");
        builder.Append('}');
    }

    /// <summary>
    /// Emits the multipart/mixed boundary extraction, which must precede the reader expression.
    /// </summary>
    private static void AppendMultipartBoundaryIfNeeded(
        StringBuilder builder,
        StreamingFraming streamingFraming)
    {
        if (streamingFraming != StreamingFraming.MultipartMixed)
        {
            return;
        }

        builder.AppendLine("var boundary = response.Content.Headers.ContentType?.Parameters");
        builder.AppendLine(4, ".FirstOrDefault(p => string.Equals(p.Name, \"boundary\", StringComparison.OrdinalIgnoreCase))?.Value?.Trim('\"')");
        builder.AppendLine(4, "?? throw new global::System.IO.InvalidDataException(\"A multipart/mixed response without a boundary in its Content-Type cannot be split into its parts.\");");
    }

    /// <summary>
    /// Returns the reader expression that produces the <c>IAsyncEnumerable</c> for a wire framing.
    /// </summary>
    private static string GetStreamReaderExpression(
        string streamingItemType,
        StreamingFraming streamingFraming,
        IReadOnlyList<string> sseItemEvents)
        => streamingFraming switch
        {
            StreamingFraming.ServerSentEvents => ServerSentEventsReader(streamingItemType, sseItemEvents),
            StreamingFraming.JsonLines => $"StreamReaders.ReadJsonLinesAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken)",
            StreamingFraming.JsonSequence => $"StreamReaders.ReadJsonSequenceAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken)",
            StreamingFraming.MultipartMixed => $"StreamReaders.ReadMultipartMixedAsync<{streamingItemType}>(stream, boundary, jsonSerializerOptions, cancellationToken)",

            // A JSON array: the serializer's own element-by-element read, behind a look for an empty body
            StreamingFraming.JsonArray => $"StreamReaders.ReadJsonArrayAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken)",

            // Any other framing is a StreamReaders-based wire framing that was added without an explicit
            // reader branch - fail loudly at generation time rather than silently emitting the
            // JSON-array brace-scan path (wrong bytes). Mirrors the per-op ReaderMethodName switch.
            _ => throw new InvalidOperationException(
                $"No typed-client stream reader is defined for framing '{streamingFraming}'."),
        };

    // The reader of an event stream: with the names of x-sse-item-events when the operation gives any
    private static string ServerSentEventsReader(
        string streamingItemType,
        IReadOnlyList<string> sseItemEvents)
        => sseItemEvents.Count == 0
            ? $"StreamReaders.ReadServerSentEventsAsync<{streamingItemType}>(stream, jsonSerializerOptions, cancellationToken)"
            : $"StreamReaders.ReadServerSentEventsOfAsync<{streamingItemType}>(stream, jsonSerializerOptions, new[] {{ {string.Join(", ", sseItemEvents.Select(CSharpLiteralHelper.ToStringLiteral))} }}, cancellationToken)";

    private static void GenerateGenericMethodBody(
        string httpMethod,
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        string returnType,
        bool hasParameters,
        bool hasReturnType,
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        string? resilienceStamp,
        XmlReadPlan? xmlRead = null,
        bool isTextResponse = false)
    {
        // Resolve the verb through the same helper the per-operation client uses, so both
        // generated clients express the same verb the same way: the static HttpMethod property
        // for RFC verbs (HttpMethod.Query, ...) and new HttpMethod("VERB") only for custom
        // additionalOperations verbs that have none.
        var httpMethodExpression = EndpointMapHelper.BuildHttpMethodExpression(httpMethod);

        // The body by the same rule as every other verb; a form, file or multipart body is built into a local first
        var content = AppendRequestContent(builder, operation, openApiDoc, hasParameters, xmlRead);
        if (!string.Equals(content, LocalContent, StringComparison.Ordinal))
        {
            builder.AppendLine();
        }

        builder.AppendLine($"using var requestMessage = new HttpRequestMessage({httpMethodExpression}, url);");

        if (content is not null)
        {
            builder.AppendLine($"requestMessage.Content = {content};");
        }

        if (xmlRead?.Accept is not null)
        {
            builder.AppendLine($"requestMessage.Headers.Accept.ParseAdd(\"{xmlRead.Accept}\");");
        }

        if (resilienceStamp is not null)
        {
            builder.AppendLine(resilienceStamp);
        }

        builder.AppendLine();
        builder.AppendLine("using var response = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);");

        AppendResponseCompletion(
            builder,
            resultStyle,
            PayloadKind(hasReturnType, xmlRead, isTextResponse),
            returnType,
            isLastStatement: hasReturnType,
            xmlRead: xmlRead);
    }

    /// <summary>
    /// The content of a direct file upload, into a <c>content</c> local: the file(s) as multipart/form-data parts, or
    /// the raw file with its media type.
    /// </summary>
    private static void AppendDirectFileUploadContent(
        StringBuilder builder,
        OpenApiOperation operation,
        string? fileUploadContentType)
    {
        // Check if this is a multi-file upload (array of binary)
        var isMultiFileUpload = IsMultiFileUpload(operation);

        // Generate file upload code (use null-forgiving operator since File is optional in spec but required for upload)
        if (fileUploadContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
        {
            builder.AppendLine("using var content = new MultipartFormDataContent();");

            if (isMultiFileUpload)
            {
                // Multi-file upload - iterate over the array
                builder.AppendLine();
                builder.AppendLine("for (var i = 0; i < parameters.File!.Length; i++)");
                builder.AppendLine("{");
                builder.AppendLine(4, "var fileItem = parameters.File[i];");
                builder.AppendLine(4, "var streamContent = new StreamContent(fileItem.OpenReadStream());");
                builder.AppendLine();
                builder.AppendLine(4, "if (fileItem.ContentType is not null)");
                builder.AppendLine(4, "{");
                builder.AppendLine(8, "streamContent.Headers.ContentType = new MediaTypeHeaderValue(fileItem.ContentType);");
                builder.AppendLine(4, "}");
                builder.AppendLine();
                builder.AppendLine(4, "content.Add(streamContent, \"files\", fileItem.FileName);");
                builder.AppendLine("}");
            }
            else
            {
                // Single file upload
                builder.AppendLine("var streamContent = new StreamContent(parameters.File!.OpenReadStream());");
                builder.AppendLine();
                builder.AppendLine("if (parameters.File!.ContentType is not null)");
                builder.AppendLine("{");
                builder.AppendLine(4, "streamContent.Headers.ContentType = new MediaTypeHeaderValue(parameters.File.ContentType);");
                builder.AppendLine("}");
                builder.AppendLine();
                builder.AppendLine("content.Add(streamContent, \"file\", parameters.File.FileName);");
            }
        }
        else
        {
            // application/octet-stream or image/* content types
            builder.AppendLine("using var content = new StreamContent(parameters.File!.OpenReadStream());");
            builder.AppendLine($"content.Headers.ContentType = new MediaTypeHeaderValue({RawFileBodyHelper.ContentTypeExpression(operation, "parameters.File", fileUploadContentType ?? "application/octet-stream")});");
        }
    }

    /// <summary>
    /// The name of the local a form, file or multipart body is built into.
    /// </summary>
    private const string LocalContent = "content";

    /// <summary>
    /// The request body of an operation as content - the one rule every verb sends its body by: a stream of records, a
    /// file, a multipart form, a URL-encoded form, XML or JSON, with the statements that build it emitted first;
    /// <see langword="null"/> when there is no body.
    /// </summary>
    private static string? AppendRequestContent(
        StringBuilder builder,
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        bool hasParameters,
        XmlReadPlan? xmlRead)
    {
        if (!hasParameters || !operation.HasRequestBody())
        {
            return null;
        }

        const string requestAccess = "parameters.Request";
        if (StreamedBodyContent(operation, requestAccess) is { } streamed)
        {
            return streamed;
        }

        if (IsDirectFileUpload(operation))
        {
            builder.AppendLine();
            AppendDirectFileUploadContent(builder, operation, operation.GetFileUploadContentType());
            builder.AppendLine();
            return LocalContent;
        }

        if (GetSchemaBasedMultipartSchema(operation, openApiDoc) is { } multipartSchema)
        {
            // The parts end with a blank line of their own
            GenerateSchemaBasedMultipartFormData(builder, multipartSchema, FormBodyHelper.FindMultipartFormData(operation), requestAccess);
            return LocalContent;
        }

        if (UrlEncodedFormSchema(operation, openApiDoc) is { } formSchema)
        {
            builder.AppendLine();
            GenerateUrlEncodedForm(builder, formSchema, requestAccess);
            return LocalContent;
        }

        if (xmlRead?.RequestContent is { } xmlContent)
        {
            return xmlContent;
        }

        return operation.GetJsonRequestBodyMediaType() is not null
            ? JsonBodyContent(operation, requestAccess)
            : null;
    }

    private static void GeneratePostMethodBody(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        string returnType,
        bool hasParameters,
        bool hasReturnType,
        bool hasLocationHeader,
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        string? resilienceStamp,
        XmlReadPlan? xmlRead = null,
        bool isTextResponse = false)
    {
        AppendBodySend(builder, "PostAsync", "HttpMethod.Post", operation, openApiDoc, hasParameters, resilienceStamp, xmlRead);

        if (hasLocationHeader)
        {
            // Return the Location header as Uri
            if (resultStyle == TypedClientResultStyleType.Result)
            {
                builder.Append("return await BuildLocationResponseAsync(response, cancellationToken).ConfigureAwait(false);");
            }
            else
            {
                builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);");
                builder.Append("return response.Headers.Location!;");
            }
        }
        else
        {
            AppendResponseCompletion(
                builder,
                resultStyle,
                PayloadKind(hasReturnType, xmlRead, isTextResponse),
                returnType,
                isLastStatement: true,
                xmlRead: xmlRead);
        }
    }

    private static void GeneratePutMethodBody(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        string returnType,
        bool hasParameters,
        bool hasReturnType,
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        string? resilienceStamp,
        XmlReadPlan? xmlRead = null,
        bool isTextResponse = false)
    {
        AppendBodySend(builder, "PutAsync", "HttpMethod.Put", operation, openApiDoc, hasParameters, resilienceStamp, xmlRead);

        AppendResponseCompletion(
            builder,
            resultStyle,
            PayloadKind(hasReturnType, xmlRead, isTextResponse),
            returnType,
            isLastStatement: true,
            xmlRead: xmlRead);
    }

    /// <summary>
    /// Emits the send of a verb with an <c>HttpClient</c> helper (<c>PostAsync</c>, <c>PutAsync</c>) and its body, built by
    /// <see cref="AppendRequestContent"/>.
    /// </summary>
    private static void AppendBodySend(
        StringBuilder builder,
        string helper,
        string httpMethodExpression,
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        bool hasParameters,
        string? resilienceStamp,
        XmlReadPlan? xmlRead)
    {
        var content = AppendRequestContent(builder, operation, openApiDoc, hasParameters, xmlRead);
        AppendSend(
            builder,
            $"var response = await httpClient.{helper}(url, {content ?? "null"}, cancellationToken).ConfigureAwait(false);",
            resilienceStamp,
            httpMethodExpression,
            content,
            accept: xmlRead?.Accept);
    }

    private static void GenerateDeleteMethodBody(
        string returnType,
        bool hasReturnType,
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        string? resilienceStamp,
        XmlReadPlan? xmlRead = null,
        bool isTextResponse = false)
    {
        AppendSend(builder, "var response = await httpClient.DeleteAsync(url, cancellationToken).ConfigureAwait(false);", resilienceStamp, "HttpMethod.Delete", contentExpression: null, accept: xmlRead?.Accept);

        AppendResponseCompletion(
            builder,
            resultStyle,
            PayloadKind(hasReturnType, xmlRead, isTextResponse),
            returnType,
            isLastStatement: true,
            xmlRead: xmlRead);
    }

    /// <summary>
    /// Whether the JSON request body is declared as <c>application/json</c> (the content's default media type).
    /// </summary>
    private static bool IsPlainJson(OpenApiOperation operation)
        => string.Equals(operation.GetJsonRequestBodyMediaType(), "application/json", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A request body sent as a stream of records (JSON Lines, JSON Text Sequences) as content: the generated
    /// <c>StreamedRequestContent</c>, written with the client's JSON options in the first declared sequential media
    /// type; <see langword="null"/> when the body is not one.
    /// </summary>
    private static string? StreamedBodyContent(
        OpenApiOperation operation,
        string requestAccess)
        => operation.GetStreamedRequestBody() is { } streamed
            ? StreamedRequestContentExtractor.CreateContentExpression(requestAccess, "jsonSerializerOptions", streamed.PrimaryMediaType, streamed.IsRequired)
            : null;

    /// <summary>
    /// The JSON request body as content: with the declared media type when it is another JSON media type
    /// (<c>application/merge-patch+json</c>, <c>application/vnd.x+json</c>).
    /// </summary>
    private static string JsonBodyContent(
        OpenApiOperation operation,
        string requestAccess)
        => IsPlainJson(operation)
            ? $"jsonSerializerOptions.ToJsonContent({requestAccess})"
            : $"jsonSerializerOptions.ToJsonContent({requestAccess}, new System.Net.Http.Headers.MediaTypeHeaderValue(\"{operation.GetJsonRequestBodyMediaType()}\"))";

    /// <summary>
    /// Emits the send for an operation: the verb helper call (<c>GetAsync</c>, <c>PostAsJsonAsync</c>, ...) as
    /// before, or — when the operation has its own retry settings (x-retry-*) — the same request built as an
    /// <c>HttpRequestMessage</c> carrying the resilience stamp the API resilience handler reads.
    /// </summary>
    private static void AppendSend(
        StringBuilder builder,
        string plainSendStatement,
        string? resilienceStamp,
        string httpMethodExpression,
        string? contentExpression,
        bool responseHeadersRead = false,
        bool disposeResponse = false,
        string? accept = null,
        bool forceRequestMessage = false)
    {
        // A buffered response is read in full before the method returns, so the method disposes it; only a streamed
        // response the caller takes over (the Result style's streaming envelope) is left undisposed
        disposeResponse |= !responseHeadersRead;
        if (resilienceStamp is null && accept is null && !forceRequestMessage)
        {
            builder.AppendLine(disposeResponse && plainSendStatement.StartsWith("var response", StringComparison.Ordinal)
                ? "using " + plainSendStatement
                : plainSendStatement);
            return;
        }

        builder.AppendLine($"using var requestMessage = new HttpRequestMessage({httpMethodExpression}, url);");
        if (contentExpression is not null)
        {
            builder.AppendLine($"requestMessage.Content = {contentExpression};");
        }

        if (accept is not null)
        {
            builder.AppendLine($"requestMessage.Headers.Accept.ParseAdd(\"{accept}\");");
        }

        if (resilienceStamp is not null)
        {
            builder.AppendLine(resilienceStamp);
            if (resilienceStamp.Contains('\n'))
            {
                builder.AppendLine();
            }
        }

        var completion = responseHeadersRead ? "HttpCompletionOption.ResponseHeadersRead, " : string.Empty;
        var declaration = disposeResponse ? "using var" : "var";
        builder.AppendLine($"{declaration} response = await httpClient.SendAsync(requestMessage, {completion}cancellationToken).ConfigureAwait(false);");
    }

    /// <summary>
    /// The statements that put the operation's header and cookie parameters on a request, or
    /// <see langword="null"/> when it has none. A value is written as its wire text: never with the
    /// culture of the machine the client runs on.
    /// </summary>
    private static string? BuildHeaderAndCookieStatements(
        List<(OpenApiParameter Param, string? ReferenceId)> headerParams,
        List<(OpenApiParameter Param, string? ReferenceId)> cookieParams,
        string requestVariable,
        OpenApiDocument openApiDoc,
        string parametersClassName,
        ParameterPropertyNames propertyNames)
    {
        if (headerParams.Count == 0 && cookieParams.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        foreach (var (param, referenceId) in headerParams)
        {
            var propName = propertyNames.Get(param, referenceId);
            var variable = "header" + propName;
            var paramType = GetParameterTypeWithInlineEnumAwareness(param, openApiDoc, parametersClassName, propName);

            sb.AppendLine($"if (parameters.{propName} is {ValuePattern(paramType)} {variable})");
            sb.AppendLine("{");

            // An array: its items joined by commas (style: simple)
            var headerText = param.GetArrayDelimiter() is { } delimiter
                ? JoinedItemsExpression(param, variable, paramType, delimiter, escapeItems: false)
                : WireValueHelper.IsIntegerEnum(param.Schema)
                    ? WireValueHelper.IntegerEnumWireExpression(variable) + "!"
                    : BuildWireTextExpression(variable, paramType, WireValueHelper.IsStringEnum(param.Schema));
            sb.AppendLine(4, $"{requestVariable}.Headers.Add({CSharpLiteralHelper.ToStringLiteral(param.Name!)}, {headerText});");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // RFC 6265 Cookie header (name=value; name2=value2), each value percent-encoded (the server decodes it) unless
        // the parameter declares style: cookie (sent as is)
        if (cookieParams.Count > 0)
        {
            sb.AppendLine("var cookieParts = new List<string>();");
            foreach (var (param, referenceId) in cookieParams)
            {
                var propName = propertyNames.Get(param, referenceId);
                var variable = "cookie" + propName;
                var paramType = GetParameterTypeWithInlineEnumAwareness(param, openApiDoc, parametersClassName, propName);

                sb.AppendLine($"if (parameters.{propName} is {ValuePattern(paramType)} {variable})");
                sb.AppendLine("{");
                var text = BuildWireTextExpression(variable, paramType, WireValueHelper.IsStringEnum(param.Schema));
                sb.AppendLine(4, $"cookieParts.Add(\"{CSharpLiteralHelper.EscapeString(param.Name!)}=\" + {(param.Style == ParameterStyle.Cookie ? text : $"Uri.EscapeDataString({text})")});");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            sb.AppendLine("if (cookieParts.Count > 0)");
            sb.AppendLine("{");
            sb.AppendLine(4, $"{requestVariable}.Headers.TryAddWithoutValidation(\"Cookie\", string.Join(\"; \", cookieParts));");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();

        // A value that is there: an empty string is left out, as before
        static string ValuePattern(string paramType)
            => paramType.TrimEnd('?') == "string" ? "{ Length: > 0 }" : "{ }";
    }

    /// <summary>
    /// A C# expression of type <c>string</c>: an array's items joined on its delimiter, each as the specification writes
    /// it (an enum by its wire value) and, in a path, percent-encoded on its own so the delimiter stays one.
    /// </summary>
    private static string JoinedItemsExpression(
        OpenApiParameter param,
        string access,
        string paramType,
        char delimiter,
        bool escapeItems)
    {
        var itemsSchema = param.Schema.Dereferenced?.Items;
        var itemType = TryGetListElementType(paramType, out var elementType) ? elementType : "string";
        var itemText = WireValueHelper.IsIntegerEnum(itemsSchema)
            ? WireValueHelper.IntegerEnumWireExpression("item") + "!"
            : BuildWireTextExpression("item", itemType, WireValueHelper.IsStringEnum(itemsSchema));
        if (escapeItems)
        {
            itemText = $"Uri.EscapeDataString({itemText})";
        }

        return $"string.Join({CSharpLiteralHelper.ToStringLiteral(delimiter.ToString())}, global::System.Linq.Enumerable.Select({access}, item => {itemText}))";
    }

    /// <summary>
    /// A C# expression of type <c>string</c> that gives the wire text of a non-null scalar value.
    /// </summary>
    private static string BuildWireTextExpression(
        string valueExpression,
        string paramType,
        bool isEnum)
    {
        if (isEnum)
        {
            return $"{valueExpression}.ToWireValue()";
        }

        var type = paramType.TrimEnd('?');
        if (type == "string")
        {
            return valueExpression;
        }

        if (type == "bool")
        {
            return $"({valueExpression} ? \"true\" : \"false\")";
        }

        return WireValueHelper.TryBuildWireExpression(valueExpression, type) is { } wireValue
            ? wireValue + "!"
            : $"global::System.FormattableString.Invariant($\"{{{valueExpression}}}\")";
    }

    private static string GetParameterType(
        OpenApiParameter param,
        OpenApiDocument openApiDoc)
    {
        if (param.Schema is null)
        {
            return "string";
        }

        return GetSchemaTypeName(param.Schema, openApiDoc);
    }

    /// <summary>
    /// Returns the C# type name for a parameter, accounting for inline enums.
    /// When the schema is an inline enum (string + enum values, no $ref), the type is
    /// the generated enum name (<c>{parametersClassName}{PropertyName}</c>) — matches what
    /// <see cref="OperationParameterExtractor"/> writes on the corresponding record property.
    /// The URL-builder then takes the non-string branch in <see cref="BuildEncodedExpression"/>,
    /// which produces <c>Uri.EscapeDataString($"{...}")</c> — interpolation handles the
    /// enum-to-string conversion safely.
    /// </summary>
    private static string GetParameterTypeWithInlineEnumAwareness(
        OpenApiParameter param,
        OpenApiDocument openApiDoc,
        string parametersClassName,
        string propName)
    {
        if (param.Schema is OpenApiSchema actualSchema &&
            actualSchema.IsInlineEnum &&
            !string.IsNullOrEmpty(param.Name))
        {
            return InlineEnumExtractor.GenerateInlineEnumTypeName(parametersClassName, propName, TypeConflictRegistry.ComponentTypeNamesOf(openApiDoc));
        }

        return GetParameterType(param, openApiDoc);
    }

    private static string GetSchemaTypeName(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry = null)
        => GetSchemaTypeName(schema, openApiDoc, registry, operationId: null, pathSegment: null, context: null, inlineSchemas: null);

    private static string GetSchemaTypeName(
        IOpenApiSchema? schema,
        OpenApiDocument openApiDoc,
        TypeConflictRegistry? registry,
        string? operationId,
        string? pathSegment,
        string? context,
        Dictionary<string, HttpClientInlineSchemaInfo>? inlineSchemas)
        => ClientSchemaTypeResolver.Resolve(
            schema,
            openApiDoc,
            registry,
            collection: "List",
            topCollection: null,
            context,
            (inlineSchema, inlineContext) =>
            {
                if (string.IsNullOrEmpty(operationId) || pathSegment is null || inlineSchemas is null)
                {
                    return null;
                }

                var typeName = InlineSchemaExtractor.GenerateInlineTypeName(operationId!, inlineContext);
                if (!inlineSchemas.ContainsKey(typeName))
                {
                    var recordParams = InlineSchemaExtractor.ExtractRecordFromInlineSchema(inlineSchema, typeName, registry);
                    inlineSchemas[typeName] = new HttpClientInlineSchemaInfo(typeName, pathSegment, recordParams);
                }

                return typeName;
            });

    /// <summary>
    /// Checks if the operation is a direct file upload (binary or array of binary schema).
    /// Returns false for schema references to objects that may contain file properties.
    /// </summary>
    private static bool IsDirectFileUpload(OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content is null)
        {
            return false;
        }

        foreach (var contentEntry in operation.RequestBody.Content)
        {
            var schema = contentEntry.Value.Schema;

            // Single binary file
            if (schema is OpenApiSchema directSchema &&
                directSchema.Type?.HasFlag(JsonSchemaType.String) == true &&
                string.Equals(directSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Array of binary files
            if (schema is OpenApiSchema { Items: OpenApiSchema itemSchema } && schema.IsOfType(JsonSchemaType.Array) &&
                string.Equals(itemSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Note: Schema references (like FileAsFormDataRequest) are NOT direct file uploads
        }

        return false;
    }

    /// <summary>
    /// Appends a repeated-key foreach block for a form-explode array query parameter.
    /// For required parameters, emits the bare foreach.
    /// For optional parameters, wraps the foreach in a null guard.
    /// Each element is encoded using <see cref="BuildEncodedExpression"/> (or raw when allowReserved).
    /// </summary>
    /// <summary>
    /// A wire name for the inside of a generated interpolated string literal: escaped as text, braces doubled.
    /// </summary>
    // An object in the query: each property that is set, by its key (escaped: filter%5Bname%5D), as a scalar of its type
    private static void AppendQueryObject(
        StringBuilder builder,
        OpenApiParameterExtensions.QueryObject queryObject,
        string paramAccess)
    {
        var local = "queryObject" + paramAccess.Substring(paramAccess.LastIndexOf('.') + 1);
        builder.AppendLine();
        builder.AppendLine($"if ({paramAccess} is {{ }} {local})");
        builder.AppendLine("{");
        foreach (var property in queryObject.Properties)
        {
            var access = $"{local}.{property.WireName.ToPascalCaseForDotNet()}";
            var key = QueryKey(queryObject.KeyOf(property));
            var schema = (IOpenApiSchema?)property.Schema.WrappedEnum() ?? property.Schema;
            var type = property.Schema.ToCSharpTypeForModel(property.Required);
            var value = WireValueHelper.IsIntegerEnum(schema)
                ? WireValueHelper.IntegerEnumWireExpression(access)
                : WireValueHelper.IsStringEnum(schema)
                    ? $"Uri.EscapeDataString({access}.ToWireValue()!)"
                    : BuildEncodedExpression(access, type);
            if (property.Schema.CanBeNullInQueryObject(property.Required))
            {
                builder.AppendLine(4, $"if ({access} is not null)");
                builder.AppendLine(4, "{");
                builder.AppendLine(8, $"queryParams.Add($\"{key}={{{value}}}\");");
                builder.AppendLine(4, "}");
            }
            else
            {
                builder.AppendLine(4, $"queryParams.Add($\"{key}={{{value}}}\");");
            }
        }

        builder.AppendLine("}");
    }

    /// <summary>
    /// A query key as the URI carries it, escaped as its value is: <c>page[size]</c> is <c>page%5Bsize%5D</c>, and a
    /// name with <c>&amp;</c>, <c>=</c>, <c>#</c> or a space stays one key. The escaped text holds nothing a C#
    /// interpolated string would read (no quote, backslash or brace).
    /// </summary>
    private static string QueryKey(string name)
        => Uri.EscapeDataString(name);

    private static void AppendQueryArrayForeach(
        StringBuilder builder,
        string paramName,
        string paramAccess,
        string paramType,
        bool allowReserved,
        bool isRequired,
        bool itemIsEnum = false,
        bool itemIsIntegerEnum = false)
    {
        // Element type comes off the shared List<T>/T[] extractor; if neither shape matches
        // (no array param should reach here that way) fall back to the param type itself.
        if (!TryGetListElementType(paramType, out var elementType))
        {
            elementType = paramType;
        }

        // allowReserved: the reserved characters of a string item as they are, everything else (and #) encoded; a
        // value that is no string is formatted for the wire as always
        var encodedItem = allowReserved
            ? (elementType == "string" ? WireValueHelper.AllowReservedEscapeExpression("item") : BuildEncodedExpression("item", elementType))
            : BuildEncodedExpression("item", elementType);

        // An enum item: the value as the specification writes it
        if (itemIsEnum)
        {
            encodedItem = allowReserved ? WireValueHelper.AllowReservedEscapeExpression("item.ToWireValue()") : "Uri.EscapeDataString(item.ToWireValue())";
        }

        // An integer enum item: its number
        if (itemIsIntegerEnum)
        {
            encodedItem = WireValueHelper.IntegerEnumWireExpression("item");
        }

        if (isRequired)
        {
            builder.AppendLine($"foreach (var item in {paramAccess})");
            builder.AppendLine("{");
            builder.AppendLine(4, $"queryParams.Add($\"{QueryKey(paramName)}={{{encodedItem}}}\");");
            builder.AppendLine("}");
        }
        else
        {
            builder.AppendLine();
            builder.AppendLine($"if ({paramAccess} is not null)");
            builder.AppendLine("{");
            builder.AppendLine(4, $"foreach (var item in {paramAccess})");
            builder.AppendLine(4, "{");
            builder.AppendLine(8, $"queryParams.Add($\"{QueryKey(paramName)}={{{encodedItem}}}\");");
            builder.AppendLine(4, "}");
            builder.AppendLine("}");
        }
    }

    /// <summary>
    /// Extracts the element type from a <c>List&lt;T&gt;</c> or <c>T[]</c> type name.
    /// Upstream contract: array query params arrive as a non-nullable <c>List&lt;T&gt;</c>
    /// (no trailing <c>?</c>, no nesting), and streaming response bodies arrive as
    /// <c>List&lt;T&gt;</c>; both feed this single extractor so the surgery lives in one place.
    /// </summary>
    /// <returns><c>true</c> with <paramref name="elementType"/> set when the shape matched; otherwise <c>false</c>.</returns>
    private static bool TryGetListElementType(
        string paramType,
        out string elementType)
    {
        if (paramType.EndsWith("[]", StringComparison.Ordinal))
        {
            elementType = paramType.Substring(0, paramType.Length - 2);
            return true;
        }

        if (paramType.StartsWith("List<", StringComparison.Ordinal) &&
            paramType.EndsWith(">", StringComparison.Ordinal))
        {
            elementType = paramType.Substring(5, paramType.Length - 6);
            return true;
        }

        elementType = string.Empty;
        return false;
    }

    /// <summary>
    /// Builds the null/empty guard expression for an optional query parameter, selecting the
    /// right form for the parameter's C# type: <c>!string.IsNullOrEmpty(x)</c> for strings,
    /// <c>x.HasValue</c> for nullable value types, and <c>x is not null</c> otherwise.
    /// </summary>
    private static string BuildNullCheck(
        string paramAccess,
        string paramType)
    {
        if (paramType == "string")
        {
            return $"!string.IsNullOrEmpty({paramAccess})";
        }

        if (CSharpTypeHelper.IsBasicValueType(paramType))
        {
            return $"{paramAccess}.HasValue";
        }

        return $"{paramAccess} is not null";
    }

    /// <summary>
    /// Checks if the operation is a multi-file upload (direct array of binary).
    /// Only returns true for direct arrays, not for schema references to objects.
    /// </summary>
    private static bool IsMultiFileUpload(OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content is null)
        {
            return false;
        }

        foreach (var contentEntry in operation.RequestBody.Content)
        {
            var schema = contentEntry.Value.Schema;

            // Direct array of binary - this is a multi-file upload
            if (schema is OpenApiSchema { Items: OpenApiSchema { Format: "binary" } } && schema.IsOfType(JsonSchemaType.Array))
            {
                return true;
            }

            // Note: Schema references to objects (like FilesAsFormDataRequest) that contain
            // file arrays should NOT be treated as direct file uploads - they use the Request pattern
        }

        return false;
    }

    /// <summary>
    /// Gets the schema for schema-based multipart/form-data requests.
    /// Returns the resolved schema if the request body is multipart/form-data (declared with any parameter and in any
    /// case) with a schema reference, otherwise returns null.
    /// </summary>
    private static IOpenApiSchema? GetSchemaBasedMultipartSchema(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc)
    {
        if (operation.RequestBody?.Content is null)
        {
            return null;
        }

        // A model ($ref) under multipart/form-data, declared with any parameter and in any case
        if (FormBodyHelper.FindMultipartFormDataMediaType(operation.RequestBody.Content) is not { } multipartMediaType ||
            operation.RequestBody.Content[multipartMediaType].Schema is not { } schema ||
            schema.GetFileUploadInfo().IsFile)
        {
            return null;
        }

        return FormBodyHelper.ResolveModelSchema(schema, openApiDoc);
    }

    /// <summary>
    /// The model of a request body that is sent as a URL-encoded form: the operation's form body is
    /// <c>application/x-www-form-urlencoded</c> (not multipart, which carries files) and is not also JSON (JSON is
    /// sent then).
    /// </summary>
    private static IOpenApiSchema? UrlEncodedFormSchema(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc)
    {
        if (operation.GetJsonRequestBodyMediaType() is not null ||
            operation.HasFileUpload() ||
            operation.GetFormBodyMediaType() is not { } formMediaType)
        {
            return null;
        }

        return FormBodyHelper.ResolveModelSchema(operation.RequestBody?.Content?[formMediaType].Schema, openApiDoc);
    }

    /// <summary>
    /// Generates the <c>FormUrlEncodedContent</c> of a form model: every field as its wire text, a list as repeated
    /// fields (<see cref="FormBodyHelper"/>).
    /// </summary>
    private static void GenerateUrlEncodedForm(
        StringBuilder builder,
        IOpenApiSchema schema,
        string requestAccess)
    {
        builder.AppendLine("var fields = new List<KeyValuePair<string, string>>();");
        FormBodyHelper.AppendUrlEncodedFields(builder, schema, requestAccess, "fields");
        builder.AppendLine("using var content = new FormUrlEncodedContent(fields);");
        builder.AppendLine();
    }

    /// <summary>
    /// Generates code for schema-based multipart/form-data requests.
    /// Creates MultipartFormDataContent with StringContent for text fields, StreamContent for binary fields and
    /// JsonContent for an object, a map or a composition.
    /// </summary>
    private static void GenerateSchemaBasedMultipartFormData(
        StringBuilder builder,
        IOpenApiSchema schema,
        IOpenApiMediaType? multipart,
        string requestAccess)
    {
        builder.AppendLine("using var content = new MultipartFormDataContent();");
        builder.AppendLine();

        // A file is a StreamContent with its own content type; a text field a StringContent; an object an
        // application/json part written through the client's serializer options (as a JSON body is), or an XML or a
        // URL-encoded part as a StringContent of its media type
        FormBodyHelper.AppendMultipartFields(
            builder,
            schema,
            multipart,
            requestAccess,
            new MultipartPartWriter(
                File: (sb, indent, name, file) =>
                {
                    sb.AppendLine(indent, $"var fileContent = new StreamContent({file}.OpenReadStream());");
                    sb.AppendLine();
                    sb.AppendLine(indent, $"if ({file}.ContentType is not null)");
                    sb.AppendLine(indent, "{");
                    sb.AppendLine(indent + 4, $"fileContent.Headers.ContentType = new MediaTypeHeaderValue({file}.ContentType);");
                    sb.AppendLine(indent, "}");
                    sb.AppendLine();
                    sb.AppendLine(indent, $"content.Add(fileContent, {CSharpLiteralHelper.ToStringLiteral(name)}, {file}.FileName);");
                },
                Text: (sb, indent, name, text) => sb.AppendLine(indent, $"content.Add(new StringContent({text}), {CSharpLiteralHelper.ToStringLiteral(name)});"),
                Json: (sb, indent, name, value) => sb.AppendLine(indent, $"content.Add(jsonSerializerOptions.ToJsonContent({value}), {CSharpLiteralHelper.ToStringLiteral(name)});"),
                Part: (sb, indent, name, text, mediaType) => sb.AppendLine(indent, $"content.Add(new StringContent({text}, global::System.Text.Encoding.UTF8, {CSharpLiteralHelper.ToStringLiteral(mediaType)}), {CSharpLiteralHelper.ToStringLiteral(name)});")));
    }

    /// <summary>
    /// Checks if any of the response content types are binary (application/octet-stream, image/*, etc.).
    /// </summary>
    private static bool IsBinaryResponseContent(
        IDictionary<string, IOpenApiMediaType> content)
        => ResponseBodyClassifier.PrimaryBody(content)?.Kind == ResponseBodyKind.Binary;

    /// <summary>
    /// How a response body is read: as text (a text response), by its model's XML mapping, or as JSON; none without a
    /// return type.
    /// </summary>
    private static ResponsePayloadKind PayloadKind(
        bool hasReturnType,
        XmlReadPlan? xmlRead,
        bool isTextResponse)
    {
        if (!hasReturnType)
        {
            return ResponsePayloadKind.None;
        }

        if (isTextResponse)
        {
            return ResponsePayloadKind.Text;
        }

        return xmlRead?.ReadMethod is null || xmlRead.ByContentType ? ResponsePayloadKind.Json : ResponsePayloadKind.Xml;
    }

    /// <summary>
    /// Checks if any of the response content types are text-based (text/plain, text/html, etc.).
    /// </summary>
    private static bool IsTextResponseContent(
        IDictionary<string, IOpenApiMediaType> content)
    {
        foreach (var key in content.Keys)
        {
            if (key.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Emits the statement(s) that terminate a method body once <c>response</c> is in scope.
    /// </summary>
    /// <remarks>
    /// This is the single place where the Throw-vs-Result decision is made:
    /// <list type="bullet">
    /// <item>Throw - calls <c>EnsureSuccessAsync</c> (which throws on a non-success status) and then reads the content directly.</item>
    /// <item>Result - never throws on status; delegates to a generated Build*ResponseAsync helper that wraps status, raw content and deserialized content in an EndpointResponse envelope.</item>
    /// </list>
    /// Keeping both styles here guarantees the Throw output stays byte-identical to the pre-Result behaviour.
    /// </remarks>
    private static void AppendResponseCompletion(
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        ResponsePayloadKind payloadKind,
        string returnType,
        bool isLastStatement,
        XmlReadPlan? xmlRead = null)
    {
        if (xmlRead?.ModelOrFileType is { } modelType)
        {
            AppendModelOrFileCompletion(builder, resultStyle, payloadKind, returnType, modelType, isLastStatement, xmlRead);
            return;
        }

        if (payloadKind == ResponsePayloadKind.Json && xmlRead is { ByContentType: true, ReadMethod: not null })
        {
            // Offered as JSON and XML: the model read by the response's Content-Type
            builder.AppendLine("if (response.IsSuccessStatusCode && XmlModelSupport.IsXmlMediaType(response.Content.Headers.ContentType?.MediaType))");
            builder.AppendLine("{");
            if (resultStyle == TypedClientResultStyleType.Result)
            {
                builder.AppendLine(4, "var xmlBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);");
                builder.AppendLine(4, $"return new EndpointResponse<{returnType}>(true, response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), xmlBytes.Length > 0 ? {xmlRead.ReadMethod!}(XmlModelSupport.ParseContent(xmlBytes)) : null, ReadHeaders(response));");
            }
            else
            {
                builder.AppendLine(4, $"return {xmlRead.ReadMethod!}(XmlModelSupport.ParseContent(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false)));");
            }

            builder.AppendLine("}");
            builder.AppendLine();
            AppendResponseCompletion(builder, resultStyle, ResponsePayloadKind.Json, returnType, isLastStatement, xmlRead: null);
            return;
        }

        if (payloadKind == ResponsePayloadKind.Xml && xmlRead?.ReadMethod is not null)
        {
            // The model read from the XML body by its generated XML mapping - from the bytes, so the document's own
            // encoding declaration is honoured (a string read would decode by the charset parameter or UTF-8 only)
            if (resultStyle == TypedClientResultStyleType.Result)
            {
                builder.AppendLine("var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);");
                builder.AppendLine("var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
                AppendStatement(builder, $"return new EndpointResponse<{returnType}>(response.IsSuccessStatusCode, response.StatusCode, content, response.IsSuccessStatusCode && bytes.Length > 0 ? {xmlRead.ReadMethod!}(XmlModelSupport.ParseContent(bytes)) : null, ReadHeaders(response));", isLastStatement);
                return;
            }

            builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);");
            AppendStatement(builder, $"return {xmlRead.ReadMethod!}(XmlModelSupport.ParseContent(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false)));", isLastStatement);
            return;
        }

        if (resultStyle == TypedClientResultStyleType.Result)
        {
            var call = payloadKind switch
            {
                ResponsePayloadKind.Json => $"return await BuildJsonResponseAsync<{returnType}>(response, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);",
                ResponsePayloadKind.Binary => "return await BuildBinaryResponseAsync(response, cancellationToken).ConfigureAwait(false);",
                ResponsePayloadKind.Text => "return await BuildTextResponseAsync(response, cancellationToken).ConfigureAwait(false);",
                _ => "return await BuildEmptyResponseAsync(response, cancellationToken).ConfigureAwait(false);",
            };

            AppendStatement(builder, call, isLastStatement);
            return;
        }

        if (payloadKind == ResponsePayloadKind.None)
        {
            AppendStatement(builder, "await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);", isLastStatement);
            return;
        }

        builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);");

        if (payloadKind == ResponsePayloadKind.Json)
        {
            // A success without a body (a 204, an empty 200) is null, not a JsonException
            builder.AppendLine("var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
            AppendStatement(builder, $"return json.Length == 0 ? default! : JsonSerializer.Deserialize(json, jsonSerializerOptions.TypeInfo<{returnType}>())!;", isLastStatement);
            return;
        }

        var read = payloadKind switch
        {
            ResponsePayloadKind.Binary => "return await DownloadedFile.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false);",
            _ => "return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);",
        };

        AppendStatement(builder, read, isLastStatement);
    }

    /// <summary>
    /// Emits the completion of a model or a file: the file when the answer's Content-Type is neither JSON nor XML,
    /// else the model, read as it would be alone (by a local function) and wrapped in the <c>ContentOrFile&lt;T&gt;</c>.
    /// </summary>
    private static void AppendModelOrFileCompletion(
        StringBuilder builder,
        TypedClientResultStyleType resultStyle,
        ResponsePayloadKind payloadKind,
        string returnType,
        string modelType,
        bool isLastStatement,
        XmlReadPlan xmlRead)
    {
        var model = new StringBuilder();
        AppendResponseCompletion(model, resultStyle, payloadKind, modelType, isLastStatement: true, xmlRead: xmlRead with { ModelOrFileType = null });

        if (resultStyle == TypedClientResultStyleType.Result)
        {
            builder.AppendLine("if (response.IsSuccessStatusCode && DownloadedFile.IsFileMediaType(response.Content.Headers.ContentType?.MediaType))");
            builder.AppendLine("{");
            builder.AppendLine(4, "var file = await DownloadedFile.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false);");
            builder.AppendLine(4, $"return new EndpointResponse<{returnType}>(true, response.StatusCode, string.Empty, {returnType}.FromFile(file), ReadHeaders(response));");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine("var modelResponse = await ReadContentAsync().ConfigureAwait(false);");
            builder.AppendLine($"return new EndpointResponse<{returnType}>(modelResponse.IsSuccess, modelResponse.StatusCode, modelResponse.Content, modelResponse.IsSuccess && modelResponse.ContentObject is {modelType} content ? {returnType}.FromContent(content) : null, ReadHeaders(response));");
            builder.AppendLine();
            builder.AppendLine($"async Task<EndpointResponse<{modelType}>> ReadContentAsync()");
        }
        else
        {
            builder.AppendLine("await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);");
            builder.AppendLine("if (DownloadedFile.IsFileMediaType(response.Content.Headers.ContentType?.MediaType))");
            builder.AppendLine("{");
            builder.AppendLine(4, $"return {returnType}.FromFile(await DownloadedFile.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false));");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine($"return {returnType}.FromContent(await ReadContentAsync().ConfigureAwait(false));");
            builder.AppendLine();
            builder.AppendLine($"async Task<{modelType}> ReadContentAsync()");
        }

        // The Throw style has checked the status already
        var modelBody = model.ToString();
        if (resultStyle != TypedClientResultStyleType.Result)
        {
            modelBody = modelBody.Replace("await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);" + Environment.NewLine, string.Empty);
        }

        builder.AppendLine("{");
        foreach (var line in modelBody.Split('\n'))
        {
            var text = line.TrimEnd('\r');
            if (text.Length == 0)
            {
                builder.AppendLine();
            }
            else
            {
                builder.AppendLine(4, text);
            }
        }

        AppendStatement(builder, "}", isLastStatement);
    }

    private static void AppendStatement(
        StringBuilder builder,
        string statement,
        bool isLastStatement)
    {
        if (isLastStatement)
        {
            builder.Append(statement);
        }
        else
        {
            builder.AppendLine(statement);
        }
    }

    /// <summary>
    /// Creates the EnsureSuccessAsync helper method that reads error response body before throwing.
    /// This replaces the standard EnsureSuccessStatusCode() to preserve error details: it throws the generated
    /// exception (<see cref="ApiExceptionExtractor"/>), which carries the status, the body and the headers.
    /// </summary>
    private static MethodParameters CreateEnsureSuccessMethod(
        string exceptionTypeName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("if (response.IsSuccessStatusCode)");
        sb.AppendLine("{");
        sb.AppendLine(4, "return;");
        sb.AppendLine("}");
        sb.AppendLine();

        // At most 4 KB of the error body in the message: a proxy's 10 MB HTML page is not read whole into it
        sb.AppendLine("const int MaxErrorBytes = 4096;");
        sb.AppendLine("using var errorStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("var errorBuffer = new byte[MaxErrorBytes + 1];");
        sb.AppendLine("var errorLength = 0;");
        sb.AppendLine("int errorRead;");
        sb.AppendLine("while (errorLength < errorBuffer.Length && (errorRead = await errorStream.ReadAsync(errorBuffer.AsMemory(errorLength), cancellationToken).ConfigureAwait(false)) > 0)");
        sb.AppendLine("{");
        sb.AppendLine(4, "errorLength += errorRead;");
        sb.AppendLine("}");
        sb.AppendLine();

        // The text is in the charset the Content-Type names (a proxy, an older server); UTF-8 without one or for a
        // name the runtime does not know
        sb.AppendLine("var errorEncoding = System.Text.Encoding.UTF8;");
        sb.AppendLine("if (response.Content.Headers.ContentType?.CharSet is { Length: > 0 } errorCharset)");
        sb.AppendLine("{");
        sb.AppendLine(4, "try");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "errorEncoding = System.Text.Encoding.GetEncoding(errorCharset.Trim('\"'));");
        sb.AppendLine(4, "}");
        sb.AppendLine(4, "catch (System.ArgumentException)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "errorEncoding = System.Text.Encoding.UTF8;");
        sb.AppendLine(4, "}");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("var errorContent = errorLength > MaxErrorBytes");
        sb.AppendLine(4, "? errorEncoding.GetString(errorBuffer, 0, MaxErrorBytes) + \"…\"");
        sb.AppendLine(4, ": errorEncoding.GetString(errorBuffer, 0, errorLength);");
        sb.Append($"throw {exceptionTypeName}.From(response, errorContent);");

        return new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PrivateStaticAsync,
            ReturnGenericTypeName: null,
            ReturnTypeName: "System.Threading.Tasks.Task",
            Name: "EnsureSuccessAsync",
            Parameters:
            [
                new ParameterBaseParameters(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "HttpResponseMessage",
                    IsNullableType: false,
                    IsReferenceType: true,
                    Name: "response",
                    DefaultValue: null),
                new ParameterBaseParameters(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "CancellationToken",
                    IsNullableType: false,
                    IsReferenceType: false,
                    Name: "cancellationToken",
                    DefaultValue: null),
            ],
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: sb.ToString());
    }

    /// <summary>
    /// Creates the helper methods used exclusively by the Result style to wrap a response in an
    /// <c>EndpointResponse</c> envelope instead of throwing on a non-success status code.
    /// </summary>
    /// <remarks>
    /// Transport-level failures (DNS, TLS, connection reset, cancellation) still surface as exceptions
    /// from <c>HttpClient</c> itself - only status-code failures are converted into an envelope.
    /// </remarks>
    private static List<MethodParameters> CreateResultStyleHelperMethods(
        bool hasFileDownload)
    {
        var helpers = new List<MethodParameters>
        {
            CreateReadHeadersMethod(),
            CreateBuildEmptyResponseMethod(),
            CreateBuildJsonResponseMethod(),
        };

        if (hasFileDownload)
        {
            helpers.Add(CreateBuildBinaryResponseMethod());
        }

        helpers.Add(CreateBuildTextResponseMethod());
        helpers.Add(CreateBuildLocationResponseMethod());
        return helpers;
    }

    private static MethodParameters CreateReadHeadersMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("var headers = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine();
        sb.AppendLine("foreach (var header in response.Headers)");
        sb.AppendLine("{");
        sb.AppendLine(4, "headers[header.Key] = header.Value;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("foreach (var header in response.Content.Headers)");
        sb.AppendLine("{");
        sb.AppendLine(4, "headers[header.Key] = header.Value;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.Append("return headers;");

        return new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PrivateStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: "IReadOnlyDictionary<string, IEnumerable<string>>",
            Name: "ReadHeaders",
            Parameters: [CreateResponseParameter()],
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: sb.ToString());
    }

    private static MethodParameters CreateBuildEmptyResponseMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine("return new EndpointResponse(");
        sb.AppendLine(4, "response.IsSuccessStatusCode,");
        sb.AppendLine(4, "response.StatusCode,");
        sb.AppendLine(4, "content,");
        sb.AppendLine(4, "contentObject: null,");
        sb.Append(4, "ReadHeaders(response));");

        return CreateBuildResponseMethod("BuildEmptyResponseAsync", "EndpointResponse", genericName: null, includeSerializerOptions: false, sb.ToString());
    }

    private static MethodParameters CreateBuildJsonResponseMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine("object? contentObject = null;");
        sb.AppendLine("var isSuccess = response.IsSuccessStatusCode;");
        sb.AppendLine("if (isSuccess && !string.IsNullOrEmpty(content))");
        sb.AppendLine("{");
        sb.AppendLine(4, "// A success whose body is no JSON (a proxy's HTML page) is no success - this style never throws for it");
        sb.AppendLine(4, "try");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "contentObject = JsonSerializer.Deserialize(content, jsonSerializerOptions.TypeInfo<T>());");
        sb.AppendLine(4, "}");
        sb.AppendLine(4, "catch (JsonException)");
        sb.AppendLine(4, "{");
        sb.AppendLine(8, "isSuccess = false;");
        sb.AppendLine(4, "}");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("return new EndpointResponse<T>(");
        sb.AppendLine(4, "isSuccess,");
        sb.AppendLine(4, "response.StatusCode,");
        sb.AppendLine(4, "content,");
        sb.AppendLine(4, "contentObject,");
        sb.Append(4, "ReadHeaders(response));");

        return CreateBuildResponseMethod("BuildJsonResponseAsync<T>", "EndpointResponse<T>", genericName: "T", includeSerializerOptions: true, sb.ToString());
    }

    private static MethodParameters CreateBuildBinaryResponseMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// The file on success; on an error status the error body, as text, in Content.");
        sb.AppendLine("if (response.IsSuccessStatusCode)");
        sb.AppendLine("{");
        sb.AppendLine(4, "var file = await DownloadedFile.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine(4, "return new EndpointResponse<DownloadedFile>(true, response.StatusCode, string.Empty, file, ReadHeaders(response));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        sb.Append("return new EndpointResponse<DownloadedFile>(false, response.StatusCode, errorBody, null, ReadHeaders(response));");

        return CreateBuildResponseMethod("BuildBinaryResponseAsync", "EndpointResponse<DownloadedFile>", genericName: null, includeSerializerOptions: false, sb.ToString());
    }

    // The open file on success - the envelope's content owns the response; on an error status the error body, as text,
    // in Content, and the response disposed
    private static MethodParameters CreateBuildStreamedFileResponseMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("if (response.IsSuccessStatusCode)");
        sb.AppendLine("{");
        sb.AppendLine(4, "var file = await StreamedFile.FromHttpResponseAsync(response, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine(4, "return new EndpointResponse<StreamedFile>(true, response.StatusCode, string.Empty, file, ReadHeaders(response));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("using (response)");
        sb.AppendLine("{");
        sb.AppendLine(4, "var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine(4, "return new EndpointResponse<StreamedFile>(false, response.StatusCode, errorBody, null, ReadHeaders(response));");
        sb.Append('}');

        return CreateBuildResponseMethod("BuildStreamedFileResponseAsync", "EndpointResponse<StreamedFile>", genericName: null, includeSerializerOptions: false, sb.ToString());
    }

    private static MethodParameters CreateBuildTextResponseMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine("return new EndpointResponse<string>(");
        sb.AppendLine(4, "response.IsSuccessStatusCode,");
        sb.AppendLine(4, "response.StatusCode,");
        sb.AppendLine(4, "content,");
        sb.AppendLine(4, "response.IsSuccessStatusCode ? content : null,");
        sb.Append(4, "ReadHeaders(response));");

        return CreateBuildResponseMethod("BuildTextResponseAsync", "EndpointResponse<string>", genericName: null, includeSerializerOptions: false, sb.ToString());
    }

    private static MethodParameters CreateBuildLocationResponseMethod()
    {
        var sb = new StringBuilder();
        sb.AppendLine("var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine();
        sb.AppendLine("return new EndpointResponse<Uri>(");
        sb.AppendLine(4, "response.IsSuccessStatusCode,");
        sb.AppendLine(4, "response.StatusCode,");
        sb.AppendLine(4, "content,");
        sb.AppendLine(4, "response.Headers.Location,");
        sb.Append(4, "ReadHeaders(response));");

        return CreateBuildResponseMethod("BuildLocationResponseAsync", "EndpointResponse<Uri>", genericName: null, includeSerializerOptions: false, sb.ToString());
    }

    private static MethodParameters CreateBuildResponseMethod(
        string name,
        string envelopeTypeName,
        string? genericName,
        bool includeSerializerOptions,
        string content)
    {
        _ = genericName;

        var parameters = new List<ParameterBaseParameters> { CreateResponseParameter() };

        if (includeSerializerOptions)
        {
            parameters.Add(
                new ParameterBaseParameters(
                    Attributes: null,
                    GenericTypeName: null,
                    IsGenericListType: false,
                    TypeName: "JsonSerializerOptions",
                    IsNullableType: false,
                    IsReferenceType: true,
                    Name: "jsonSerializerOptions",
                    DefaultValue: null));
        }

        parameters.Add(CreateCancellationTokenParameter());

        return new MethodParameters(
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PrivateStaticAsync,
            ReturnGenericTypeName: "System.Threading.Tasks.Task",
            ReturnTypeName: envelopeTypeName,
            Name: name,
            Parameters: parameters,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: content);
    }

    private static ParameterBaseParameters CreateResponseParameter()
        => new(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "HttpResponseMessage",
            IsNullableType: false,
            IsReferenceType: true,
            Name: "response",
            DefaultValue: null);

    private static ParameterBaseParameters CreateCancellationTokenParameter()
        => new(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: "CancellationToken",
            IsNullableType: false,
            IsReferenceType: false,
            Name: "cancellationToken",
            DefaultValue: null);
}