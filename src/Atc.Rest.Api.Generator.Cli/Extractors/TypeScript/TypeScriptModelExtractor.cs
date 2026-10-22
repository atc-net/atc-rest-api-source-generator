namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Extracts OpenAPI schema definitions and converts them to TypeScript interface parameters.
/// </summary>
public static class TypeScriptModelExtractor
{
    /// <summary>Suffix appended to the writable variant of a schema (e.g.
    /// <c>UserWritable</c>). Chosen over <c>UserRequest</c> to avoid colliding with
    /// explicit <c>*Request</c> schemas that real specs commonly declare.</summary>
    internal const string WritableSuffix = "Writable";

    /// <summary>
    /// Extracts all model definitions from OpenAPI document components.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="config">TypeScript client generation configuration.</param>
    /// <param name="enumNames">Names of types that are enums (for correct import paths).</param>
    /// <returns>List of (TypeName, InterfaceParameters) tuples for each model.</returns>
    public static List<(string Name, TypeScriptInterfaceParameters Parameters)> Extract(
        OpenApiDocument openApiDoc,
        TypeScriptClientConfig config,
        HashSet<string>? enumNames = null)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);
        ArgumentNullException.ThrowIfNull(config);

        var results = new List<(string Name, TypeScriptInterfaceParameters Parameters)>();

        if (openApiDoc.Components?.Schemas is null || openApiDoc.Components.Schemas.Count == 0)
        {
            return results;
        }

        var headerContent = config.GenerateFileHeaders ? TypeScriptHeaderBuilder.FileHeader : null;
        var discriminators = TypeScriptDiscriminatorLiterals.Collect(openApiDoc);

        foreach (var schema in openApiDoc.Components.Schemas)
        {
            var originalSchemaName = schema.Key;
            var schemaValue = schema.Value;

            // Skip schema references
            if (schemaValue is OpenApiSchemaReference)
            {
                continue;
            }

            // Skip deprecated schemas if not including them
            if (!config.IncludeDeprecated && schemaValue is OpenApiSchema { Deprecated: true })
            {
                continue;
            }

            if (schemaValue is not OpenApiSchema actualSchema)
            {
                continue;
            }

            // Skip array and dictionary schemas - they are handled by ExtractArrayTypeAliases
            if (actualSchema.IsOfType(JsonSchemaType.Array) || IsRecordAliasSchema(actualSchema))
            {
                continue;
            }

            // Skip enum schemas - handled by TypeScriptEnumExtractor
            if (actualSchema.IsOfType(JsonSchemaType.String) && actualSchema.Enum is { Count: > 0 })
            {
                continue;
            }

            // Only process object schemas - including composed ones that leave `type` out
            // (allOf: [$ref, { properties }] is how inheritance is usually written).
            if (!IsObjectSchema(actualSchema))
            {
                continue;
            }

            var hasReadOnly = SchemaHasMarker(actualSchema, readOnly: true);
            var hasWriteOnly = SchemaHasMarker(actualSchema, readOnly: false);

            if (hasReadOnly || hasWriteOnly)
            {
                // Response variant keeps the schema's canonical name; writeOnly props drop.
                var responseParams = ExtractInterfaceFromSchema(originalSchemaName, actualSchema, headerContent, enumNames, config.NamingStrategy, config.ConvertDates, config.MutableModels, config.BrandedIds, SchemaVariant.Response, discriminators);
                if (responseParams is not null)
                {
                    results.Add((originalSchemaName, responseParams));
                }

                // Writable sibling gets the WritableSuffix; readOnly props drop. Client
                // method signatures opt into this name when emitting a request body whose
                // schema is in the writable set (see TypeScriptClientExtractor).
                var writableName = originalSchemaName + WritableSuffix;
                var requestParams = ExtractInterfaceFromSchema(writableName, actualSchema, headerContent, enumNames, config.NamingStrategy, config.ConvertDates, config.MutableModels, config.BrandedIds, SchemaVariant.Request, discriminators);
                if (requestParams is not null)
                {
                    results.Add((writableName, requestParams));
                }
            }
            else
            {
                var interfaceParams = ExtractInterfaceFromSchema(originalSchemaName, actualSchema, headerContent, enumNames, config.NamingStrategy, config.ConvertDates, config.MutableModels, config.BrandedIds, SchemaVariant.Combined, discriminators);
                if (interfaceParams is not null)
                {
                    results.Add((originalSchemaName, interfaceParams));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Returns the set of schema names whose Writable sibling exists. Client and hook
    /// extractors consult this set to decide whether to use <c>&lt;Name&gt;Writable</c>
    /// for a request body's parameter type.
    /// </summary>
    public static HashSet<string> CollectSchemasWithWritableVariant(
        OpenApiDocument openApiDoc)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        var result = new HashSet<string>(StringComparer.Ordinal);
        if (openApiDoc.Components?.Schemas is null)
        {
            return result;
        }

        foreach (var (name, schemaValue) in openApiDoc.Components.Schemas)
        {
            if (schemaValue is not OpenApiSchema schema)
            {
                continue;
            }

            if (SchemaHasMarker(schema, readOnly: true) || SchemaHasMarker(schema, readOnly: false))
            {
                result.Add(name);
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts top-level <c>oneOf</c> / <c>anyOf</c> schemas as TypeScript union type
    /// aliases (e.g. <c>export type PaymentMethod = CreditCard | BankTransfer | PayPal;</c>).
    /// Without this, every consumer would have to either cast the leaf type or hand-write
    /// the union — and TypeScript wouldn't narrow on a discriminator property.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="config">TypeScript client generation configuration.</param>
    /// <returns>List of (TypeName, FileContent) tuples for each union type alias.</returns>
    public static List<(string Name, string Content)> ExtractUnionTypeAliases(
        OpenApiDocument openApiDoc,
        TypeScriptClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);
        ArgumentNullException.ThrowIfNull(config);

        var results = new List<(string Name, string Content)>();
        var headerContent = config.GenerateFileHeaders ? TypeScriptHeaderBuilder.FileHeader : null;

        foreach (var (schemaName, leafNames, _) in CollectUnionAliases(openApiDoc, config))
        {
            var sb = new StringBuilder();
            if (headerContent is not null)
            {
                sb.Append(headerContent);
            }

            // One import per leaf so the union type alias compiles even when nothing else
            // in the file references those names.
            foreach (var leaf in leafNames)
            {
                sb.Append("import type { ").Append(leaf).Append(" } from './").Append(leaf).AppendLine("';");
            }

            sb.AppendLine();
            sb.Append("export type ").Append(schemaName).Append(" = ").Append(string.Join(" | ", leafNames)).AppendLine(";");

            results.Add((schemaName, sb.ToString()));
        }

        return results;
    }

    /// <summary>
    /// Extracts array wrapper schemas as TypeScript type aliases (e.g., export type Accounts = Account[];).
    /// These are named array schemas in components/schemas that wrap a $ref item type.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="config">TypeScript client generation configuration.</param>
    /// <returns>List of (TypeName, FileContent) tuples for each array type alias.</returns>
    public static List<(string Name, string Content)> ExtractArrayTypeAliases(
        OpenApiDocument openApiDoc,
        TypeScriptClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);
        ArgumentNullException.ThrowIfNull(config);

        var results = new List<(string Name, string Content)>();

        if (openApiDoc.Components?.Schemas is null || openApiDoc.Components.Schemas.Count == 0)
        {
            return results;
        }

        var headerContent = config.GenerateFileHeaders ? TypeScriptHeaderBuilder.FileHeader : null;

        foreach (var schema in openApiDoc.Components.Schemas)
        {
            var schemaName = schema.Key;
            var schemaValue = schema.Value;

            if (schemaValue is not OpenApiSchema actualSchema)
            {
                continue;
            }

            var isScalarAlias = IsScalarAliasSchema(actualSchema);
            var isRecordAlias = IsRecordAliasSchema(actualSchema);
            if (!actualSchema.IsOfType(JsonSchemaType.Array) && !isScalarAlias && !isRecordAlias)
            {
                continue;
            }

            // Skip deprecated schemas if not including them
            if (!config.IncludeDeprecated && actualSchema.Deprecated)
            {
                continue;
            }

            var sb = new StringBuilder();

            if (headerContent is not null)
            {
                sb.Append(headerContent);
            }

            // A component that only names a primitive (UserId: string/uuid) or a numeric enum is
            // referenced by name from other models, so it needs its own alias file.
            if (isScalarAlias)
            {
                sb.Append("export type ").Append(schemaName).Append(" = ").Append(GetScalarAliasType(actualSchema)).AppendLine(";");
                results.Add((schemaName, sb.ToString()));
                continue;
            }

            // A dictionary (additionalProperties, no properties of its own) is a Record: as an interface it
            // had no members, and every key was lost to the type.
            if (isRecordAlias)
            {
                if (actualSchema.AdditionalProperties is OpenApiSchemaReference valueRef &&
                    valueRef.ComponentName is { } valueName)
                {
                    sb.Append("import type { ").Append(valueName).Append(" } from './").Append(valueName).AppendLine("';");
                    sb.AppendLine();
                }

                sb.Append("export type ").Append(schemaName).Append(" = ").Append(actualSchema.ToTypeScriptTypeForModel(isRequired: true, config.ConvertDates)).AppendLine(";");
                results.Add((schemaName, sb.ToString()));
                continue;
            }

            // OpenAPI 3.1 / JSON Schema 2020-12: a `type: array` schema with `prefixItems`
            // is a tuple, not an array. Route through ToTypeScriptReturnType so we emit
            // `[number, number]` instead of the lossy `unknown[]`.
            if (actualSchema.HasPrefixItems)
            {
                var tupleType = actualSchema.ToTypeScriptReturnType();
                sb.Append("export type ").Append(schemaName).Append(" = ").Append(tupleType).AppendLine(";");
            }
            else
            {
                var itemType = GetArrayItemTypeName(actualSchema);

                // Add import for the item type if it references another schema
                if (actualSchema.Items is OpenApiSchemaReference)
                {
                    sb.Append("import type { ").Append(itemType).Append(" } from './").Append(itemType).AppendLine("';");
                    sb.AppendLine();
                }

                sb.Append("export type ").Append(schemaName).Append(" = ").Append(itemType).AppendLine("[];");
            }

            results.Add((schemaName, sb.ToString()));
        }

        return results;
    }

    /// <summary>
    /// An object schema: <c>type: object</c>, or no <c>type</c> but <c>allOf</c> / <c>properties</c>
    /// (composition usually leaves the type out). <c>oneOf</c> / <c>anyOf</c> schemas are unions,
    /// emitted by <see cref="ExtractUnionTypeAliases"/>.
    /// </summary>
    internal static bool IsObjectSchema(OpenApiSchema schema)
        => schema.IsOfType(JsonSchemaType.Object) ||
           (schema.Type is null &&
            (schema.AllOf is { Count: > 0 } || schema.Properties is { Count: > 0 }) &&
            schema.OneOf is not { Count: > 0 } &&
            schema.AnyOf is not { Count: > 0 });

    /// <summary>
    /// The top-level <c>oneOf</c> / <c>anyOf</c> schemas emitted as union type aliases, with their
    /// <c>$ref</c> members and the explicit <c>discriminator.propertyName</c> (<see langword="null"/> without a
    /// discriminator block) - shared with the Zod schemas so every alias has a matching <c>z.union</c> /
    /// <c>z.discriminatedUnion</c>. Schemas that combine the union with their own properties stay interfaces.
    /// </summary>
    internal static List<(string Name, List<string> Leaves, string? Discriminator)> CollectUnionAliases(
        OpenApiDocument openApiDoc,
        TypeScriptClientConfig config)
    {
        var aliases = new List<(string Name, List<string> Leaves, string? Discriminator)>();
        if (openApiDoc.Components?.Schemas is null)
        {
            return aliases;
        }

        foreach (var schema in openApiDoc.Components.Schemas)
        {
            if (schema.Value is not OpenApiSchema actualSchema ||
                (!config.IncludeDeprecated && actualSchema.Deprecated))
            {
                continue;
            }

            var hasOwnObject = actualSchema.Type?.HasFlag(JsonSchemaType.Object) == true ||
                               (actualSchema.Properties is not null && actualSchema.Properties.Count > 0);
            if (hasOwnObject)
            {
                continue;
            }

            var leafNames = CollectUnionLeafRefs(actualSchema);
            if (leafNames.Count > 0)
            {
                var discriminator = actualSchema.DiscriminatorPropertyName;
                aliases.Add((schema.Key, leafNames, string.IsNullOrEmpty(discriminator) ? null : discriminator));
            }
        }

        return aliases;
    }

    /// <summary>
    /// A component schema that is only a primitive or a numeric enum: no object, no array, no
    /// composition. A string enum is not one - <see cref="TypeScriptEnumExtractor"/> emits it.
    /// </summary>
    internal static bool IsScalarAliasSchema(OpenApiSchema schema)
    {
        if (schema.Type is not { } type ||
            schema.AllOf is { Count: > 0 } ||
            schema.OneOf is { Count: > 0 } ||
            schema.AnyOf is { Count: > 0 })
        {
            return false;
        }

        var withoutNull = type & ~JsonSchemaType.Null;
        if (withoutNull == JsonSchemaType.String)
        {
            return schema.Enum is not { Count: > 0 };
        }

        return withoutNull is JsonSchemaType.Integer or JsonSchemaType.Number or JsonSchemaType.Boolean;
    }

    /// <summary>
    /// A component schema that is only a dictionary: <c>additionalProperties</c> and no properties, members or
    /// composition of its own. Emitted as <c>Record&lt;string, V&gt;</c>, not as an interface.
    /// </summary>
    internal static bool IsRecordAliasSchema(OpenApiSchema schema)
        => schema.AdditionalProperties is not null &&
           schema.Properties is not { Count: > 0 } &&
           schema.AllOf is not { Count: > 0 } &&
           schema.OneOf is not { Count: > 0 } &&
           schema.AnyOf is not { Count: > 0 } &&
           (schema.Type is null || (schema.Type.Value & ~JsonSchemaType.Null) == JsonSchemaType.Object);

    /// <summary>
    /// The literal values of a numeric enum (<c>enum: [1, 2, 3]</c>), without a <c>null</c> entry.
    /// </summary>
    internal static List<string> GetNumericEnumLiterals(OpenApiSchema schema)
        => schema.Enum is null
            ? []
            : schema.Enum
                .Where(v => v is not null && !JsonNullSentinel.IsJsonNullSentinel(v) && v.GetValueKind() == System.Text.Json.JsonValueKind.Number)
                .Select(v => v.ToJsonString())
                .ToList();

    /// <summary>
    /// Filters a property against the active variant. The default
    /// (<see cref="SchemaVariant.Combined"/>) keeps every property; Response drops
    /// <c>writeOnly</c>; Request drops <c>readOnly</c>.
    /// </summary>
    private static bool ShouldIncludeProperty(
        OpenApiSchema propSchema,
        SchemaVariant variant)
        => variant switch
        {
            SchemaVariant.Response => !propSchema.WriteOnly,
            SchemaVariant.Request => !propSchema.ReadOnly,
            _ => true,
        };

    /// <summary>
    /// Returns <c>true</c> when any direct property of <paramref name="schema"/> carries
    /// the OpenAPI marker selected by <paramref name="readOnly"/> — <c>true</c> checks for
    /// <c>readOnly: true</c>, <c>false</c> for <c>writeOnly: true</c>. Only direct properties
    /// are inspected; deeply nested or inherited markers are out of scope for the split.
    /// </summary>
    private static bool SchemaHasMarker(
        OpenApiSchema schema,
        bool readOnly)
    {
        if (schema.Properties is null)
        {
            return false;
        }

        foreach (var (_, propValue) in schema.Properties)
        {
            if (propValue is OpenApiSchema propSchema &&
                (readOnly ? propSchema.ReadOnly : propSchema.WriteOnly))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> CollectUnionLeafRefs(OpenApiSchema schema)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Visit(IList<IOpenApiSchema>? variants)
        {
            if (variants is null)
            {
                return;
            }

            foreach (var v in variants)
            {
                if (v is not OpenApiSchemaReference variantRef)
                {
                    continue;
                }

                var refName = variantRef.ComponentName;
                if (refName is not null && seen.Add(refName))
                {
                    result.Add(refName);
                }
            }
        }

        Visit(schema.OneOf);
        Visit(schema.AnyOf);

        return result;
    }

    /// <summary>
    /// An inline <c>type: string</c> schema without an <c>enum</c> - the only property a discriminator
    /// literal replaces.
    /// </summary>
    private static bool IsPlainString(IOpenApiSchema schema)
        => schema is OpenApiSchema { Enum: null or { Count: 0 } } plain &&
           plain.Type?.HasFlag(JsonSchemaType.String) == true;

    private static string GetScalarAliasType(OpenApiSchema schema)
    {
        var literals = GetNumericEnumLiterals(schema);
        return literals.Count > 0
            ? string.Join(" | ", literals)
            : schema.ToTypeScriptReturnType();
    }

    private static string GetArrayItemTypeName(OpenApiSchema arraySchema)
    {
        if (arraySchema.Items is OpenApiSchemaReference itemRef)
        {
            return itemRef.ComponentName ?? "unknown";
        }

        if (arraySchema.Items is OpenApiSchema itemSchema)
        {
            return itemSchema.Type.ToTypeScriptTypeName(itemSchema.Format);
        }

        return "unknown";
    }

    /// <summary>
    /// Extracts a single TypeScript interface from an OpenAPI schema. When
    /// <paramref name="variant"/> is <see cref="SchemaVariant.Response"/>, properties
    /// marked <c>writeOnly: true</c> are skipped; for <see cref="SchemaVariant.Request"/>,
    /// <c>readOnly: true</c> properties are skipped instead. <see cref="SchemaVariant.Combined"/>
    /// keeps every property and is the only variant used for schemas without markers.
    /// </summary>
    private static TypeScriptInterfaceParameters? ExtractInterfaceFromSchema(
        string schemaName,
        OpenApiSchema schema,
        string? headerContent,
        HashSet<string>? enumNames,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool mutableModels,
        bool brandedIds,
        SchemaVariant variant,
        TypeScriptDiscriminatorLiterals discriminators)
    {
        // allOf composition: every $ref member becomes an `extends`, the inline members contribute
        // their properties (and required names) as if declared on the schema itself.
        var ownProperties = new List<KeyValuePair<string, IOpenApiSchema>>(schema.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal));
        var required = new HashSet<string>(schema.Required ?? new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var extendsTypeNames = new List<string>();
        if (schema.AllOf is { Count: > 0 })
        {
            foreach (var subSchema in schema.AllOf)
            {
                if (subSchema is OpenApiSchemaReference allOfRef)
                {
                    if (allOfRef.ComponentName is { } refName &&
                        !extendsTypeNames.Contains(refName, StringComparer.Ordinal))
                    {
                        extendsTypeNames.Add(refName);
                    }
                }
                else if (subSchema is OpenApiSchema inlineMember)
                {
                    if (inlineMember.Properties is not null)
                    {
                        ownProperties.AddRange(inlineMember.Properties.Where(p => ownProperties.TrueForAll(o => !string.Equals(o.Key, p.Key, StringComparison.Ordinal))));
                    }

                    if (inlineMember.Required is not null)
                    {
                        required.UnionWith(inlineMember.Required);
                    }
                }
            }
        }

        var properties = ownProperties
            .Where(p => p.Value is not OpenApiSchema propSchema || ShouldIncludeProperty(propSchema, variant))
            .ToList();

        var tsProperties = new List<TypeScriptPropertyParameters>();
        var importTypes = new HashSet<string>(extendsTypeNames, StringComparer.Ordinal);
        var brandImports = new SortedSet<string>(StringComparer.Ordinal);
        var extendsTypeName = extendsTypeNames.Count > 0 ? string.Join(", ", extendsTypeNames) : null;

        // A generic pagination base (emitted as Base<T>) takes the item type from the inline member.
        if (schema.AllOf is { Count: > 0 } &&
            TypeScriptTypeMapper.TryGetGenericPagination(schema.AllOf, out var paginationBase, out var paginationItem, out var paginationItemSchema))
        {
            extendsTypeName = paginationBase + "<" + paginationItem + ">";
            if (paginationItemSchema is not null)
            {
                importTypes.Add(paginationItemSchema);
            }
        }

        // Detect generic pagination pattern: array property with empty items (items: {}) under one of the pagination
        // names (results, items, data, ...) - the same gate the C# generator has. Any other untyped array is
        // unknown[]: a generic model would need a type argument at every reference (TS2314).
        var genericArrayPropName = DetectGenericArrayProperty(properties);
        var isGeneric = genericArrayPropName is not null && Generator.Extractors.SchemaExtractor.IsPaginationBaseSchema(schema);

        // The Writable request variant is the entity's shape (UserId brands, User's discriminator values),
        // not the variant name's (UserWritableId, which the BrandedIds file never declares).
        var entityName = variant == SchemaVariant.Request && schemaName.EndsWith(WritableSuffix, StringComparison.Ordinal)
            ? schemaName[..^WritableSuffix.Length]
            : schemaName;

        foreach (var prop in properties)
        {
            var propName = TypeScriptPropertyNames.Resolve(prop.Key, properties.Select(p => p.Key), namingStrategy);
            var isRequired = required.Contains(prop.Key, StringComparer.Ordinal);

            // Replace empty array type with generic T[] for pagination base schemas
            var tsType = isGeneric && prop.Key == genericArrayPropName
                ? (isRequired ? "T[]" : "T[] | null")
                : prop.Value.ToTypeScriptTypeForModel(isRequired, convertDates);

            // A discriminator is a literal on its variant, so the union narrows on it. Only a plain string
            // property is narrowed: an enum reference is typed already, and a literal is not assignable to
            // a TypeScript enum.
            if (IsPlainString(prop.Value) &&
                tsType.StartsWith("string", StringComparison.Ordinal) &&
                discriminators.TypeOf(entityName, prop.Key) is { } literal)
            {
                tsType = literal + tsType["string".Length..];
            }

            // Branded IDs: a `string + format: uuid` property whose name suggests an
            // entity identifier swaps the inferred `string` for the brand. The cast at
            // the JSON boundary still works because brands ARE strings at runtime —
            // they just won't accept arbitrary other strings statically.
            if (brandedIds && tsType.StartsWith("string", StringComparison.Ordinal))
            {
                var brand = TypeScriptBrandedIdExtractor.ResolvePropertyBrand(entityName, prop.Key, prop.Value);
                if (brand is not null)
                {
                    // Preserve the original suffix (`""` for required, `" | undefined"` for optional)
                    // by substring-replacing only the leading `string` token.
                    tsType = brand + tsType["string".Length..];
                    brandImports.Add(brand);
                }
            }

            // Track referenced types for imports
            TypeScriptSchemaImports.Collect(prop.Value, importTypes);

            // Build description from schema if available. Deprecated properties carry an
            // @deprecated tag so IDE shows the strikethrough; without it the spec author's
            // intent is invisible at the call site.
            JsDocComment? docTags = null;
            if (prop.Value is OpenApiSchema propSchema)
            {
                var propDoc = propSchema.GetSchemaSummary() ?? propSchema.Description;
                var propExample = GetSchemaExampleString(propSchema);
                if (!string.IsNullOrEmpty(propDoc) || propSchema.Deprecated || propExample is not null)
                {
                    docTags = new JsDocComment(
                        description: propDoc,
                        parameters: null,
                        returns: null,
                        isDeprecated: propSchema.Deprecated,
                        deprecatedMessage: null,
                        example: propExample);
                }
            }

            tsProperties.Add(new TypeScriptPropertyParameters(
                DocumentationTags: docTags,
                IsReadonly: !mutableModels,
                TypeAnnotation: tsType,
                IsOptional: !isRequired,
                Name: TypeScriptPropertyNames.ToMemberSyntax(propName),
                DefaultValue: null));
        }

        // additionalProperties beside declared properties: an index signature admits the extra keys. Its type
        // must admit every declared property too, so it is the value type only when each property is
        // required and of that type (`{ default: string; [key: string]: string }`), else unknown.
        if (schema.AdditionalProperties is not null && tsProperties.Count > 0)
        {
            var valueType = schema.AdditionalProperties.ToTypeScriptTypeForModel(isRequired: true, convertDates);
            var indexType = tsProperties.TrueForAll(p => !p.IsOptional && string.Equals(p.TypeAnnotation, valueType, StringComparison.Ordinal))
                ? valueType
                : "unknown";
            if (string.Equals(indexType, valueType, StringComparison.Ordinal))
            {
                TypeScriptSchemaImports.Collect(schema.AdditionalProperties, importTypes);
            }

            tsProperties.Add(new TypeScriptPropertyParameters(
                DocumentationTags: null,
                IsReadonly: !mutableModels,
                TypeAnnotation: indexType,
                IsOptional: false,
                Name: "[key: string]",
                DefaultValue: null));
        }

        // Drop anything collected that the emitted interface never names. TypeScriptSchemaImports.Collect
        // walks the schema graph, so it picks up references the type emitter does not surface —
        // an inline `oneOf` property, for instance, is emitted as `unknown` while its branches are
        // still collected. Importing those produces an interface that fails to compile under
        // noUnusedLocals, or the equivalent ESLint rule.
        var namedTypes = new StringBuilder();
        foreach (var property in tsProperties)
        {
            namedTypes.Append(property.TypeAnnotation).Append(' ');
        }

        var namedTypesText = namedTypes.ToString();
        importTypes.RemoveWhere(typeName =>
            !ContainsTypeReference(namedTypesText, typeName) &&
            !extendsTypeNames.Contains(typeName, StringComparer.Ordinal));

        // Build import statements
        var importStatements = BuildImportStatements(importTypes, schemaName, enumNames);

        // Branded ID imports — one combined import line pointing at types/BrandedIds.
        // Emitted only when this interface actually uses at least one brand.
        if (brandImports.Count > 0)
        {
            var joined = string.Join(", ", brandImports);
            importStatements.Add($"import type {{ {joined} }} from '../types/BrandedIds';");
        }

        // Build JSDoc for the interface itself. The whole schema may be deprecated (i.e.
        // the spec author is signaling "stop using this type") — surface that to consumers.
        JsDocComment? interfaceDocTags = null;
        var schemaDoc = schema.GetSchemaSummary() ?? schema.Description;
        if (!string.IsNullOrEmpty(schemaDoc) || schema.Deprecated)
        {
            interfaceDocTags = new JsDocComment(
                description: schemaDoc,
                parameters: null,
                returns: null,
                isDeprecated: schema.Deprecated,
                deprecatedMessage: null,
                example: null);
        }

        return new TypeScriptInterfaceParameters(
            HeaderContent: headerContent,
            DocumentationTags: interfaceDocTags,
            Modifiers: TypeScriptModifiers.Export,
            TypeName: isGeneric ? $"{schemaName}<T>" : schemaName,
            ExtendsTypeName: extendsTypeName,
            ImportStatements: importStatements.Count > 0 ? importStatements : null,
            Properties: tsProperties.Count > 0 ? tsProperties : null,
            Methods: null);
    }

    /// <summary>
    /// Detects if a schema has a generic array property (type: array with empty items).
    /// This pattern is used by pagination base types (PaginatedResult, PagedResult, etc.).
    /// Returns the property name if found, null otherwise.
    /// </summary>
    private static string? DetectGenericArrayProperty(
        List<KeyValuePair<string, IOpenApiSchema>> properties)
    {
        foreach (var prop in properties)
        {
            // Empty items ({}) or null items means this is a generic array placeholder
            if (prop.Value is OpenApiSchema arraySchema && arraySchema.IsOfType(JsonSchemaType.Array) &&
                (arraySchema.Items is null ||
                 (arraySchema.Items is OpenApiSchema itemSchema &&
                  itemSchema.Type is null &&
                  itemSchema.Properties?.Count is null or 0)))
            {
                return prop.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns a string representation of the schema's example for use in JSDoc @example.
    /// Fallback chain: Examples[0] → Example (scalar on media-type), both as JSON strings.
    /// </summary>
    private static string? GetSchemaExampleString(OpenApiSchema schema)
    {
        // OAS 3.1/3.2 schema-level examples array (JSON Schema keyword)
        if (schema.Examples is { Count: > 0 })
        {
            return schema.Examples[0].ToJsonString();
        }

        // OAS 3.0-style scalar example. The property is deprecated in Microsoft.OpenApi because
        // 3.1 replaced the keyword, but it remains the only place a 3.0 document's `example`
        // surfaces — the reader does not fold it into Examples.
#pragma warning disable CS0618 // Type or member is obsolete
        return schema.Example?.ToJsonString();
#pragma warning restore CS0618
    }

    /// <summary>
    /// Determines whether <paramref name="text"/> names <paramref name="typeName"/> as a whole
    /// identifier rather than as part of a longer one.
    /// </summary>
    /// <remarks>
    /// A plain substring test would treat <c>Resource</c> as used by a property typed
    /// <c>ResourceQuery</c>, which would keep an import that is genuinely unused. Identifier
    /// characters on either side disqualify a match.
    /// </remarks>
    private static bool ContainsTypeReference(
        string text,
        string typeName)
    {
        var index = text.IndexOf(typeName, StringComparison.Ordinal);
        while (index >= 0)
        {
            var precededByIdentifierChar = index > 0 && IsIdentifierChar(text[index - 1]);
            var followedByIndex = index + typeName.Length;
            var followedByIdentifierChar = followedByIndex < text.Length && IsIdentifierChar(text[followedByIndex]);

            if (!precededByIdentifierChar && !followedByIdentifierChar)
            {
                return true;
            }

            index = text.IndexOf(typeName, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsIdentifierChar(char value)
        => char.IsLetterOrDigit(value) || value == '_' || value == '$';

    /// <summary>
    /// Builds import statements for referenced types.
    /// </summary>
    private static List<string> BuildImportStatements(
        HashSet<string> importTypes,
        string currentTypeName,
        HashSet<string>? enumNames)
    {
        var imports = new List<string>();

        foreach (var typeName in importTypes.OrderBy(t => t, StringComparer.Ordinal))
        {
            // Don't import self
            if (typeName.Equals(currentTypeName, StringComparison.Ordinal))
            {
                continue;
            }

            // Use correct relative path based on whether the type is an enum or model
            var importPath = enumNames is not null && enumNames.Contains(typeName)
                ? $"../enums/{typeName}"
                : $"./{typeName}";

            imports.Add($"import type {{ {typeName} }} from '{importPath}';");
        }

        return imports;
    }
}