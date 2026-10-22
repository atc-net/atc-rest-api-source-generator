// ReSharper disable RedundantSwitchExpressionArms
// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable InvertIf
// ReSharper disable GrammarMistakeInComment

namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension methods for IOpenApiSchema to handle C# type mapping and schema operations.
/// </summary>
[SuppressMessage("", "CA1024:Use properties where appropriate", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "CA1708:Names of 'Members'", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S1144:Remove the unused private method", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S3928:The parameter name 'schema'", Justification = "OK - CLang14 - extension")]
public static class OpenApiSchemaExtensions
{
    /// <summary>
    /// Sanitizes a schema name to be a valid C# identifier.
    /// Every character an identifier cannot hold becomes an underscore ("Foo.Bar" becomes "Foo_Bar",
    /// "Note-Size" becomes "Note_Size"), and a name that starts with a digit gets a leading underscore
    /// ("3DModel" becomes "_3DModel").
    /// </summary>
    /// <param name="schemaName">The original schema name from OpenAPI.</param>
    /// <returns>A sanitized name that is a valid C# identifier.</returns>
    public static string SanitizeSchemaName(string schemaName)
    {
        if (string.IsNullOrEmpty(schemaName))
        {
            return schemaName;
        }

        // A keyword cannot name a type: its first letter is capitalised (class -> Class), and one with no letter to
        // capitalise gets an underscore (__arglist -> ___arglist)
        if (CSharpKeywords.IsKeyword(schemaName))
        {
            return char.IsLetter(schemaName[0])
                ? char.ToUpperInvariant(schemaName[0]) + schemaName.Substring(1)
                : "_" + schemaName;
        }

        var needsChange = char.IsDigit(schemaName[0]);
        foreach (var c in schemaName)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                needsChange = true;
                break;
            }
        }

        if (!needsChange)
        {
            return schemaName;
        }

        var sb = new System.Text.StringBuilder(schemaName.Length + 1);
        if (char.IsDigit(schemaName[0]))
        {
            sb.Append('_');
        }

        foreach (var c in schemaName)
        {
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Resolves a type name, using full namespace qualification if it conflicts with a .NET system type.
    /// Also sanitizes the type name to be a valid C# identifier.
    /// </summary>
    /// <param name="typeName">The original type name from the OpenAPI schema.</param>
    /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
    /// <returns>The resolved type name (fully qualified if conflicting).</returns>
    public static string ResolveTypeName(
        string typeName,
        TypeConflictRegistry? registry = null)
    {
        // First sanitize the name to be a valid C# identifier
        var sanitized = SanitizeSchemaName(typeName);

        // Then check for conflicts with system types
        return registry?.IsConflicting(sanitized) == true
            ? registry.GetFullyQualifiedName(sanitized)
            : sanitized;
    }

    /// <summary>
    /// Determines whether a composition branch exists only to permit <c>null</c>, and so carries no
    /// shape the generated type would otherwise have to represent.
    /// </summary>
    /// <remarks>
    /// Two spellings qualify. The OpenAPI 3.1 one is <c>type: "null"</c> on its own. The 3.0 one is
    /// an untyped or object-typed branch made nullable — 3.0 has no <c>type: "null"</c> to write —
    /// which the parser resolves to the type flags <c>Object | Null</c>. The 3.0 form only qualifies
    /// when it declares nothing else, so a branch that genuinely describes an object still counts as
    /// a union member.
    /// <para>
    /// The non-null part must be <c>object</c> or absent. A nullable scalar branch such as
    /// <c>{type: string, nullable: true}</c> is a real alternative — treating it as a marker would
    /// turn <c>oneOf: [$ref, {type: string}]</c> into the reference alone and silently lose the
    /// string.
    /// </para>
    /// </remarks>
    /// <param name="branch">The composition branch to inspect.</param>
    /// <returns><see langword="true"/> when the branch only adds nullability.</returns>
    internal static bool IsNullOnlyBranch(IOpenApiSchema? branch)
    {
        if (branch is not OpenApiSchema candidate ||
            candidate.Type is not { } type ||
            !type.HasFlag(JsonSchemaType.Null))
        {
            return false;
        }

        var withoutNull = type & ~JsonSchemaType.Null;
        if (withoutNull is not (default(JsonSchemaType) or JsonSchemaType.Object))
        {
            return false;
        }

        return candidate.Properties is null or { Count: 0 } &&
               candidate.Required is null or { Count: 0 } &&
               candidate.Enum is null or { Count: 0 } &&
               candidate.OneOf is null or { Count: 0 } &&
               candidate.AnyOf is null or { Count: 0 } &&
               candidate.AllOf is null or { Count: 0 } &&
               candidate.Items is null &&
               candidate.AdditionalProperties is null &&
               string.IsNullOrEmpty(candidate.Format);
    }

    /// <param name="schema">The OpenAPI schema interface.</param>
    extension(IOpenApiSchema schema)
    {
        /// <summary>
        /// The date format of a string schema, lower case: <c>"date"</c> (a calendar day) or <c>"date-time"</c> (an
        /// instant), whatever the case the specification writes it in (as the type mappers read it);
        /// <see langword="null"/> for any other schema.
        /// </summary>
        public string? DateFormat
            => schema.IsOfType(JsonSchemaType.String) &&
               schema.Format?.ToLowerInvariant() is ("date" or "date-time") and var format
                ? format
                : null;

        /// <summary>
        /// Checks if the schema is a schema reference.
        /// </summary>
        /// <returns>True if the schema is a reference.</returns>
        public bool IsSchemaReference()
            => schema is OpenApiSchemaReference;

        /// <summary>
        /// Gets the OpenAPI type of a schema as a string (e.g., "array", "object", "string").
        /// Handles both OpenApiSchema and OpenApiSchemaReference by resolving references.
        /// Supports OpenAPI 3.1.0 where JsonSchemaType is a flags enum.
        /// </summary>
        /// <returns>The schema type as a lowercase string, or null if unknown.</returns>
        public string? GetSchemaType()
        {
            switch (schema)
            {
                case OpenApiSchemaReference schemaRef:
                    return schemaRef.Target?.GetSchemaType();
                case OpenApiSchema { Type: not null } openApiSchema:
                {
                    var schemaType = openApiSchema.Type.Value;

                    // JsonSchemaType is a flags enum in OpenAPI 3.1.0
                    // Check for specific type flags (in priority order, excluding Null flag)
                    if (schemaType.HasFlag(JsonSchemaType.Array))
                    {
                        return "array";
                    }

                    if (schemaType.HasFlag(JsonSchemaType.Object))
                    {
                        return "object";
                    }

                    if (schemaType.HasFlag(JsonSchemaType.String))
                    {
                        return "string";
                    }

                    if (schemaType.HasFlag(JsonSchemaType.Integer))
                    {
                        return "integer";
                    }

                    if (schemaType.HasFlag(JsonSchemaType.Number))
                    {
                        return "number";
                    }

                    if (schemaType.HasFlag(JsonSchemaType.Boolean))
                    {
                        return "boolean";
                    }

                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the C# type name for a schema without nullable handling.
        /// Returns types like "int", "long", "string", "Pet", "Pet[]", "object".
        /// </summary>
        /// <returns>The C# type name for the schema.</returns>
        public string GetCSharpTypeName()
            => schema switch
            {
                OpenApiSchemaReference schemaRef => SanitizeSchemaName(schemaRef.Reference.Id ?? "object"),
                OpenApiSchema actualSchema when actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true => actualSchema.GetArrayTypeName(),
                OpenApiSchema actualSchema => WidenForBounds(EffectiveType(actualSchema).ToPrimitiveCSharpTypeName(actualSchema.Format) ?? "object", actualSchema),
                _ => "object",
            };

        /// <summary>
        /// Checks if a schema type allows null values.
        /// In OpenAPI 3.1/Microsoft.OpenApi 3.0.x, nullable is indicated by the Null flag in JsonSchemaType.
        /// Also checks the string representation for older formats.
        /// </summary>
        /// <returns>True if the schema allows null values.</returns>
        public bool IsNullable()
        {
            if (schema is not OpenApiSchema openApiSchema)
            {
                return false;
            }

            // `nullable: true` is an OpenAPI 3.0 keyword. Microsoft.OpenApi up to 3.7.0 still
            // honoured it inside a 3.1 document by folding it into the Null type flag; 3.10.2
            // correctly stopped, because 3.1 replaced it with `type: [x, 'null']` — the keyword
            // lands in UnrecognizedKeywords instead.
            //
            // The 3.1 spec is on the library's side, but silently flipping a consumer's generated
            // model from `string?` to `string` on a package bump is a breaking change they never
            // asked for and would not notice until it failed at runtime. So the keyword is still
            // honoured wherever it appears, and ATC_API_SCH022 tells the author to migrate.
            if (openApiSchema.UnrecognizedKeywords is { } unrecognized &&
                unrecognized.TryGetValue("nullable", out var nullableNode) &&
                nullableNode?.GetValueKind() == JsonValueKind.True)
            {
                return true;
            }

            var schemaType = openApiSchema.Type;
            if (schemaType is null)
            {
                return false;
            }

            // Check if the type includes the Null flag (JsonSchemaType is a flags enum)
            if (schemaType.Value.HasFlag(JsonSchemaType.Null))
            {
                return true;
            }

            // Fallback: Check if the type string representation contains "null"
            var typeString = schemaType.ToString();
            return typeString is not null && typeString.Contains("Null", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks the schema's type, ignoring nullability: <c>type: object</c>, the OpenAPI 3.1
        /// <c>type: [object, 'null']</c> and the 3.0 <c>type: object</c> + <c>nullable: true</c>
        /// are all of type <see cref="JsonSchemaType.Object"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="JsonSchemaType"/> is a flags enum, so comparing <c>Type</c> with <c>==</c>
        /// is false for a nullable schema — which then is neither generated nor resolved.
        /// </remarks>
        /// <param name="type">The single (non-null) type to check for.</param>
        /// <returns>True when the schema's type, without the Null flag, is <paramref name="type"/>.</returns>
        public bool IsOfType(JsonSchemaType type)
            => schema is OpenApiSchema actual &&
               EffectiveType(actual) is { } schemaType &&
               (schemaType & ~JsonSchemaType.Null) == type;

        /// <summary>
        /// Determines whether a <c>oneOf</c> / <c>anyOf</c> composition carries a branch whose only
        /// contribution is nullability, in either specification's spelling.
        /// </summary>
        /// <remarks>
        /// OpenAPI 3.1 removed the <c>nullable</c> keyword, so <c>oneOf: [$ref, {type: "null"}]</c>
        /// is how a conformant 3.1 document says "this, or null". A 3.0 document has no
        /// <c>type: "null"</c> to write, so the same thing is spelled as an empty object that is
        /// nullable — <c>oneOf: [$ref, {type: object, nullable: true}]</c> — and the parser resolves
        /// that to the type flags <c>Object | Null</c>. Both are markers rather than real union
        /// members and are filtered out of the resulting type.
        /// </remarks>
        /// <returns><see langword="true"/> when such a branch is present.</returns>
        public bool HasNullBranchInOneOf()
        {
            if (schema is not OpenApiSchema openApiSchema)
            {
                return false;
            }

            return HasNullBranch(openApiSchema.OneOf) || HasNullBranch(openApiSchema.AnyOf);

            static bool HasNullBranch(IList<IOpenApiSchema>? branches)
            {
                if (branches is null)
                {
                    return false;
                }

                foreach (var branch in branches)
                {
                    if (IsNullOnlyBranch(branch))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Determines whether a <c>oneOf</c> / <c>anyOf</c> composition contains a branch that is
        /// neither a <c>$ref</c> nor a nullability marker.
        /// </summary>
        /// <remarks>
        /// Such a branch is a real union member the generator cannot name, so callers must fall
        /// back to an untyped result rather than silently dropping it — otherwise
        /// <c>oneOf: [$ref, {type: string}]</c> would masquerade as the reference alone.
        /// </remarks>
        /// <returns><see langword="true"/> when an unnameable branch is present.</returns>
        public bool HasNonReferenceBranchInOneOf()
        {
            if (schema is not OpenApiSchema openApiSchema)
            {
                return false;
            }

            return HasUnnameable(openApiSchema.OneOf) || HasUnnameable(openApiSchema.AnyOf);

            static bool HasUnnameable(IList<IOpenApiSchema>? branches)
            {
                if (branches is null)
                {
                    return false;
                }

                foreach (var branch in branches)
                {
                    if (branch is OpenApiSchemaReference)
                    {
                        continue;
                    }

                    if (IsNullOnlyBranch(branch))
                    {
                        continue;
                    }

                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Collects the <c>$ref</c> names from a <c>oneOf</c> / <c>anyOf</c> composition, ignoring
        /// any <c>type: "null"</c> marker branch.
        /// </summary>
        /// <returns>The referenced schema names, in declaration order.</returns>
        public List<string> GetOneOfReferenceIds()
        {
            var result = new List<string>();
            if (schema is not OpenApiSchema openApiSchema)
            {
                return result;
            }

            Collect(openApiSchema.OneOf);
            if (result.Count == 0)
            {
                Collect(openApiSchema.AnyOf);
            }

            return result;

            void Collect(IList<IOpenApiSchema>? branches)
            {
                if (branches is null)
                {
                    return;
                }

                foreach (var branch in branches)
                {
                    if (branch is not OpenApiSchemaReference branchRef)
                    {
                        continue;
                    }

                    var refName = branchRef.Reference.Id ?? branchRef.Id;
                    if (!string.IsNullOrEmpty(refName) && !result.Contains(refName!, StringComparer.Ordinal))
                    {
                        result.Add(refName!);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the schema has multiple non-null types (OpenAPI 3.1 feature).
        /// OpenAPI 3.1 allows type arrays like ["string", "integer"] which is represented
        /// as combined flags in Microsoft.OpenApi.
        /// </summary>
        /// <returns>True if the schema has more than one non-null type.</returns>
        public bool HasMultipleNonNullTypes()
        {
            if (schema is not OpenApiSchema openApiSchema)
            {
                return false;
            }

            var schemaType = openApiSchema.Type;
            if (schemaType is null)
            {
                return false;
            }

            return schemaType.Value.CountNonNullTypes() > 1;
        }

        /// <summary>
        /// Gets all non-null type names from the schema.
        /// </summary>
        /// <returns>A list of type names (e.g., ["string", "integer"]), or empty list.</returns>
        public IReadOnlyList<string> GetAllNonNullTypeNames()
        {
            if (schema is not OpenApiSchema openApiSchema)
            {
                return [];
            }

            return openApiSchema.Type?.GetNonNullTypeNames() ?? [];
        }

        /// <summary>
        /// Gets the primary non-null type from the schema.
        /// When multiple types are present (OpenAPI 3.1), returns the first by priority order.
        /// </summary>
        /// <returns>The primary type, or null if no type is defined.</returns>
        public JsonSchemaType? GetPrimaryNonNullType()
        {
            if (schema is not OpenApiSchema openApiSchema)
            {
                return null;
            }

            return openApiSchema.Type?.GetPrimaryType();
        }

        // ========== OpenAPI 3.1 $ref with Sibling Properties ==========

        /// <summary>
        /// Gets the <c>summary</c> value for a schema (OpenAPI 3.2 field).
        /// Microsoft.OpenApi 3.6.0 does not expose <c>summary</c> as a first-class property on
        /// <see cref="OpenApiSchema"/>, so the value is read from <see cref="OpenApiSchema.UnrecognizedKeywords"/>.
        /// Returns <see langword="null"/> if the schema is a reference (summary on a reference is not supported)
        /// or if no <c>summary</c> keyword is present.
        /// </summary>
        /// <returns>The summary string, or <see langword="null"/> if not set.</returns>
        public string? GetSchemaSummary()
        {
            if (schema is not OpenApiSchema actualSchema ||
                actualSchema.UnrecognizedKeywords is null)
            {
                return null;
            }

            return actualSchema.UnrecognizedKeywords.TryGetValue("summary", out var value)
                ? value?.ToString()
                : null;
        }

        /// <summary>
        /// Checks if this schema reference has any sibling properties that would override
        /// the target schema's properties (OpenAPI 3.1 feature).
        /// </summary>
        /// <returns>True if the reference has sibling properties.</returns>
        public bool HasRefSiblingProperties()
        {
            if (schema is not OpenApiSchemaReference schemaRef)
            {
                return false;
            }

            // Check for common sibling properties
            return !string.IsNullOrEmpty(schemaRef.Description) ||
                   schemaRef.Deprecated ||
                   schemaRef.Default is not null;
        }

        // ========== JSON Schema 2020-12 Support ==========

        /// <summary>
        /// Checks if the schema has a const value (JSON Schema 2020-12).
        /// A const value means the schema only validates against that exact value.
        /// </summary>
        /// <returns>True if the schema has a const value defined.</returns>
        public bool HasConstValue()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return false;
            }

            return !string.IsNullOrEmpty(actualSchema.Const);
        }

        /// <summary>
        /// Gets the const value from the schema (JSON Schema 2020-12).
        /// </summary>
        /// <returns>The const value as a string, or null if not defined.</returns>
        public string? GetConstValue()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return null;
            }

            return actualSchema.Const;
        }

        /// <summary>
        /// Checks if the schema explicitly uses unevaluatedProperties: false with composition (JSON Schema 2020-12).
        /// This is only relevant when the schema uses allOf/oneOf/anyOf composition.
        /// Note: Microsoft.OpenApi's UnevaluatedProperties defaults to false, so we only check
        /// when the schema actually uses composition keywords where unevaluatedProperties matters.
        /// </summary>
        /// <returns>True if unevaluatedProperties is false AND the schema uses composition.</returns>
        public bool HasUnevaluatedPropertiesRestriction()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return false;
            }

            // Only check when schema uses composition (allOf/oneOf/anyOf)
            // because unevaluatedProperties is only meaningful in composition contexts
            var hasComposition = actualSchema.AllOf?.Count > 0 ||
                                actualSchema.OneOf?.Count > 0 ||
                                actualSchema.AnyOf?.Count > 0;

            // If no composition, unevaluatedProperties doesn't matter
            if (!hasComposition)
            {
                return false;
            }

            // UnevaluatedProperties is a bool in Microsoft.OpenApi
            // When false, it means no additional properties beyond evaluated ones are allowed
            return !actualSchema.UnevaluatedProperties;
        }

        // ========== Content Encoding Support (JSON Schema 2020-12 / OpenAPI 3.1) ==========

        /// <summary>
        /// Gets the contentEncoding value from the schema (JSON Schema 2020-12 / OpenAPI 3.1).
        /// ContentEncoding indicates how the string content is encoded (e.g., "base64", "base64url").
        /// Note: Microsoft.OpenApi may not expose this directly, so we check UnrecognizedKeywords.
        /// </summary>
        /// <returns>The content encoding (e.g., "base64"), or null if not specified.</returns>
        public string? GetContentEncoding()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return null;
            }

            // Check UnrecognizedKeywords for contentEncoding (not a first-class property in Microsoft.OpenApi)
            if (actualSchema.UnrecognizedKeywords is not null &&
                actualSchema.UnrecognizedKeywords.TryGetValue("contentEncoding", out var value))
            {
                return value?.ToString();
            }

            return null;
        }

        /// <summary>
        /// Checks if the schema represents base64-encoded binary content.
        /// Returns true if contentEncoding is "base64" or "base64url".
        /// </summary>
        /// <returns>True if the content is base64-encoded.</returns>
        public bool IsBase64Encoded()
        {
            var encoding = schema.GetContentEncoding();
            return encoding?.ToLowerInvariant() is "base64" or "base64url";
        }

        /// <summary>
        /// Checks if this string schema should map to byte[] based on format or contentEncoding.
        /// Returns true for:
        /// - format: byte (OpenAPI 3.0 base64)
        /// - contentEncoding: base64 or base64url (JSON Schema 2020-12)
        /// </summary>
        /// <returns>True if the schema should map to byte[] in C#.</returns>
        public bool ShouldMapToByteArray()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return false;
            }

            // Check format: byte (OpenAPI 3.0 style)
            if (actualSchema.Format?.Equals("byte", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            // Check contentEncoding (JSON Schema 2020-12 / OpenAPI 3.1 style)
            return schema.IsBase64Encoded();
        }

        // ========== PrefixItems / Tuple Support (JSON Schema 2020-12 / OpenAPI 3.1) ==========

        /// <summary>
        /// Checks if the schema has prefixItems defined (JSON Schema 2020-12 / OpenAPI 3.1).
        /// PrefixItems defines a tuple with typed positional elements.
        /// </summary>
        /// <returns>True if prefixItems is defined.</returns>
        public bool HasPrefixItems()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return false;
            }

            // Microsoft.OpenApi 3.x doesn't surface prefixItems as a typed property — for
            // OpenAPI 3.1 specs the YAML reader drops the keyword into UnrecognizedKeywords,
            // and older specs that work around the parser put it under Extensions (with or
            // without the "x-" prefix). Accept both so the helper works regardless of how
            // the spec was written.
            return (actualSchema.UnrecognizedKeywords is not null &&
                    actualSchema.UnrecognizedKeywords.ContainsKey("prefixItems")) ||
                   (actualSchema.Extensions is not null &&
                    actualSchema.Extensions.ContainsKey("prefixItems"));
        }

        /// <summary>
        /// Checks if the schema is a strict tuple (prefixItems with items: false).
        /// A strict tuple only allows the exact elements defined in prefixItems.
        /// </summary>
        /// <returns>True if this is a strict tuple.</returns>
        public bool IsStrictTuple()
        {
            if (!schema.HasPrefixItems())
            {
                return false;
            }

            if (schema is not OpenApiSchema actualSchema)
            {
                return false;
            }

            // items: false is represented as a boolean false in extensions
            // or Items being null with no additional items schema
            if (actualSchema.Extensions is not null &&
                actualSchema.Extensions.TryGetValue("items", out var itemsExt))
            {
                // Check if items is explicitly false
                return IsExtensionValueFalse(itemsExt);
            }

            // If no items extension and Items schema is null, treat as strict
            return actualSchema.Items is null;
        }

        /// <summary>
        /// Gets tuple information from a prefixItems schema.
        /// </summary>
        /// <param name="document">The OpenAPI document for resolving references (can be null).</param>
        /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
        /// <returns>TupleInfo if prefixItems is present, null otherwise.</returns>
        public TupleInfo? GetTupleInfo(
            OpenApiDocument? document = null,
            TypeConflictRegistry? registry = null)
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return null;
            }

            // An OpenAPI 3.1 document carries prefixItems as an unrecognized keyword; older workarounds as an extension
            var prefixItemsArray = TypeScriptTypeMapper.GetPrefixItemsArray(actualSchema);
            if (prefixItemsArray is null)
            {
                return null;
            }

            var prefixItems = ExtractPrefixItems(prefixItemsArray, document, registry);
            if (prefixItems.Count == 0)
            {
                return null;
            }

            var isStrictTuple = schema.IsStrictTuple();
            string? additionalItemsType = null;

            // If not strict and has items schema, get the additional items type
            if (!isStrictTuple && actualSchema.Items is not null)
            {
                additionalItemsType = actualSchema.Items.ToCSharpType(true, registry);
            }

            return new TupleInfo
            {
                PrefixItems = prefixItems,
                IsStrictTuple = isStrictTuple,
                AdditionalItemsType = additionalItemsType,
                MinItems = actualSchema.MinItems > 0 ? actualSchema.MinItems : null,
                MaxItems = actualSchema.MaxItems > 0 ? actualSchema.MaxItems : null,
            };
        }

        /// <summary>
        /// Checks if an extension value represents boolean false.
        /// </summary>
        private static bool IsExtensionValueFalse(IOpenApiExtension? extension)
        {
            if (extension is not JsonNodeExtension jsonNodeExt)
            {
                return false;
            }

            return jsonNodeExt.Node is { } node && node.GetValueKind() == JsonValueKind.False;
        }

        /// <summary>
        /// Extracts the tuple items from the prefixItems array.
        /// </summary>
        private static List<TupleItemInfo> ExtractPrefixItems(
            JsonArray jsonArray,
            OpenApiDocument? document,
            TypeConflictRegistry? registry)
        {
            var result = new List<TupleItemInfo>();

            var index = 0;
            foreach (var item in jsonArray)
            {
                if (item is JsonObject schemaObj)
                {
                    var itemInfo = ParseTupleItemFromJson(schemaObj, index, registry);
                    result.Add(itemInfo);
                }

                index++;
            }

            return result;
        }

        /// <summary>
        /// Parses a tuple item from a JSON schema object.
        /// </summary>
        /// <summary>
        /// The schema's type - or, without a <c>type</c> (JSON Schema 2020-12 needs none next to <c>const</c> or
        /// <c>enum</c>), the type its values have: a const is kept as text by the reader, so it is a string; an enum
        /// is the kind of its members (strings, integers, numbers or booleans; <c>null</c> members do not count).
        /// Neither is an <c>object</c> no client could pass a value to.
        /// </summary>
        private static JsonSchemaType? EffectiveType(OpenApiSchema actual)
        {
            if (actual.Type is { } type)
            {
                return type;
            }

            if (!string.IsNullOrEmpty(actual.Const))
            {
                return JsonSchemaType.String;
            }

            var members = (actual.Enum ?? []).Where(m => m is JsonValue value && value.GetValueKind() != JsonValueKind.Null).Cast<JsonValue>().ToList();
            if (members.Count == 0)
            {
                return null;
            }

            if (members.All(m => m.GetValueKind() == JsonValueKind.String))
            {
                return JsonSchemaType.String;
            }

            if (members.All(m => m.GetValueKind() is JsonValueKind.True or JsonValueKind.False))
            {
                return JsonSchemaType.Boolean;
            }

            if (members.All(m => m.GetValueKind() == JsonValueKind.Number))
            {
                return members.All(m => long.TryParse(m.ToJsonString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    ? JsonSchemaType.Integer
                    : JsonSchemaType.Number;
            }

            return JsonSchemaType.String;
        }

        /// <summary>
        /// Whether a schema's <c>default</c> can be the default of a C# parameter: a string, number or boolean for a
        /// property that is a string, a number, a boolean or an enum (its own or one it wraps in <c>allOf</c>). A date
        /// (<c>DateOnly</c> is no constant), an array or an object cannot.
        /// </summary>
        /// <param name="registry">The registry of the generation; with <see cref="TypeConflictRegistry.ExtensibleEnums"/> a string enum's member is no constant.</param>
        public bool HasConstantDefault(TypeConflictRegistry? registry = null)
            => DefaultIsConstant(schema, registry);

        private static bool DefaultIsConstant(
            IOpenApiSchema candidate,
            TypeConflictRegistry? registry)
        {
            if (candidate is not OpenApiSchema actual ||
                actual.Default is not JsonValue value ||
                value.GetValueKind() is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }

            if ((actual.Enum is { Count: > 0 } ? actual : actual.WrappedEnum()) is { } enumSchema)
            {
                // A client's extensible enum is a struct: its members are no compile-time constants
                return registry?.ExtensibleEnums != true || !enumSchema.IsOfType(JsonSchemaType.String);
            }

            var csharp = EffectiveType(actual).ToPrimitiveCSharpTypeName(actual.Format);
            return csharp is "string" or "bool" or "int" or "long" or "short" or "double" or "float" or "decimal";
        }

        /// <summary>
        /// The one reference the schema wraps in an <c>allOf</c> - <c>allOf: [$ref, {}]</c>, the OpenAPI 3.0 way to give
        /// a reference a description or <c>nullable</c> (what springdoc writes) - or null: a schema with a type, its own
        /// properties, items, an enum or another composition, or an <c>allOf</c> with more than a reference and empty
        /// schemas, is no wrapper.
        /// </summary>
        /// <returns>The wrapped reference, or null.</returns>
        public OpenApiSchemaReference? SingleWrappedReference()
        {
            if (schema is not OpenApiSchema wrapper ||
                wrapper.AllOf is not { Count: > 0 } allOf ||
                HasValueType(wrapper.Type) ||
                wrapper.Properties is { Count: > 0 } ||
                wrapper.OneOf is { Count: > 0 } ||
                wrapper.AnyOf is { Count: > 0 } ||
                wrapper.Items is not null ||
                wrapper.Enum is { Count: > 0 })
            {
                return null;
            }

            var references = allOf.OfType<OpenApiSchemaReference>().ToList();
            return references.Count == 1 && allOf.All(part => part is OpenApiSchemaReference || IsEmptySchema(part))
                ? references[0]
                : null;
        }

        /// <summary>
        /// Whether the value has no text form - an object, a map, a composition without a type, or an array of any of
        /// them - so it is no URL-encoded form field (or query value) a client can send or a server can bind; in a
        /// multipart form it is sent as an application/json part. An enum or a model
        /// wrapped as <c>allOf: [$ref]</c> is judged as what it wraps; an untyped schema is its text.
        /// </summary>
        /// <returns>True for a value with no text form.</returns>
        public bool HasNoTextForm()
        {
            var value = (IOpenApiSchema?)schema.SingleWrappedReference() ?? schema;
            if (value.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
                return value.Items is { } items && items.HasNoTextForm();
            }

            return value.Type?.HasFlag(JsonSchemaType.Object) == true ||
                   value.Properties is { Count: > 0 } ||
                   ((value.Type is null || value.Type == JsonSchemaType.Null) &&
                    (value.OneOf is { Count: > 0 } || value.AnyOf is { Count: > 0 } || value.AllOf is { Count: > 0 }));
        }

        /// <summary>
        /// Whether a model property of this schema can be null (so a client checks it before sending it): its C#
        /// type is nullable, or a reference type (a string, a <c>Uri</c>, an object). A required value type cannot.
        /// </summary>
        /// <param name="isRequired">Whether the model requires the property.</param>
        /// <returns>True when the property can be null.</returns>
        public bool CanBeNullInQueryObject(bool isRequired)
        {
            var type = schema.ToCSharpTypeForModel(isRequired);
            return type.EndsWith("?", StringComparison.Ordinal) ||
                   type is "string" or "object" or "Uri" or "System.Uri" or "byte[]";
        }

        /// <summary>
        /// Whether the schema says nothing about the value (<c>{}</c>, or one with only a description or
        /// <c>nullable</c>): no type but null, no format, properties, items, enum or composition. A parameter of such a
        /// schema is the text it is on the wire.
        /// </summary>
        /// <returns>True for an untyped schema.</returns>
        public bool IsUntyped()
            => schema is OpenApiSchema actual &&
               (actual.Type is null || actual.Type == JsonSchemaType.Null) &&
               string.IsNullOrEmpty(actual.Format) &&
               actual.Properties is null or { Count: 0 } &&
               actual.Items is null &&
               actual.Enum is null or { Count: 0 } &&
               actual.AllOf is null or { Count: 0 } &&
               actual.OneOf is null or { Count: 0 } &&
               actual.AnyOf is null or { Count: 0 };

        /// <summary>
        /// The enum a property wraps as <c>allOf: [$ref]</c> (the 3.0 way to give a referenced enum a default or
        /// a description), or <see langword="null"/>.
        /// </summary>
        /// <returns>The enum schema the single <c>allOf</c> reference points to.</returns>
        public OpenApiSchema? WrappedEnum()
            => schema is OpenApiSchema { AllOf: { Count: 1 } allOf } &&
               allOf[0] is OpenApiSchemaReference { Target: OpenApiSchema { Enum.Count: > 0 } target }
                ? target
                : null;

        private static (string? Value, bool IsExclusive) Bound(
            string? inclusive,
            string? exclusive)
            => !string.IsNullOrEmpty(inclusive)
                ? (inclusive, false)
                : !string.IsNullOrEmpty(exclusive)
                    ? (exclusive, true)
                    : (null, false);

        private static string? ParseNumberBound(string? bound)
            => double.TryParse(bound, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && !double.IsInfinity(value) && !double.IsNaN(value)
                ? value.ToString(CultureInfo.InvariantCulture)
                : null;

        /// <summary>
        /// An integer whose bounds do not fit an <c>int</c> (<c>maximum: 4294967295</c>) is a <c>long</c>.
        /// </summary>
        private static string WidenForBounds(
            string typeName,
            OpenApiSchema actual)
        {
            if (!string.Equals(typeName, "int", StringComparison.Ordinal))
            {
                return typeName;
            }

            return Exceeds(actual.Minimum) || Exceeds(actual.Maximum) ? "long" : typeName;

            static bool Exceeds(string? bound)
                => decimal.TryParse(bound, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                   (value > int.MaxValue || value < int.MinValue);
        }

        private static TupleItemInfo ParseTupleItemFromJson(
            JsonObject schemaObj,
            int index,
            TypeConflictRegistry? registry)
        {
            // Extract type info
            var typeStr = JsonKeywordReader.StringOrNull(schemaObj["type"]) ?? "object";
            var format = JsonKeywordReader.StringOrNull(schemaObj["format"]);
            var description = JsonKeywordReader.StringOrNull(schemaObj["description"]);
            var nullable = schemaObj["nullable"]?.GetValue<bool>() ?? false;

            // Handle $ref
            var refStr = JsonKeywordReader.StringOrNull(schemaObj["$ref"]);
            string csharpType;
            var refName = !string.IsNullOrEmpty(refStr)
                ? refStr!.Split('/').LastOrDefault()
                : null;
            if (!string.IsNullOrWhiteSpace(refName))
            {
                csharpType = ResolveTypeName(refName!, registry);
            }
            else
            {
                // Either no $ref, or a malformed one (e.g. a trailing-slash ref whose last
                // segment is empty). Fall back to the mapped primitive type rather than
                // emitting an empty, invalid C# type name.
                csharpType = IOpenApiSchema.MapJsonTypeToCSharp(typeStr, format);
            }

            // Generate name from description or use positional
            var name = IOpenApiSchema.GenerateTupleElementName(description, index);

            return new TupleItemInfo
            {
                CSharpType = csharpType,
                Name = name,
                Description = description,
                IsNullable = nullable,
            };
        }

        /// <summary>
        /// Generates a name for a tuple element from its description or position.
        /// </summary>
        private static string GenerateTupleElementName(
            string? description,
            int index)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return $"Item{index + 1}";
            }

            // Extract first word as name, convert to PascalCase
            var firstWord = description.Split(' ', '.', ',', ':')[0];
            return firstWord.ToPascalCaseForDotNet();
        }

        /// <summary>
        /// Maps a JSON Schema type and format to a C# type.
        /// </summary>
        private static string MapJsonTypeToCSharp(
            string jsonType,
            string? format)
            => jsonType.ToLowerInvariant() switch
            {
                "string" when format == "uuid" => "Guid",
                "string" when format == "date-time" => "DateTimeOffset",
                "string" when format == "date" => "DateOnly",
                "string" when format == "uri" => "Uri",
                "string" when format == "byte" => "byte[]",
                "string" => "string",
                "integer" when format == "int64" => "long",
                "integer" => "int",
                "number" when format == "float" => "float",
                "number" => "double",
                "boolean" => "bool",
                _ => "object",
            };

        /// <summary>
        /// Gets the reference ID from a schema reference.
        /// </summary>
        /// <returns>The reference ID or null if not a reference.</returns>
        public string? GetReferenceId()
        {
            if (schema is OpenApiSchemaReference schemaRef)
            {
                return schemaRef.Reference.Id ?? schemaRef.Id;
            }

            return null;
        }

        /// <summary>
        /// The component a reference names when that component is an alias - only a primitive or an array (no enum, no
        /// composition): the generators emit no type for it, so the value takes the aliased type and its constraints.
        /// </summary>
        /// <returns>The aliased schema, or null when this is no reference to an alias.</returns>
        public OpenApiSchema? GetAliasTarget()
            => schema is OpenApiSchemaReference { Target: OpenApiSchema target } && IsAliasSchema(target)
                ? target
                : null;

        /// <summary>
        /// Resolves a schema reference to the actual schema from the document.
        /// </summary>
        /// <param name="document">The OpenAPI document containing schema definitions.</param>
        /// <returns>The resolved OpenApiSchema or null.</returns>
        public OpenApiSchema? ResolveSchema(OpenApiDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var refId = schema.GetReferenceId();
            if (refId is null)
            {
                return schema as OpenApiSchema;
            }

            if (document.Components?.Schemas is not null &&
                document.Components.Schemas.TryGetValue(refId, out var resolvedSchema))
            {
                return resolvedSchema as OpenApiSchema;
            }

            return null;
        }

        /// <summary>
        /// Gets validation attributes for a schema based on OpenAPI constraints.
        /// </summary>
        /// <param name="isRequired">Whether the property is required.</param>
        /// <returns>A list of validation attribute strings (e.g., "Required", "Range(0, 100)").</returns>
        public IList<string> GetValidationAttributes(bool isRequired)
        {
            IList<string> attributes = new List<string>();

            // Add Required attribute for required properties
            if (isRequired)
            {
                attributes.Add("Required");
            }

            // A reference to an alias (a component that is only a primitive or an array, ProductCode: a string with a
            // pattern) is no generated type: the value is the aliased type, so the component's constraints are its own
            if (schema is OpenApiSchemaReference { Target: OpenApiSchema aliasTarget } && IsAliasSchema(aliasTarget))
            {
                return aliasTarget.GetValidationAttributes(isRequired);
            }

            // Handle actual schemas (not references)
            if (schema is not OpenApiSchema actualSchema)
            {
                return attributes;
            }

            // Range validation for numeric types (use HasFlag for flags enum compatibility)
            var schemaType = actualSchema.Type ?? JsonSchemaType.Null;

            // An exclusive bound (the 3.0 boolean next to minimum/maximum, which the reader folds into
            // exclusiveMinimum/exclusiveMaximum, or the 3.1 number) is the same Range with its exclusive flag
            var (minimum, minimumIsExclusive) = Bound(actualSchema.Minimum, actualSchema.ExclusiveMinimum);
            var (maximum, maximumIsExclusive) = Bound(actualSchema.Maximum, actualSchema.ExclusiveMaximum);
            var flags = (minimumIsExclusive ? ", MinimumIsExclusive = true" : string.Empty) +
                        (maximumIsExclusive ? ", MaximumIsExclusive = true" : string.Empty);
            if (schemaType.HasFlag(JsonSchemaType.Integer))
            {
                // A bound may be written as a decimal or in exponent form (minimum: 1.0, maximum: 1e3):
                // it is rounded inwards to the integers it allows. A bound that is no number is skipped.
                var min = ParseIntegerBound(minimum, isMinimum: true);
                var max = ParseIntegerBound(maximum, isMinimum: false);

                if (min is not null || max is not null)
                {
                    attributes.Add($"Range({min?.ToString(CultureInfo.InvariantCulture) ?? "long.MinValue"}, {max?.ToString(CultureInfo.InvariantCulture) ?? "long.MaxValue"}{flags})");
                }
            }
            else if (schemaType.HasFlag(JsonSchemaType.Number))
            {
                var min = ParseNumberBound(minimum);
                var max = ParseNumberBound(maximum);

                if (min is not null || max is not null)
                {
                    attributes.Add($"Range({min ?? "double.MinValue"}, {max ?? "double.MaxValue"}{flags})");
                }
            }

            // String length validation: ApiMinLength/ApiMaxLength, the trim- and AOT-safe MinLength/MaxLength a generated
            // project declares (LengthLimitAttributesExtractor)
            // Skip for formats that convert to non-string C# types (Uri, Guid, DateTimeOffset, byte[])
            if (schemaType.HasFlag(JsonSchemaType.String))
            {
                var format = actualSchema.Format?.ToLowerInvariant();
                var isNonStringFormat = format is "uri" or "uuid" or "date" or "date-time" or "byte";

                if (!isNonStringFormat)
                {
                    if (actualSchema.MinLength > 0)
                    {
                        attributes.Add($"ApiMinLength({actualSchema.MinLength})");
                    }

                    if (actualSchema.MaxLength > 0)
                    {
                        attributes.Add($"ApiMaxLength({actualSchema.MaxLength})");
                    }

                    // Regular expression validation: ApiRegularExpression, the generated RegularExpression that matches an
                    // empty string against the pattern too (the base counts "" as valid; the specification's pattern
                    // applies to it). A verbatim literal keeps the backslashes; a double quote is doubled
                    if (!string.IsNullOrWhiteSpace(actualSchema.Pattern))
                    {
                        var escapedPattern = actualSchema.Pattern.Replace("\"", "\"\"");
                        attributes.Add($"ApiRegularExpression(@\"{escapedPattern}\")");
                    }
                }

                // Format-based validation attributes: an email address; a uri is absolute (a relative reference is
                // format: uri-reference), which the Uri it binds to does not check
                switch (format)
                {
                    case "email":
                        attributes.Add("EmailAddress");
                        break;
                    case "uri":
                        attributes.Add("ApiAbsoluteUri");
                        break;
                }
            }

            // Array length validation
            if (schemaType.HasFlag(JsonSchemaType.Array))
            {
                var hasMinItems = actualSchema.MinItems > 0;
                var hasMaxItems = actualSchema.MaxItems > 0;

                if (hasMinItems)
                {
                    attributes.Add($"ApiMinLength({actualSchema.MinItems})");
                }

                if (hasMaxItems)
                {
                    attributes.Add($"ApiMaxLength({actualSchema.MaxItems})");
                }

                // A pattern on the items (in place, or on the string component they reference): RegularExpression
                // validates one string, so each item is matched by the generated ApiItemsRegularExpression
                if (ItemPattern(actualSchema.Items) is { } itemPattern)
                {
                    attributes.Add($"ApiItemsRegularExpression(@\"{itemPattern.Replace("\"", "\"\"")}\")");
                }

                // Items of format: uri are absolute, as one is
                var items = actualSchema.Items is OpenApiSchemaReference itemsRef ? itemsRef.GetAliasTarget() : actualSchema.Items as OpenApiSchema;
                if (items is { Type: { } itemType } && itemType.HasFlag(JsonSchemaType.String) &&
                    string.Equals(items.Format, "uri", StringComparison.OrdinalIgnoreCase))
                {
                    attributes.Add("ApiAbsoluteUri");
                }
            }

            return attributes;
        }

        /// <summary>
        /// The pattern every item of this array must match: the <c>pattern</c> of a string <c>items</c> schema, declared
        /// in place or on the string component the items reference; null when there is none.
        /// </summary>
        /// <returns>The pattern, or null.</returns>
        public string? GetItemPattern()
            => schema is OpenApiSchema { Type: { } type } actual && type.HasFlag(JsonSchemaType.Array)
                ? ItemPattern(actual.Items)
                : null;

        /// <summary>
        /// Checks if schema represents a file or collection of files.
        /// </summary>
        /// <returns>A tuple with IsFile (true if file type) and IsCollection (true if array of files).</returns>
        public (bool IsFile, bool IsCollection) GetFileUploadInfo()
        {
            if (schema is OpenApiSchema actualSchema)
            {
                // Single file: type: string, format: binary
                if (actualSchema.Type?.HasFlag(JsonSchemaType.String) == true &&
                    string.Equals(actualSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, false);
                }

                // Array of files
                if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true &&
                    actualSchema.Items is OpenApiSchema itemSchema &&
                    string.Equals(itemSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, true);
                }
            }

            return (false, false);
        }

        /// <summary>
        /// Maps an OpenAPI schema to a C# type string for parameters.
        /// Optional parameters are made nullable so they can represent "not provided".
        /// </summary>
        /// <param name="isRequired">Whether the parameter is required.</param>
        /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
        /// <returns>A C# type string representation.</returns>
        public string ToCSharpType(
            bool isRequired,
            TypeConflictRegistry? registry = null)
        {
            // Handle schema references
            if (schema is OpenApiSchemaReference schemaRef)
            {
                // An alias (a component that is only an array or a primitive) is no generated type: the parameter
                // takes the aliased type itself (string, List<string>, Guid, ...)
                if (schemaRef.Target is OpenApiSchema aliasTarget && IsAliasSchema(aliasTarget))
                {
                    return aliasTarget.ToCSharpType(isRequired, registry);
                }

                // Try Reference.Id first (Microsoft.OpenApi v3.0+), fall back to Id
                var refName = schemaRef.Reference.Id ?? schemaRef.Id ?? "object";
                refName = ResolveTypeName(refName, registry);
                return isRequired ? refName : $"{refName}?";
            }

            // Handle actual schemas
            if (schema is not OpenApiSchema schema1)
            {
                return "object";
            }

            // Handle base64-encoded content (format: byte or contentEncoding: base64)
            // Must check before generic type mapping since both result in string type
            if (schema.ShouldMapToByteArray())
            {
                return isRequired ? "byte[]" : "byte[]?";
            }

            // Handle array types - check using HasFlag for combined flags
            if (schema1.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
                var itemType = schema1.GetArrayItemType(registry);
                return isRequired ? $"List<{itemType}>" : $"List<{itemType}>?";
            }

            // Handle primitive types using HasFlag for combined flags (e.g., String | Null)
            var baseType = WidenForBounds(EffectiveType(schema1).ToCSharpTypeName(schema1.Format, includeIFormFile: true), schema1);

            // For parameters: optional value types and strings should be nullable
            if (!isRequired && (CSharpTypeHelper.IsExtendedValueType(baseType) || baseType == "string"))
            {
                return $"{baseType}?";
            }

            return baseType;
        }

        /// <summary>
        /// Maps an OpenAPI schema to a C# type string for model properties.
        /// A property is nullable when the schema says so (<c>nullable: true</c>, <c>type: [..., "null"]</c>), or when it
        /// is optional (not in <c>required</c>) and has no <c>default</c>: a property left out of the JSON is null.
        /// </summary>
        /// <param name="isRequired">Whether the property is in the required array.</param>
        /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
        /// <returns>A C# type string representation.</returns>
        public string ToCSharpTypeForModel(
            bool isRequired,
            TypeConflictRegistry? registry = null)
        {
            // Optional without a default: a property the JSON leaves out is null - for a value type not a default
            // (0, false) that looks like a sent value, for a reference type not a "never null" the compiler trusts.
            // A default no C# constant can hold (a date, an array, an object) is no default for the parameter: the
            // property stays optional and nullable, as if it had none
            var isOptional = !isRequired && !DefaultIsConstant(schema, registry);

            // Handle schema references (a model, or an enum)
            if (schema is OpenApiSchemaReference schemaRef)
            {
                // An alias (a component that is only an array or a primitive) is no generated type:
                // the property takes the aliased type itself (List<string>, Guid, ...).
                if (schemaRef.Target is OpenApiSchema aliasTarget && IsAliasSchema(aliasTarget))
                {
                    return aliasTarget.ToCSharpTypeForModel(isRequired, registry);
                }

                var refName = schemaRef.Reference.Id ?? schemaRef.Id ?? "object";
                var refTypeName = ResolveTypeName(refName, registry);

                // A component that allows null (type: [object, 'null']) makes the property nullable
                // even when it is required.
                return isOptional || schemaRef.Target.IsNullable() ? $"{refTypeName}?" : refTypeName;
            }

            // Handle actual schemas
            if (schema is not OpenApiSchema schema1)
            {
                return "object";
            }

            // Check if schema has nullable: true (in OpenAPI 3.1/Microsoft.OpenApi 3.0.x, this is indicated by JsonSchemaType.Null flag)
            var isNullable = schema1.IsNullable() || isOptional;

            // Handle allOf compositions (commonly used for nullable $ref in OpenAPI 3.0)
            // Pattern: allOf: [ $ref: "#/..." ] with nullable: true
            if (schema1.AllOf is { Count: > 0 })
            {
                // Find the $ref schema in allOf
                foreach (var subSchema in schema1.AllOf)
                {
                    if (subSchema is OpenApiSchemaReference allOfRef)
                    {
                        // Check if the referenced schema is a polymorphic type (oneOf/anyOf)
                        // Polymorphic base types are not currently generated by the generators,
                        // so we fall back to object to maintain compatibility
                        var targetSchema = allOfRef.Target;
                        if (targetSchema is not null && targetSchema.HasPolymorphicComposition())
                        {
                            return isNullable
                                ? "object?"
                                : "object";
                        }

                        var refName = allOfRef.Reference.Id ?? allOfRef.Id ?? "object";
                        var typeName = ResolveTypeName(refName, registry);
                        return isNullable ? $"{typeName}?" : typeName;
                    }
                }
            }

            // Handle oneOf carrying a single reference, with or without an explicit null branch.
            //
            //   oneOf: [ $ref ]                    + nullable: true   — the OpenAPI 3.0 spelling
            //   oneOf: [ $ref, { type: "null" } ]                     — the OpenAPI 3.1 spelling
            //
            // Both mean "this reference, or null". 3.1 removed the `nullable` keyword, so the
            // second form is what a conformant 3.1 document uses; treating the null branch as just
            // another union member would degrade the property to `object`.
            var oneOfHasNullBranch = schema1.HasNullBranchInOneOf();
            var oneOfRefs = schema1.GetOneOfReferenceIds();
            if (oneOfRefs.Count == 1 && !schema1.HasNonReferenceBranchInOneOf())
            {
                var typeName = ResolveTypeName(oneOfRefs[0], registry);
                return isNullable || oneOfHasNullBranch ? $"{typeName}?" : typeName;
            }

            // Handle base64-encoded content (format: byte or contentEncoding: base64)
            // Must check before generic type mapping since both result in string type
            if (schema.ShouldMapToByteArray())
            {
                return isNullable
                    ? "byte[]?"
                    : "byte[]";
            }

            // Handle array types - check using HasFlag for combined flags
            if (schema1.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
                var itemType = schema1.GetArrayItemType(registry);
                return isNullable ? $"List<{itemType}>?" : $"List<{itemType}>";
            }

            // Handle primitive types using HasFlag for combined flags (e.g., String | Null)
            var baseType = WidenForBounds(EffectiveType(schema1).ToCSharpTypeName(schema1.Format, includeIFormFile: true), schema1);

            return isNullable
                ? $"{baseType}?"
                : baseType;
        }

        /// <summary>
        /// Reads an integer schema's <c>minimum</c> / <c>maximum</c>, which a specification may write as any
        /// JSON number. Rounded inwards (a minimum up, a maximum down) and limited to the range of a long.
        /// </summary>
        private static long? ParseIntegerBound(
            string? bound,
            bool isMinimum)
        {
            if (string.IsNullOrEmpty(bound) ||
                !decimal.TryParse(bound, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            value = isMinimum ? decimal.Ceiling(value) : decimal.Floor(value);
            if (value <= long.MinValue)
            {
                return long.MinValue;
            }

            return value >= long.MaxValue ? long.MaxValue : (long)value;
        }

        /// <summary>
        /// A component schema that only names an array or a primitive (no enum, no composition,
        /// no tuple) - the generators emit no type for it.
        /// </summary>
        private static bool IsAliasSchema(OpenApiSchema target)
        {
            if (EffectiveType(target) is not { } type ||
                target.AllOf is { Count: > 0 } ||
                target.HasPolymorphicComposition() ||
                target.HasPrefixItems())
            {
                return false;
            }

            var withoutNull = type & ~JsonSchemaType.Null;

            // A string or integer enum is a generated enum type; a number or boolean enum has no C# enum to be and
            // is its primitive, with the values in the documentation
            if (target.Enum is { Count: > 0 } && withoutNull is JsonSchemaType.String or JsonSchemaType.Integer)
            {
                return false;
            }

            return withoutNull != default && withoutNull != JsonSchemaType.Object;
        }

        // The pattern of an array's string items: declared in place, or on the string component they reference
        private static string? ItemPattern(IOpenApiSchema? items)
        {
            var item = items switch
            {
                OpenApiSchemaReference { Target: OpenApiSchema target } when IsAliasSchema(target) => target,
                OpenApiSchema inPlace => inPlace,
                _ => null,
            };

            return item is { Type: { } type } &&
                   type.HasFlag(JsonSchemaType.String) &&
                   !string.IsNullOrWhiteSpace(item.Pattern) &&
                   item.Format?.ToLowerInvariant() is not ("uri" or "uuid" or "date" or "date-time" or "byte")
                ? item.Pattern
                : null;
        }

        /// <summary>
        /// Checks if the schema uses allOf composition.
        /// </summary>
        /// <returns>True if the schema has allOf elements.</returns>
        public bool HasAllOfComposition()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return false;
            }

            return actualSchema.AllOf is { Count: > 0 };
        }

        /// <summary>
        /// Checks if the schema uses oneOf composition.
        /// </summary>
        /// <returns>True if the schema has oneOf elements.</returns>
        public bool HasOneOfComposition()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return false;
            }

            return actualSchema.OneOf is { Count: > 0 };
        }

        /// <summary>
        /// Checks if the schema uses anyOf composition.
        /// </summary>
        /// <returns>True if the schema has anyOf elements.</returns>
        public bool HasAnyOfComposition()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return false;
            }

            return actualSchema.AnyOf is { Count: > 0 };
        }

        /// <summary>
        /// Checks if the schema uses polymorphic composition (oneOf or anyOf).
        /// </summary>
        /// <returns>True if the schema has oneOf or anyOf elements.</returns>
        public bool HasPolymorphicComposition()
            => schema.HasOneOfComposition() || schema.HasAnyOfComposition();

        /// <summary>
        /// Returns true when the schema has an explicit discriminator block, regardless of whether
        /// <c>propertyName</c> is set. Use this to distinguish "discriminator block present but
        /// propertyName omitted" from "no discriminator at all".
        /// </summary>
        public bool HasDiscriminatorBlock()
            => schema is OpenApiSchema actualSchema && actualSchema.Discriminator is not null;

        /// <summary>
        /// Gets the discriminator property name from a polymorphic schema.
        /// </summary>
        /// <returns>The discriminator property name, or null if not defined.</returns>
        public string? GetDiscriminatorPropertyName()
            => schema is not OpenApiSchema actualSchema
                ? null
                : actualSchema.Discriminator?.PropertyName;

        /// <summary>
        /// Gets the schema name from the OpenAPI 3.2 <c>discriminator.defaultMapping</c> field.
        /// </summary>
        /// <returns>The schema name to use as fallback, or null if not defined.</returns>
        public string? GetDiscriminatorDefaultMappingSchemaName()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return null;
            }

            var defaultMapping = actualSchema.Discriminator?.DefaultMapping;
            if (defaultMapping is null)
            {
                return null;
            }

            var schemaName = defaultMapping.Reference?.Id ?? defaultMapping.Id;
            return string.IsNullOrEmpty(schemaName) ? null : schemaName;
        }

        /// <summary>
        /// Gets the discriminator mapping from a polymorphic schema.
        /// </summary>
        /// <returns>A dictionary mapping discriminator values to schema reference IDs, or null if not defined.</returns>
        public IDictionary<string, string>? GetDiscriminatorMapping()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return null;
            }

            var mapping = actualSchema.Discriminator?.Mapping;
            if (mapping is null || mapping.Count == 0)
            {
                return null;
            }

            // Convert to dictionary, extracting just the schema name from the schema reference
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kvp in mapping)
            {
                // kvp.Value is OpenApiSchemaReference - extract the reference ID
                var schemaRef = kvp.Value;
                var schemaName = schemaRef.Reference?.Id ?? schemaRef.Id;

                if (!string.IsNullOrEmpty(schemaName))
                {
                    result[kvp.Key] = schemaName!;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the schema names of all polymorphic variants from oneOf or anyOf.
        /// </summary>
        /// <returns>A list of schema reference IDs.</returns>
        public IList<string> GetPolymorphicVariantSchemaNames()
        {
            var result = new List<string>();

            if (schema is not OpenApiSchema actualSchema)
            {
                return result;
            }

            // Collect from oneOf
            if (actualSchema.OneOf is not null)
            {
                foreach (var subSchema in actualSchema.OneOf)
                {
                    if (subSchema is OpenApiSchemaReference schemaRef)
                    {
                        var refId = schemaRef.Reference.Id ?? schemaRef.Id;
                        if (!string.IsNullOrEmpty(refId))
                        {
                            result.Add(refId!);
                        }
                    }
                }
            }

            // Collect from anyOf
            if (actualSchema.AnyOf is not null)
            {
                foreach (var subSchema in actualSchema.AnyOf)
                {
                    if (subSchema is OpenApiSchemaReference schemaRef)
                    {
                        var refId = schemaRef.Reference.Id ?? schemaRef.Id;
                        if (!string.IsNullOrEmpty(refId))
                        {
                            result.Add(refId!);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Auto-detects a discriminator property from polymorphic variant schemas.
        /// Looks for a common string property with priority: "type", "kind", "discriminator", "$type".
        /// </summary>
        /// <param name="document">The OpenAPI document for resolving schema references.</param>
        /// <returns>The detected discriminator property name, or null if not found.</returns>
        public string? DetectDiscriminatorProperty(OpenApiDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var variantNames = schema.GetPolymorphicVariantSchemaNames();
            if (variantNames.Count == 0)
            {
                return null;
            }

            // Priority list of common discriminator property names
            var priorityNames = new[] { "type", "kind", "discriminator", "$type" };

            // Get all string properties from each variant schema
            var variantProperties = new List<HashSet<string>>();
            foreach (var variantName in variantNames)
            {
                if (document.Components?.Schemas is null ||
                    !document.Components.Schemas.TryGetValue(variantName, out var variantSchema))
                {
                    continue;
                }

                if (variantSchema is not OpenApiSchema actualVariant || actualVariant.Properties is null)
                {
                    continue;
                }

                var stringProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in actualVariant.Properties)
                {
                    if (prop.Value is OpenApiSchema propSchema &&
                        propSchema.Type?.HasFlag(JsonSchemaType.String) == true)
                    {
                        stringProps.Add(prop.Key);
                    }
                }

                variantProperties.Add(stringProps);
            }

            if (variantProperties.Count == 0)
            {
                return null;
            }

            // Find common properties across all variants
            var commonProps = variantProperties[0];
            for (var i = 1; i < variantProperties.Count; i++)
            {
                commonProps = new HashSet<string>(
                    commonProps.Where(p => variantProperties[i].Contains(p, StringComparer.OrdinalIgnoreCase)),
                    StringComparer.OrdinalIgnoreCase);
            }

            // Check priority names first
            foreach (var priorityName in priorityNames)
            {
                if (commonProps.Contains(priorityName, StringComparer.OrdinalIgnoreCase))
                {
                    // Return the actual property name with correct casing
                    foreach (var prop in commonProps)
                    {
                        if (prop.Equals(priorityName, StringComparison.OrdinalIgnoreCase))
                        {
                            return prop;
                        }
                    }
                }
            }

            // If no priority name found, return the alphabetically first common string property
            // (sorted for deterministic output across GC runs and .NET versions)
            return commonProps.Count > 0 ? commonProps.OrderBy(p => p, StringComparer.Ordinal).First() : null;
        }

        /// <summary>
        /// Checks if the schema has additionalProperties defined.
        /// </summary>
        /// <returns>True if additionalProperties is defined (either as true or as a typed schema).</returns>
        public bool HasAdditionalProperties()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return false;
            }

            return actualSchema.AdditionalProperties is not null;
        }

        /// <summary>
        /// Checks if additionalProperties is defined as 'true' (any type allowed).
        /// In OpenAPI, additionalProperties: true means any additional string-keyed values are allowed.
        /// </summary>
        /// <returns>True if additionalProperties allows any type.</returns>
        public bool IsAdditionalPropertiesAnyType()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return false;
            }

            // When additionalProperties: true, the schema has no type defined or is empty
            if (actualSchema.AdditionalProperties is OpenApiSchema addPropsSchema)
            {
                // No type means "any" (additionalProperties: true)
                // Also check for empty object schema
                return addPropsSchema.Type is null ||
                       addPropsSchema.Type == JsonSchemaType.Null ||
                       (addPropsSchema.Type.Value == default && addPropsSchema.Properties?.Count is null or 0);
            }

            return false;
        }

        /// <summary>
        /// Gets the C# type for the additionalProperties value.
        /// Returns "object" for untyped (additionalProperties: true).
        /// </summary>
        /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
        /// <returns>The C# type string for dictionary values.</returns>
        public string GetAdditionalPropertiesValueType(TypeConflictRegistry? registry = null)
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return "object";
            }

            return DictionaryValueType(actualSchema.AdditionalProperties, registry, depth: 0);
        }

        /// <summary>
        /// Gets the full Dictionary type string for a schema with additionalProperties.
        /// Returns null if the schema doesn't have additionalProperties.
        /// </summary>
        /// <param name="isRequired">Whether the property is required.</param>
        /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
        /// <returns>Full dictionary type string like "Dictionary&lt;string, bool&gt;" or null.</returns>
        public string? GetDictionaryTypeString(
            bool isRequired,
            TypeConflictRegistry? registry = null)
        {
            if (!schema.HasAdditionalProperties())
            {
                return null;
            }

            var valueType = schema.GetAdditionalPropertiesValueType(registry);
            var dictType = $"Dictionary<string, {valueType}>";

            return isRequired ? dictType : $"{dictType}?";
        }

        /// <summary>
        /// Gets the base schema name from an allOf composition (first $ref element).
        /// </summary>
        /// <returns>The reference ID of the base schema, or null.</returns>
        public string? GetAllOfBaseSchemaName()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return null;
            }

            if (actualSchema.AllOf is null || actualSchema.AllOf.Count == 0)
            {
                return null;
            }

            // Find the $ref schema (typically first in allOf)
            foreach (var subSchema in actualSchema.AllOf)
            {
                if (subSchema is OpenApiSchemaReference schemaRef)
                {
                    return schemaRef.Reference.Id ?? schemaRef.Id;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds an array property override in an allOf composition.
        /// Used for generic type detection (e.g., PaginatedResult with overridden results array).
        /// Resolves array aliases to their element types.
        /// </summary>
        /// <param name="document">The OpenAPI document for resolving references.</param>
        /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
        /// <returns>The C# type name of the array items, or null if not found.</returns>
        public string? GetAllOfArrayOverrideItemType(
            OpenApiDocument document,
            TypeConflictRegistry? registry = null)
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return null;
            }

            if (actualSchema.AllOf is null)
            {
                return null;
            }

            // Look for inline object schemas (not $ref) with array properties
            foreach (var subSchema in actualSchema.AllOf)
            {
                // Skip references - we want inline definitions that override base
                if (subSchema is OpenApiSchemaReference)
                {
                    continue;
                }

                if (subSchema is not OpenApiSchema inlineSchema)
                {
                    continue;
                }

                if (inlineSchema.Properties is null)
                {
                    continue;
                }

                // Find array property that overrides base schema's array
                foreach (var prop in inlineSchema.Properties)
                {
                    if (prop.Value is OpenApiSchema propSchema &&
                        propSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
                    {
                        // Get the array item type
                        var itemType = propSchema.GetArrayItemType(registry);

                        // If the item is a reference to an array schema, resolve it
                        itemType = IOpenApiSchema.ResolveArrayAliasType(itemType, document, registry);

                        return itemType;
                    }

                    // The override names an array alias component (results: $ref Accounts): its items are the page's items
                    if (prop.Value is OpenApiSchemaReference { Target: OpenApiSchema aliasSchema } &&
                        aliasSchema.IsOfType(JsonSchemaType.Array) &&
                        aliasSchema.Items is not null &&
                        !aliasSchema.HasPrefixItems())
                    {
                        return aliasSchema.GetArrayItemType(registry);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves array alias types to their element types.
        /// If the type name refers to an array schema (like "Accounts" which is array of Account),
        /// returns the element type ("Account") instead.
        /// </summary>
        private static string ResolveArrayAliasType(
            string typeName,
            OpenApiDocument document,
            TypeConflictRegistry? registry = null)
        {
            if (document.Components?.Schemas is null)
            {
                return typeName;
            }

            // Try to find the schema with this name
            if (!document.Components.Schemas.TryGetValue(typeName, out var schemaInterface))
            {
                return typeName;
            }

            // Check if it's an array schema
            if (schemaInterface is OpenApiSchema arraySchema && arraySchema.IsOfType(JsonSchemaType.Array))
            {
                // Return the array's item type instead
                return arraySchema.GetArrayItemType(registry);
            }

            return typeName;
        }

        /// <summary>
        /// Maps schema to C# type with allOf generic support.
        /// Returns PaginatedResult&lt;Account&gt; for allOf patterns with base schema and array override.
        /// </summary>
        /// <param name="document">The OpenAPI document for resolving references.</param>
        /// <param name="isRequired">Whether the type is required.</param>
        /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
        /// <returns>A C# type string, potentially generic.</returns>
        public string ToCSharpTypeWithGenericSupport(
            OpenApiDocument document,
            bool isRequired,
            TypeConflictRegistry? registry = null)
        {
            // Handle schema references first - this covers simple $ref cases
            if (schema is OpenApiSchemaReference schemaRef)
            {
                var refId = schemaRef.Reference.Id ?? schemaRef.Id;

                if (!string.IsNullOrEmpty(refId))
                {
                    // Check if this reference points to an array schema (array alias)
                    if (document.Components?.Schemas is not null &&
                        document.Components.Schemas.TryGetValue(refId!, out var resolvedSchema) &&
                        resolvedSchema is OpenApiSchema arraySchema && arraySchema.IsOfType(JsonSchemaType.Array) &&
                        arraySchema.Items is not null)
                    {
                        // It's an array alias - resolve to actual array type with type conflict resolution
                        var itemType = arraySchema.GetArrayItemType(registry);
                        return isRequired
                            ? $"{itemType}[]"
                            : $"{itemType}[]?";
                    }

                    // Simple reference - return the reference name
                    var typeName = ResolveTypeName(refId!, registry);
                    return isRequired
                        ? typeName
                        : $"{typeName}?";
                }
            }

            if (schema.HasAllOfComposition())
            {
                var baseSchemaName = schema.GetAllOfBaseSchemaName();
                var arrayItemType = schema.GetAllOfArrayOverrideItemType(document, registry);

                if (!string.IsNullOrEmpty(baseSchemaName) && !string.IsNullOrEmpty(arrayItemType))
                {
                    // Generate generic type: BaseSchema<ItemType>
                    var resolvedBaseName = ResolveTypeName(baseSchemaName, registry);
                    var genericType = $"{resolvedBaseName}<{arrayItemType}>";
                    return isRequired
                        ? genericType
                        : $"{genericType}?";
                }

                // If we have a base schema but no array override, return the base schema name
                if (!string.IsNullOrEmpty(baseSchemaName))
                {
                    var resolvedBaseName = ResolveTypeName(baseSchemaName, registry);
                    return isRequired
                        ? resolvedBaseName
                        : $"{resolvedBaseName}?";
                }
            }

            // Fall back to standard type resolution
            var baseType = schema.ToCSharpType(isRequired, registry);

            // Also resolve array aliases for simple references (e.g., "Accounts" -> "Account[]")
            if (baseType != "object" && !baseType.Contains("<") && !baseType.EndsWith("[]", StringComparison.Ordinal))
            {
                var resolvedType = IOpenApiSchema.ResolveArrayAliasToArrayType(baseType, document, registry);
                if (resolvedType != baseType)
                {
                    return isRequired
                        ? resolvedType
                        : $"{resolvedType}?";
                }
            }

            return baseType;
        }

        /// <summary>
        /// Resolves an array alias type name to its actual array type.
        /// If the type name refers to an array schema (like "Accounts" which is array of Account),
        /// returns "Account[]" instead of "Accounts".
        /// </summary>
        private static string ResolveArrayAliasToArrayType(
            string typeName,
            OpenApiDocument document,
            TypeConflictRegistry? registry = null)
        {
            // Remove nullable marker for lookup
            var baseTypeName = typeName.TrimEnd('?');

            if (document.Components?.Schemas is null)
            {
                return typeName;
            }

            // Try to find the schema with this name
            if (!document.Components.Schemas.TryGetValue(baseTypeName, out var schemaInterface))
            {
                return typeName;
            }

            // Check if it's an array schema
            if (schemaInterface is not OpenApiSchema arraySchema || !arraySchema.IsOfType(JsonSchemaType.Array))
            {
                return typeName;
            }

            // Return the array type with element type
            var itemType = arraySchema.GetArrayItemType(registry);
            return $"{itemType}[]";
        }

        /// <summary>
        /// Gets the list of interfaces from the x-implements OpenAPI extension.
        /// This extension allows specifying interfaces that a generated model should implement.
        /// </summary>
        /// <returns>A list of interface names, or an empty list if not specified.</returns>
        public IList<string> GetImplementedInterfaces()
        {
            var result = new List<string>();

            if (schema is not OpenApiSchema actualSchema)
            {
                return result;
            }

            // The x-implements extension: a list of names, or one text of comma-separated names
            var node = actualSchema.Extensions.NodeOf("x-implements");

            // Handle array of interface names
            if (node is JsonArray jsonArray)
            {
                foreach (var item in jsonArray)
                {
                    if (item is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var value) && !string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(value.Trim());
                    }
                }
            }

            // Handle single string value
            else if (node is JsonValue singleValue && singleValue.TryGetValue<string>(out var interfaceName) && !string.IsNullOrWhiteSpace(interfaceName))
            {
                // Support comma-separated list in single string
                var parts = interfaceName.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var trimmedPart = part.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedPart))
                    {
                        result.Add(trimmedPart);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the explicit x-pagination annotation from the schema.
        /// Returns true if marked as pagination, false if explicitly not, null if not annotated.
        /// </summary>
        /// <returns>True/false if explicitly annotated, null if not present (use heuristic fallback).</returns>
        public bool? GetPaginationAnnotation()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return null;
            }

            if (actualSchema.Extensions is null ||
                !actualSchema.Extensions.TryGetValue("x-pagination", out var extension) ||
                extension is not JsonNodeExtension jsonNodeExt)
            {
                return null;
            }

            if (jsonNodeExt.Node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            return null;
        }
    }

    /// <param name="schema">The OpenAPI schema representing an array.</param>
    extension(OpenApiSchema schema)
    {
        /// <summary>
        /// Gets the full array type name including brackets (e.g., "Pet[]", "string[]").
        /// </summary>
        /// <returns>The full array type name with brackets.</returns>
        public string GetArrayTypeName()
        {
            if (schema.Items is null)
            {
                return "object[]";
            }

            var itemType = schema.Items.GetCSharpTypeName();
            return $"{itemType}[]";
        }

        /// <summary>
        /// Gets the array item type from an OpenAPI schema.
        /// </summary>
        /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
        /// <returns>The C# type string for array items.</returns>
        public string GetArrayItemType(TypeConflictRegistry? registry = null)
        {
            switch (schema.Items)
            {
                case null:
                    return "object";

                // An item that names a primitive alias (ProductCode: a string) is that primitive; an array alias keeps
                // its name, which the callers unwrap to its own items (ResolveArrayAliasType)
                case OpenApiSchemaReference { Target: OpenApiSchema aliasTarget } when IsAliasSchema(aliasTarget) && !aliasTarget.IsOfType(JsonSchemaType.Array):
                    return aliasTarget.ToCSharpType(true, registry);
                case OpenApiSchemaReference itemRef:
                {
                    var itemRefName = itemRef.Reference.Id ?? "object";
                    return ResolveTypeName(itemRefName, registry);
                }

                case OpenApiSchema itemSchema:
                    return itemSchema.ToCSharpType(true, registry);
                default:
                    return "object";
            }
        }
    }

    // A type other than null ('nullable: true' alone says nothing about the value)
    private static bool HasValueType(JsonSchemaType? type)
        => type is { } value && (value | JsonSchemaType.Null) != JsonSchemaType.Null;

    // A schema that says nothing about the value ({}, or one with only a description)
    private static bool IsEmptySchema(IOpenApiSchema part)
    {
        if (part is not OpenApiSchema empty)
        {
            return false;
        }

        return !HasValueType(empty.Type) &&
               empty.Properties is null or { Count: 0 } &&
               empty.AllOf is null or { Count: 0 } &&
               empty.OneOf is null or { Count: 0 } &&
               empty.AnyOf is null or { Count: 0 } &&
               empty.Enum is null or { Count: 0 } &&
               empty.Items is null &&
               string.IsNullOrEmpty(empty.Format);
    }

    // The C# type of a dictionary's value: a referenced model by its name, a primitive by its type, an array as a
    // List of its item type, a nested map as a Dictionary - anything else (untyped, an inline object) object
    private static string DictionaryValueType(
        IOpenApiSchema? value,
        TypeConflictRegistry? registry,
        int depth)
    {
        if (value is null || depth > 16)
        {
            return "object";
        }

        if (value is OpenApiSchemaReference reference)
        {
            return ResolveTypeName(reference.Reference?.Id ?? reference.Id ?? "object", registry);
        }

        if (value is not OpenApiSchema inline || !HasValueType(inline.Type))
        {
            return "object";
        }

        if (inline.Type!.Value.HasFlag(JsonSchemaType.Array))
        {
            return $"List<{DictionaryValueType(inline.Items, registry, depth + 1)}>";
        }

        if (inline.Type.Value.HasFlag(JsonSchemaType.Object))
        {
            return inline.AdditionalProperties is not null && inline.Properties is null or { Count: 0 }
                ? $"Dictionary<string, {DictionaryValueType(inline.AdditionalProperties, registry, depth + 1)}>"
                : "object";
        }

        return inline.Type.ToPrimitiveCSharpTypeName(inline.Format) ?? "object";
    }
}