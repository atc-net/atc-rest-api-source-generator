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
    /// Replaces dots with underscores (e.g., "Foo.Bar.Baz" becomes "Foo_Bar_Baz").
    /// </summary>
    /// <param name="schemaName">The original schema name from OpenAPI.</param>
    /// <returns>A sanitized name that is a valid C# identifier.</returns>
    public static string SanitizeSchemaName(string schemaName)
        => string.IsNullOrEmpty(schemaName)
            ? schemaName
            : schemaName.Replace(".", "_"); // Replace dots with underscores for valid C# identifiers

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

    /// <param name="schema">The OpenAPI schema interface.</param>
    extension(IOpenApiSchema schema)
    {
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
                OpenApiSchema actualSchema => actualSchema.Type.ToPrimitiveCSharpTypeName(actualSchema.Format) ?? "object",
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

            var schemaType = openApiSchema.Type;
            if (schemaType == null)
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
            return typeString != null && typeString.Contains("Null", StringComparison.OrdinalIgnoreCase);
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
            if (schemaType == null)
            {
                return false;
            }

            return schemaType.Value.CountNonNullTypes() > 1;
        }

        /// <summary>
        /// Gets the count of non-null types in the schema.
        /// </summary>
        /// <returns>The count of non-null types, or 0 if no type is defined.</returns>
        public int GetNonNullTypeCount()
        {
            if (schema is not OpenApiSchema openApiSchema)
            {
                return 0;
            }

            return openApiSchema.Type?.CountNonNullTypes() ?? 0;
        }

        /// <summary>
        /// Gets all non-null type names from the schema.
        /// </summary>
        /// <returns>A list of type names (e.g., ["string", "integer"]), or empty list.</returns>
        public IReadOnlyList<string> GetAllNonNullTypeNames()
        {
            if (schema is not OpenApiSchema openApiSchema)
            {
                return Array.Empty<string>();
            }

            return openApiSchema.Type?.GetNonNullTypeNames() ?? Array.Empty<string>();
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
        /// Gets the effective description for a schema, considering OpenAPI 3.1 $ref with sibling support.
        /// In OpenAPI 3.1, a description alongside a $ref overrides the referenced schema's description.
        /// </summary>
        /// <param name="document">The OpenAPI document for resolving references.</param>
        /// <returns>The effective description, prioritizing sibling properties over target.</returns>
        public string? GetEffectiveDescription(OpenApiDocument document)
        {
            // If schema has its own description (sibling property), use it
            if (schema is OpenApiSchemaReference schemaRef)
            {
                // Check if the reference has a sibling description
                if (!string.IsNullOrEmpty(schemaRef.Description))
                {
                    return schemaRef.Description;
                }

                // Fall back to target schema's description
                var target = schema.ResolveSchema(document);
                return target?.Description;
            }

            // For non-references, just return the schema's description
            if (schema is OpenApiSchema actualSchema)
            {
                return actualSchema.Description;
            }

            return null;
        }

        /// <summary>
        /// Gets the effective deprecated status for a schema, considering OpenAPI 3.1 $ref with sibling support.
        /// In OpenAPI 3.1, a deprecated flag alongside a $ref can override the referenced schema's status.
        /// </summary>
        /// <param name="document">The OpenAPI document for resolving references.</param>
        /// <returns>True if the schema is effectively deprecated.</returns>
        public bool IsEffectivelyDeprecated(OpenApiDocument document)
        {
            // If schema has its own deprecated flag (sibling property), use it
            if (schema is OpenApiSchemaReference schemaRef)
            {
                // Check if the reference is marked deprecated
                if (schemaRef.Deprecated)
                {
                    return true;
                }

                // Fall back to target schema's deprecated status
                var target = schema.ResolveSchema(document);
                return target?.Deprecated ?? false;
            }

            // For non-references, just return the schema's deprecated status
            if (schema is OpenApiSchema actualSchema)
            {
                return actualSchema.Deprecated;
            }

            return false;
        }

        /// <summary>
        /// Gets the effective default value for a schema, considering OpenAPI 3.1 $ref with sibling support.
        /// In OpenAPI 3.1, a default value alongside a $ref overrides the referenced schema's default.
        /// </summary>
        /// <param name="document">The OpenAPI document for resolving references.</param>
        /// <returns>The effective default value, or null if none.</returns>
        public JsonNode? GetEffectiveDefault(OpenApiDocument document)
        {
            // If schema has its own default (sibling property), use it
            if (schema is OpenApiSchemaReference schemaRef)
            {
                // Check if the reference has a sibling default
                if (schemaRef.Default != null)
                {
                    return schemaRef.Default;
                }

                // Fall back to target schema's default
                var target = schema.ResolveSchema(document);
                return target?.Default;
            }

            // For non-references, just return the schema's default
            if (schema is OpenApiSchema actualSchema)
            {
                return actualSchema.Default;
            }

            return null;
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
                   schemaRef.Default != null;
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
            if (actualSchema.UnrecognizedKeywords != null &&
                actualSchema.UnrecognizedKeywords.TryGetValue("contentEncoding", out var value))
            {
                return value?.ToString();
            }

            return null;
        }

        /// <summary>
        /// Gets the contentMediaType value from the schema (JSON Schema 2020-12 / OpenAPI 3.1).
        /// ContentMediaType specifies the media type of the encoded content (e.g., "application/json", "image/png").
        /// </summary>
        /// <returns>The content media type, or null if not specified.</returns>
        public string? GetContentMediaType()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return null;
            }

            // Check UnrecognizedKeywords for contentMediaType
            if (actualSchema.UnrecognizedKeywords != null &&
                actualSchema.UnrecognizedKeywords.TryGetValue("contentMediaType", out var value))
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

            // prefixItems is stored in Extensions since Microsoft.OpenApi doesn't have native support
            return actualSchema.Extensions != null &&
                   actualSchema.Extensions.ContainsKey("prefixItems");
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
            if (actualSchema.Extensions != null &&
                actualSchema.Extensions.TryGetValue("items", out var itemsExt))
            {
                // Check if items is explicitly false
                return IsExtensionValueFalse(itemsExt);
            }

            // If no items extension and Items schema is null, treat as strict
            return actualSchema.Items == null;
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

            if (actualSchema.Extensions == null ||
                !actualSchema.Extensions.TryGetValue("prefixItems", out var extension))
            {
                return null;
            }

            var prefixItems = ExtractPrefixItemsFromExtension(extension, document, registry);
            if (prefixItems.Count == 0)
            {
                return null;
            }

            var isStrictTuple = schema.IsStrictTuple();
            string? additionalItemsType = null;

            // If not strict and has items schema, get the additional items type
            if (!isStrictTuple && actualSchema.Items != null)
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
            if (extension == null)
            {
                return false;
            }

            // Try to get the value using reflection (OpenApiAny wraps JsonNode)
            var extensionType = extension.GetType();

            // Check for OpenApiBoolean or similar
            var valueProperty = extensionType.GetProperty("Value");
            if (valueProperty != null)
            {
                var value = valueProperty.GetValue(extension);
                if (value is bool boolValue)
                {
                    return !boolValue;
                }

                if (value is JsonNode jsonNode && jsonNode.GetValueKind() == JsonValueKind.False)
                {
                    return true;
                }
            }

            // Check Node property (for OpenApiAny)
            var nodeProperty = extensionType.GetProperty("Node");
            if (nodeProperty != null)
            {
                var node = nodeProperty.GetValue(extension);
                if (node is JsonNode jsonNode && jsonNode.GetValueKind() == JsonValueKind.False)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts prefixItems schemas from an extension value.
        /// </summary>
        private static List<TupleItemInfo> ExtractPrefixItemsFromExtension(
            IOpenApiExtension extension,
            OpenApiDocument? document,
            TypeConflictRegistry? registry)
        {
            var result = new List<TupleItemInfo>();

            // Try to get the underlying JsonArray from the extension
            var extensionType = extension.GetType();
            var nodeProperty = extensionType.GetProperty("Node");
            if (nodeProperty == null)
            {
                return result;
            }

            var node = nodeProperty.GetValue(extension);
            if (node is not JsonArray jsonArray)
            {
                return result;
            }

            var index = 0;
            foreach (var item in jsonArray)
            {
                if (item is JsonObject schemaObj)
                {
                    var itemInfo = ParseTupleItemFromJson(schemaObj, index, document, registry);
                    result.Add(itemInfo);
                }

                index++;
            }

            return result;
        }

        /// <summary>
        /// Parses a tuple item from a JSON schema object.
        /// </summary>
        private static TupleItemInfo ParseTupleItemFromJson(
            JsonObject schemaObj,
            int index,
            OpenApiDocument? document,
            TypeConflictRegistry? registry)
        {
            // Extract type info
            var typeStr = schemaObj["type"]?.GetValue<string>() ?? "object";
            var format = schemaObj["format"]?.GetValue<string>();
            var description = schemaObj["description"]?.GetValue<string>();
            var nullable = schemaObj["nullable"]?.GetValue<bool>() ?? false;

            // Handle $ref
            var refStr = schemaObj["$ref"]?.GetValue<string>();
            string csharpType;
            if (!string.IsNullOrEmpty(refStr))
            {
                var refName = refStr
                    .Split('/')
                    .Last();
                csharpType = ResolveTypeName(refName, registry);
            }
            else
            {
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
                "string" when format == "date" => "DateTimeOffset",
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
        /// Resolves a schema reference to the actual schema from the document.
        /// </summary>
        /// <param name="document">The OpenAPI document containing schema definitions.</param>
        /// <returns>The resolved OpenApiSchema or null.</returns>
        public OpenApiSchema? ResolveSchema(OpenApiDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var refId = schema.GetReferenceId();
            if (refId == null)
            {
                return schema as OpenApiSchema;
            }

            if (document.Components?.Schemas != null &&
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

            // Handle actual schemas (not references)
            if (schema is not OpenApiSchema actualSchema)
            {
                return attributes;
            }

            // Range validation for numeric types (use HasFlag for flags enum compatibility)
            var schemaType = actualSchema.Type ?? JsonSchemaType.Null;
            if (schemaType.HasFlag(JsonSchemaType.Integer))
            {
                var hasMin = !string.IsNullOrEmpty(actualSchema.Minimum);
                var hasMax = !string.IsNullOrEmpty(actualSchema.Maximum);

                if (hasMin && hasMax)
                {
                    var min = long.Parse(actualSchema.Minimum!, CultureInfo.InvariantCulture);
                    var max = long.Parse(actualSchema.Maximum!, CultureInfo.InvariantCulture);
                    attributes.Add($"Range({min}, {max})");
                }
                else if (hasMin)
                {
                    var min = long.Parse(actualSchema.Minimum!, CultureInfo.InvariantCulture);
                    attributes.Add($"Range({min}, long.MaxValue)");
                }
                else if (hasMax)
                {
                    var max = long.Parse(actualSchema.Maximum!, CultureInfo.InvariantCulture);
                    attributes.Add($"Range(long.MinValue, {max})");
                }
            }
            else if (schemaType.HasFlag(JsonSchemaType.Number))
            {
                var hasMin = !string.IsNullOrEmpty(actualSchema.Minimum);
                var hasMax = !string.IsNullOrEmpty(actualSchema.Maximum);

                if (hasMin && hasMax)
                {
                    var min = double.Parse(actualSchema.Minimum!, CultureInfo.InvariantCulture);
                    var max = double.Parse(actualSchema.Maximum!, CultureInfo.InvariantCulture);
                    attributes.Add($"Range({min.ToString(CultureInfo.InvariantCulture)}, {max.ToString(CultureInfo.InvariantCulture)})");
                }
                else if (hasMin)
                {
                    var min = double.Parse(actualSchema.Minimum!, CultureInfo.InvariantCulture);
                    attributes.Add($"Range({min.ToString(CultureInfo.InvariantCulture)}, double.MaxValue)");
                }
                else if (hasMax)
                {
                    var max = double.Parse(actualSchema.Maximum!, CultureInfo.InvariantCulture);
                    attributes.Add($"Range(double.MinValue, {max.ToString(CultureInfo.InvariantCulture)})");
                }
            }

            // String length validation - use separate MinLength/MaxLength (match old generator style)
            if (schemaType.HasFlag(JsonSchemaType.String))
            {
                if (actualSchema.MinLength > 0)
                {
                    attributes.Add($"MinLength({actualSchema.MinLength})");
                }

                if (actualSchema.MaxLength > 0)
                {
                    attributes.Add($"MaxLength({actualSchema.MaxLength})");
                }

                // Regular expression validation
                if (!string.IsNullOrWhiteSpace(actualSchema.Pattern))
                {
                    // Use verbatim string literal (@"...") to handle backslashes in regex patterns
                    // In verbatim strings, double quotes are escaped by doubling them
                    var escapedPattern = actualSchema.Pattern.Replace("\"", "\"\"");
                    attributes.Add($"RegularExpression(@\"{escapedPattern}\")");
                }

                // Format-based validation attributes
                if (!string.IsNullOrEmpty(actualSchema.Format))
                {
                    switch (actualSchema.Format.ToLowerInvariant())
                    {
                        case "email":
                            attributes.Add("EmailAddress");
                            break;
                    }
                }
            }

            // Array length validation
            if (schemaType.HasFlag(JsonSchemaType.Array))
            {
                var hasMinItems = actualSchema.MinItems > 0;
                var hasMaxItems = actualSchema.MaxItems > 0;

                if (hasMinItems)
                {
                    attributes.Add($"MinLength({actualSchema.MinItems})");
                }

                if (hasMaxItems)
                {
                    attributes.Add($"MaxLength({actualSchema.MaxItems})");
                }
            }

            return attributes;
        }

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
                return isRequired ? $"{itemType}[]" : $"{itemType}[]?";
            }

            // Handle primitive types using HasFlag for combined flags (e.g., String | Null)
            var baseType = schema1.Type.ToCSharpTypeName(schema1.Format, includeIFormFile: true);

            // For parameters: optional value types and strings should be nullable
            if (!isRequired && (CSharpTypeHelper.IsExtendedValueType(baseType) || baseType == "string"))
            {
                return $"{baseType}?";
            }

            return baseType;
        }

        /// <summary>
        /// Maps an OpenAPI schema to a C# type string for model properties.
        /// Only properties with nullable: true in the schema are made nullable.
        /// The isRequired parameter only affects value types (which need ? to be optional).
        /// </summary>
        /// <param name="isRequired">Whether the property is in the required array.</param>
        /// <param name="registry">Optional conflict registry for detecting naming conflicts.</param>
        /// <returns>A C# type string representation.</returns>
        public string ToCSharpTypeForModel(
            bool isRequired,
            TypeConflictRegistry? registry = null)
        {
            // Handle schema references - references are never nullable based on required alone
            if (schema is OpenApiSchemaReference schemaRef)
            {
                var refName = schemaRef.Reference.Id ?? schemaRef.Id ?? "object";
                return ResolveTypeName(refName, registry);
            }

            // Handle actual schemas
            if (schema is not OpenApiSchema schema1)
            {
                return "object";
            }

            // Check if schema has nullable: true (in OpenAPI 3.1/Microsoft.OpenApi 3.0.x, this is indicated by JsonSchemaType.Null flag)
            var isNullable = schema1.IsNullable();

            // Handle base64-encoded content (format: byte or contentEncoding: base64)
            // Must check before generic type mapping since both result in string type
            if (schema.ShouldMapToByteArray())
            {
                return isNullable ? "byte[]?" : "byte[]";
            }

            // Handle array types - check using HasFlag for combined flags
            if (schema1.Type?.HasFlag(JsonSchemaType.Array) == true)
            {
                var itemType = schema1.GetArrayItemType(registry);
                return isNullable ? $"{itemType}[]?" : $"{itemType}[]";
            }

            // Handle primitive types using HasFlag for combined flags (e.g., String | Null)
            var baseType = schema1.Type.ToCSharpTypeName(schema1.Format, includeIFormFile: true);

            // For models: all types (value and reference) are only nullable if nullable: true is set in the schema
            // The required array means "must be present in JSON", not "cannot be null"
            return isNullable ? $"{baseType}?" : baseType;
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
        /// Gets the discriminator property name from a polymorphic schema.
        /// </summary>
        /// <returns>The discriminator property name, or null if not defined.</returns>
        public string? GetDiscriminatorPropertyName()
        {
            if (schema is not OpenApiSchema actualSchema)
            {
                return null;
            }

            return actualSchema.Discriminator?.PropertyName;
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
            if (mapping == null || mapping.Count == 0)
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
            if (actualSchema.OneOf != null)
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
            if (actualSchema.AnyOf != null)
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
            if (document == null)
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
                if (document.Components?.Schemas == null ||
                    !document.Components.Schemas.TryGetValue(variantName, out var variantSchema))
                {
                    continue;
                }

                if (variantSchema is not OpenApiSchema actualVariant || actualVariant.Properties == null)
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

            // If no priority name found, return the first common string property
            return commonProps.Count > 0 ? commonProps.First() : null;
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

            return actualSchema.AdditionalProperties != null;
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

            if (actualSchema.AdditionalProperties == null)
            {
                return false;
            }

            // When additionalProperties: true, the schema has no type defined or is empty
            if (actualSchema.AdditionalProperties is OpenApiSchema addPropsSchema)
            {
                // No type means "any" (additionalProperties: true)
                // Also check for empty object schema
                return addPropsSchema.Type == null ||
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

            if (actualSchema.AdditionalProperties == null)
            {
                return "object";
            }

            // Handle schema reference
            if (actualSchema.AdditionalProperties is OpenApiSchemaReference schemaRef)
            {
                var refName = schemaRef.Reference?.Id ?? schemaRef.Id ?? "object";
                return ResolveTypeName(refName, registry);
            }

            // Handle inline schema
            if (actualSchema.AdditionalProperties is OpenApiSchema addPropsSchema)
            {
                // Untyped (additionalProperties: true) or empty schema
                if (addPropsSchema.Type == null ||
                    addPropsSchema.Type == JsonSchemaType.Null ||
                    (addPropsSchema.Type.Value == default && addPropsSchema.Properties?.Count is null or 0))
                {
                    return "object";
                }

                // Typed (additionalProperties: { type: ... })
                return addPropsSchema.Type.ToPrimitiveCSharpTypeName(addPropsSchema.Format) ?? "object";
            }

            return "object";
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

            if (actualSchema.AllOf == null || actualSchema.AllOf.Count == 0)
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

            if (actualSchema.AllOf == null)
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

                if (inlineSchema.Properties == null)
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
                        itemType = ResolveArrayAliasType(itemType, document, registry);

                        return itemType;
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
            if (document.Components?.Schemas == null)
            {
                return typeName;
            }

            // Try to find the schema with this name
            if (!document.Components.Schemas.TryGetValue(typeName, out var schemaInterface))
            {
                return typeName;
            }

            // Check if it's an array schema
            if (schemaInterface is OpenApiSchema { Type: JsonSchemaType.Array } arraySchema)
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
                    if (document.Components?.Schemas != null &&
                        document.Components.Schemas.TryGetValue(refId!, out var resolvedSchema) &&
                        resolvedSchema is OpenApiSchema { Type: JsonSchemaType.Array } arraySchema &&
                        arraySchema.Items != null)
                    {
                        // It's an array alias - resolve to actual array type with type conflict resolution
                        var itemType = arraySchema.GetArrayItemType(registry);
                        return isRequired ? $"{itemType}[]" : $"{itemType}[]?";
                    }

                    // Simple reference - return the reference name
                    var typeName = ResolveTypeName(refId!, registry);
                    return isRequired ? typeName : $"{typeName}?";
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
                    return isRequired ? genericType : $"{genericType}?";
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
                var resolvedType = ResolveArrayAliasToArrayType(baseType, document, registry);
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

            if (document.Components?.Schemas == null)
            {
                return typeName;
            }

            // Try to find the schema with this name
            if (!document.Components.Schemas.TryGetValue(baseTypeName, out var schemaInterface))
            {
                return typeName;
            }

            // Check if it's an array schema
            if (schemaInterface is not OpenApiSchema { Type: JsonSchemaType.Array } arraySchema)
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

            // Look for x-implements extension
            if (actualSchema.Extensions == null ||
                !actualSchema.Extensions.TryGetValue("x-implements", out var extension))
            {
                return result;
            }

            // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
            var extensionType = extension.GetType();
            var nodeProperty = extensionType.GetProperty("Node");
            if (nodeProperty == null)
            {
                return result;
            }

            var node = nodeProperty.GetValue(extension);

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
            if (schema.Items == null)
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
}