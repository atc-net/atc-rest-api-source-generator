namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Shared helper methods for working with OpenAPI operations in TypeScript code generation.
/// Used by both TypeScriptClientExtractor and TypeScriptReactQueryHookExtractor.
/// </summary>
public static class TypeScriptOperationHelper
{
    /// <summary>
    /// Merges path-level and operation-level parameters by location.
    /// Resolves parameter references ($ref) to actual parameters.
    /// Operation-level parameters take precedence over path-level parameters with the same name.
    /// </summary>
    public static List<OpenApiParameter> GetMergedParameters(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        string path,
        ParameterLocation location)
    {
        var result = new List<OpenApiParameter>();

        // Resolve operation-level parameters (handles both direct and $ref)
        var operationParams = ResolveParametersByLocation(operation.Parameters, location);
        var operationParamNames = new HashSet<string>(
            operationParams.Select(p => p.Name ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        // Add path-level parameters first (only those not overridden at operation level)
        if (openApiDoc.Paths != null &&
            openApiDoc.Paths.TryGetValue(path, out var pathItemValue) &&
            pathItemValue is OpenApiPathItem pathItem &&
            pathItem.Parameters != null)
        {
            var pathLevelParams = ResolveParametersByLocation(pathItem.Parameters, location);
            foreach (var param in pathLevelParams)
            {
                if (!operationParamNames.Contains(param.Name ?? string.Empty))
                {
                    result.Add(param);
                }
            }
        }

        // Add operation-level parameters
        result.AddRange(operationParams);

        return result;
    }

    /// <summary>
    /// Resolves a list of IOpenApiParameter (which may include $ref references) to concrete
    /// OpenApiParameter objects filtered by location.
    /// </summary>
    public static List<OpenApiParameter> ResolveParametersByLocation(
        IList<IOpenApiParameter>? parameters,
        ParameterLocation location)
    {
        var result = new List<OpenApiParameter>();
        if (parameters == null)
        {
            return result;
        }

        foreach (var paramInterface in parameters)
        {
            var resolved = paramInterface.Resolve();
            if (resolved.Parameter != null && resolved.Parameter.In == location)
            {
                result.Add(resolved.Parameter);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the TypeScript return type for an operation.
    /// </summary>
    public static string GetReturnType(
        OpenApiOperation operation,
        bool isStreaming,
        bool isFileDownload)
    {
        if (isFileDownload)
        {
            return "Blob";
        }

        // Try to get 200 response schema, then 201
        var schema = operation.GetResponseSchema("200") ?? operation.GetResponseSchema("201");
        if (schema == null)
        {
            return isStreaming ? "unknown" : "void";
        }

        return isStreaming
            ? GetStreamingItemType(schema)
            : schema.ToTypeScriptReturnType();
    }

    /// <summary>
    /// Gets the TypeScript type string for a parameter.
    /// </summary>
    public static string GetParameterType(OpenApiParameter param)
    {
        if (param.Schema == null)
        {
            return "string";
        }

        var tsType = param.Schema.ToTypeScriptTypeForModel(isRequired: true);

        // Strip "| null" from query/path parameter types — URL parameters are either
        // present (with a value) or absent (undefined), never null.
        if (tsType.EndsWith(" | null", StringComparison.Ordinal))
        {
            tsType = tsType[..^" | null".Length];
        }

        return tsType;
    }

    /// <summary>
    /// Collects all import types needed by an operation (from response schemas and request body).
    /// </summary>
    public static void CollectImportTypes(
        OpenApiOperation operation,
        HashSet<string> importTypes)
    {
        // From response schemas (200, 201)
        CollectSchemaRefTypes(operation.GetResponseSchema("200"), importTypes);
        CollectSchemaRefTypes(operation.GetResponseSchema("201"), importTypes);

        // From request body
        var (bodySchema, _) = operation.GetRequestBodySchemaWithContentType();
        if (bodySchema != null)
        {
            CollectSchemaRefTypes(bodySchema, importTypes);

            // For multipart form data objects, also collect property types
            if (bodySchema.Properties is { Count: > 0 })
            {
                foreach (var prop in bodySchema.Properties)
                {
                    CollectSchemaRefTypes(prop.Value, importTypes);
                }
            }
        }
    }

    /// <summary>
    /// Collects $ref type names from a schema recursively (one level).
    /// </summary>
    public static void CollectSchemaRefTypes(
        IOpenApiSchema? schema,
        HashSet<string> importTypes)
    {
        if (schema == null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refName = schemaRef.Reference.Id ?? schemaRef.Id;
            if (refName != null)
            {
                importTypes.Add(refName);
            }

            return;
        }

        if (schema is not OpenApiSchema actualSchema)
        {
            return;
        }

        // Handle allOf references
        if (actualSchema.AllOf is { Count: > 0 })
        {
            foreach (var subSchema in actualSchema.AllOf)
            {
                if (subSchema is OpenApiSchemaReference allOfRef)
                {
                    var refName = allOfRef.Reference.Id ?? allOfRef.Id;
                    if (refName != null)
                    {
                        importTypes.Add(refName);
                    }
                }
            }
        }

        // Handle array item references
        if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true && actualSchema.Items is OpenApiSchemaReference itemRef)
        {
            var refName = itemRef.Reference.Id ?? itemRef.Id;
            if (refName != null)
            {
                importTypes.Add(refName);
            }
        }
    }

    /// <summary>
    /// For streaming endpoints, resolves array type aliases (e.g., Accounts -> Account[])
    /// to their item type, since the server yields individual items, not arrays.
    /// </summary>
    public static string GetStreamingItemType(IOpenApiSchema schema)
    {
        // Resolve $ref to actual schema
        var resolved = schema;
        if (schema is OpenApiSchemaReference schemaRef)
        {
            resolved = schemaRef.Target ?? schema;
        }

        // If resolved schema is an array, return the item type
        if (resolved is OpenApiSchema actualSchema &&
            actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
        {
            if (actualSchema.Items is OpenApiSchemaReference itemRef)
            {
                return itemRef.Reference.Id ?? itemRef.Id ?? "unknown";
            }

            if (actualSchema.Items is OpenApiSchema itemSchema)
            {
                return itemSchema.Type.ToTypeScriptTypeName(itemSchema.Format);
            }
        }

        // Not an array — use standard mapping
        return schema.ToTypeScriptReturnType();
    }

    /// <summary>
    /// Builds a TypeScript inline type for query parameters (e.g., { limit?: number; offset?: number }).
    /// </summary>
    public static string BuildQueryTypeInline(
        List<OpenApiParameter> queryParams,
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase)
    {
        var parts = new List<string>();
        foreach (var param in queryParams)
        {
            var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var paramType = GetParameterType(param);
            parts.Add(paramName + "?: " + paramType);
        }

        return "{ " + string.Join("; ", parts) + " }";
    }

    /// <summary>
    /// Builds a path string with template literal interpolation for path parameters.
    /// </summary>
    public static string BuildInterpolatedPath(
        string path,
        List<OpenApiParameter> pathParams,
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase)
    {
        if (pathParams.Count == 0)
        {
            return "'" + path + "'";
        }

        // Replace {paramName} with ${paramName} for template literal
        var interpolated = path;
        foreach (var param in pathParams)
        {
            var tsName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            interpolated = interpolated.Replace(
                "{" + param.Name + "}",
                "${" + tsName + "}",
                StringComparison.Ordinal);
        }

        return "`" + interpolated + "`";
    }
}