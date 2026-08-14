namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Scans an OpenAPI document for `string + format: uuid` properties and path
/// parameters whose names suggest entity identifiers, then emits a single
/// <c>types/BrandedIds.ts</c> file containing branded type aliases. Each brand
/// makes the underlying primitive nominal at compile time so a <c>UserId</c>
/// can't be passed where a <c>PetId</c> is expected.
/// </summary>
/// <remarks>
/// Branding is deliberately narrow — only <c>string + format: uuid</c> qualifies.
/// Integer IDs and unformatted strings don't pass through the filter because the
/// signal is too weak; branding them indiscriminately turns into noise.
/// </remarks>
public static class TypeScriptBrandedIdExtractor
{
    /// <summary>
    /// Scans the OpenAPI document and returns the set of brand names that should
    /// be emitted. The names are deduplicated and sorted alphabetically.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document to scan.</param>
    /// <returns>A sorted list of brand names (e.g. "PetId", "UserId").</returns>
    public static IReadOnlyList<string> CollectBrandNames(
        OpenApiDocument openApiDoc)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        var brands = new SortedSet<string>(StringComparer.Ordinal);

        // Pass 1: property-level brands across all named schemas.
        if (openApiDoc.Components?.Schemas is not null)
        {
            foreach (var schema in openApiDoc.Components.Schemas)
            {
                CollectFromSchema(brands, schema.Key, schema.Value);
            }
        }

        // Pass 2: path parameter brands. Operation-level params and path-level
        // params both flow through — they share the same naming conventions.
        if (openApiDoc.Paths is not null)
        {
            foreach (var path in openApiDoc.Paths)
            {
                CollectFromPath(brands, path.Key, path.Value);
            }
        }

        return [.. brands];
    }

    /// <summary>
    /// Resolves the brand name for a property on a named schema. Returns null when
    /// the property does not qualify for branding (wrong format, no entity hint).
    /// </summary>
    /// <param name="schemaName">The owning schema's name (used for bare-<c>id</c> derivation).</param>
    /// <param name="propertyName">The property's name as it appears in the spec.</param>
    /// <param name="propertySchema">The property's schema (must be string + format: uuid to qualify).</param>
    public static string? ResolvePropertyBrand(
        string schemaName,
        string propertyName,
        IOpenApiSchema? propertySchema)
    {
        if (!IsUuidString(propertySchema))
        {
            return null;
        }

        return DeriveBrandFromIdentifier(propertyName, fallbackEntity: schemaName);
    }

    /// <summary>
    /// Resolves the brand name for a path parameter. Returns null when the
    /// parameter does not qualify (wrong format, no entity hint, no usable
    /// parent segment for the bare-<c>id</c> case).
    /// </summary>
    /// <param name="path">The full path template (e.g. <c>/users/{id}</c>).</param>
    /// <param name="parameterName">The path parameter's name.</param>
    /// <param name="parameterSchema">The parameter's schema.</param>
    public static string? ResolveParamBrand(
        string path,
        string parameterName,
        IOpenApiSchema? parameterSchema)
    {
        if (!IsUuidString(parameterSchema))
        {
            return null;
        }

        var fallbackEntity = DeriveEntityFromParentSegment(path, parameterName);
        return DeriveBrandFromIdentifier(parameterName, fallbackEntity);
    }

    /// <summary>
    /// Emits the <c>types/BrandedIds.ts</c> file content. Returns null when no
    /// brands were detected — callers should skip the write-in that case.
    /// </summary>
    /// <param name="brandNames">The brands to emit, expected sorted.</param>
    /// <param name="headerContent">Optional auto-generated file header.</param>
    public static string? Generate(
        IReadOnlyList<string> brandNames,
        string? headerContent)
    {
        ArgumentNullException.ThrowIfNull(brandNames);

        if (brandNames.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(headerContent))
        {
            sb.Append(headerContent);
        }

        sb.AppendLine("/**");
        sb.AppendLine(" * Branded ID types. The intersection with `{ readonly __brand: '...' }` makes");
        sb.AppendLine(" * each ID nominal at compile time, so `getPet(userId)` is a type error even");
        sb.AppendLine(" * though both IDs are strings at runtime. Cast with `value as XxxId` when");
        sb.AppendLine(" * crossing a spec boundary you can't otherwise type — e.g. raw form input.");
        sb.AppendLine(" */");

        foreach (var brand in brandNames)
        {
            sb.Append("export type ").Append(brand).Append(" = string & { readonly __brand: '").Append(brand).AppendLine("' };");
        }

        return sb.ToString();
    }

    private static void CollectFromSchema(
        SortedSet<string> brands,
        string schemaName,
        IOpenApiSchema schema)
    {
        if (schema is OpenApiSchemaReference || schema is not OpenApiSchema actual)
        {
            return;
        }

        if (actual.Properties is null)
        {
            return;
        }

        foreach (var prop in actual.Properties)
        {
            var brand = ResolvePropertyBrand(schemaName, prop.Key, prop.Value);
            if (brand is not null)
            {
                brands.Add(brand);
            }
        }
    }

    private static void CollectFromPath(
        SortedSet<string> brands,
        string path,
        IOpenApiPathItem pathItem)
    {
        if (pathItem.Parameters is not null)
        {
            foreach (var param in pathItem.Parameters)
            {
                AddParamBrand(brands, path, param);
            }
        }

        if (pathItem.Operations is null)
        {
            return;
        }

        foreach (var op in pathItem.Operations.Values)
        {
            if (op.Parameters is null)
            {
                continue;
            }

            foreach (var param in op.Parameters)
            {
                AddParamBrand(brands, path, param);
            }
        }
    }

    private static void AddParamBrand(
        SortedSet<string> brands,
        string path,
        IOpenApiParameter parameter)
    {
        if (parameter.In != ParameterLocation.Path)
        {
            return;
        }

        var name = parameter.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var brand = ResolveParamBrand(path, name, parameter.Schema);
        if (brand is not null)
        {
            brands.Add(brand);
        }
    }

    private static bool IsUuidString(IOpenApiSchema? schema)
    {
        if (schema is not OpenApiSchema actual)
        {
            return false;
        }

        if (actual.Type?.HasFlag(JsonSchemaType.String) != true)
        {
            return false;
        }

        return string.Equals(actual.Format, "uuid", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Derives a <c>&lt;Entity&gt;Id</c> brand name from an identifier. Handles both
    /// the camel/PascalCase <c>&lt;entity&gt;Id</c> shape ("petId" → "PetId") and the
    /// bare <c>id</c> case using the supplied fallback entity name.
    /// </summary>
    private static string? DeriveBrandFromIdentifier(
        string identifier,
        string? fallbackEntity)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        // Bare `id` / `Id` — use the fallback entity (schema name or parent path
        // segment). Without a fallback the brand would be "Id" which is useless.
        if (string.Equals(identifier, "id", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(fallbackEntity))
            {
                return null;
            }

            return ToPascalCase(fallbackEntity) + "Id";
        }

        // `<entity>Id` or `<entity>ID` shapes. Strip the trailing Id, PascalCase the rest.
        // Names that don't end in Id don't qualify — no signal.
        if (identifier.EndsWith("Id", StringComparison.Ordinal))
        {
            var entity = identifier[..^"Id".Length];
            if (string.IsNullOrEmpty(entity))
            {
                return null;
            }

            return ToPascalCase(entity) + "Id";
        }

        if (identifier.EndsWith("ID", StringComparison.Ordinal))
        {
            var entity = identifier[..^"ID".Length];
            if (string.IsNullOrEmpty(entity))
            {
                return null;
            }

            return ToPascalCase(entity) + "Id";
        }

        return null;
    }

    /// <summary>
    /// Picks the parent segment from a path template to brand bare-<c>{id}</c> path
    /// parameters. <c>/users/{id}</c> → "User"; <c>/pets/{petId}/owner</c> → null
    /// (no preceding segment relative to the param), so the param-name rule applies.
    /// </summary>
    private static string? DeriveEntityFromParentSegment(
        string path,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // Only the bare-id case uses the parent segment. Other names carry their
        // own entity hint and don't need this fallback.
        if (!string.Equals(parameterName, "id", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var paramToken = "{" + parameterName + "}";
        var idx = Array.FindIndex(segments, s => s.Equals(paramToken, StringComparison.OrdinalIgnoreCase));
        if (idx <= 0)
        {
            return null;
        }

        var parent = segments[idx - 1];
        if (string.IsNullOrEmpty(parent) || parent.StartsWith('{'))
        {
            return null;
        }

        // Strip a trailing `s` so the plural collection name becomes a singular entity.
        // Crude but matches the convention in every existing scenario (`/users/{id}` → User).
        if (parent.EndsWith("s", StringComparison.OrdinalIgnoreCase) && parent.Length > 1)
        {
            parent = parent[..^1];
        }

        return parent;
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (char.IsUpper(value[0]))
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}