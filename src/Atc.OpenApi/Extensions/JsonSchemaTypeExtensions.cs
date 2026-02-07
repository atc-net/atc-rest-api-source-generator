namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension methods for JsonSchemaType to handle C# type name resolution.
/// </summary>
public static class JsonSchemaTypeExtensions
{
    /// <summary>
    /// Maps a JsonSchemaType and format to a C# type name.
    /// Handles combined type flags (e.g., String | Null in OpenAPI 3.1).
    /// </summary>
    /// <param name="schemaType">The JSON schema type (can be combined flags like String | Null).</param>
    /// <param name="format">Optional format string (e.g., "int64", "uuid", "date-time").</param>
    /// <param name="includeIFormFile">When true, maps "binary" format to IFormFile instead of string.</param>
    /// <returns>The C# type name, or "object" if type cannot be determined.</returns>
    public static string ToCSharpTypeName(
        this JsonSchemaType schemaType,
        string? format = null,
        bool includeIFormFile = false)
        => ((JsonSchemaType?)schemaType).ToCSharpTypeName(format, includeIFormFile);

    /// <summary>
    /// Maps a nullable JsonSchemaType and format to a C# type name.
    /// Handles combined type flags (e.g., String | Null in OpenAPI 3.1).
    /// </summary>
    /// <param name="schemaType">The JSON schema type (can be combined flags like String | Null).</param>
    /// <param name="format">Optional format string (e.g., "int64", "uuid", "date-time").</param>
    /// <param name="includeIFormFile">When true, maps "binary" format to IFormFile instead of string.</param>
    /// <returns>The C# type name, or "object" if type cannot be determined.</returns>
    public static string ToCSharpTypeName(
        this JsonSchemaType? schemaType,
        string? format = null,
        bool includeIFormFile = false)
    {
        if (schemaType == null)
        {
            return "object";
        }

        var typeValue = schemaType.Value;

        // Strip the Null flag for matching (JsonSchemaType is a flags enum in OpenAPI 3.1.0)
        if (typeValue.HasFlag(JsonSchemaType.Null))
        {
            typeValue &= ~JsonSchemaType.Null;
        }

        // Check for specific types using HasFlag (handles combined flags)
        if (typeValue.HasFlag(JsonSchemaType.Integer))
        {
            return GetIntegerTypeName(format);
        }

        if (typeValue.HasFlag(JsonSchemaType.Number))
        {
            return GetNumberTypeName(format);
        }

        if (typeValue.HasFlag(JsonSchemaType.String))
        {
            return GetStringTypeName(format, includeIFormFile);
        }

        if (typeValue.HasFlag(JsonSchemaType.Boolean))
        {
            return "bool";
        }

        if (typeValue.HasFlag(JsonSchemaType.Array))
        {
            return "object[]";
        }

        return "object";
    }

    /// <summary>
    /// Maps a JsonSchemaType to a primitive C# type name, or null for Array types.
    /// This allows callers to handle array types specially (e.g., determining item type).
    /// </summary>
    /// <param name="schemaType">The JSON schema type (can be combined flags like String | Null).</param>
    /// <param name="format">Optional format string (e.g., "int64", "uuid", "date-time").</param>
    /// <param name="includeIFormFile">When true, maps "binary" format to IFormFile instead of string.</param>
    /// <returns>The C# type name, null for Array types, or "object" for unknown types.</returns>
    public static string? ToPrimitiveCSharpTypeName(
        this JsonSchemaType? schemaType,
        string? format = null,
        bool includeIFormFile = false)
    {
        if (schemaType == null)
        {
            return "object";
        }

        var typeValue = schemaType.Value;

        // Strip the Null flag for matching (JsonSchemaType is a flags enum in OpenAPI 3.1.0)
        if (typeValue.HasFlag(JsonSchemaType.Null))
        {
            typeValue &= ~JsonSchemaType.Null;
        }

        // Array types return null - caller must handle
        if (typeValue.HasFlag(JsonSchemaType.Array))
        {
            return null;
        }

        // Check for specific types using HasFlag (handles combined flags)
        if (typeValue.HasFlag(JsonSchemaType.Integer))
        {
            return GetIntegerTypeName(format);
        }

        if (typeValue.HasFlag(JsonSchemaType.Number))
        {
            return GetNumberTypeName(format);
        }

        if (typeValue.HasFlag(JsonSchemaType.String))
        {
            return GetStringTypeName(format, includeIFormFile);
        }

        if (typeValue.HasFlag(JsonSchemaType.Boolean))
        {
            return "bool";
        }

        return "object";
    }

    /// <summary>
    /// Gets the C# type name for Integer JsonSchemaType with format.
    /// </summary>
    /// <param name="format">The format specifier (e.g., "int32", "int64").</param>
    /// <returns>The C# integer type name.</returns>
    private static string GetIntegerTypeName(string? format)
        => format switch
        {
            "int32" => "int",
            "int64" => "long",
            _ => "int",
        };

    /// <summary>
    /// Gets the C# type name for Number JsonSchemaType with format.
    /// </summary>
    /// <param name="format">The format specifier (e.g., "float", "double", "int32", "int64").</param>
    /// <returns>The C# number type name.</returns>
    private static string GetNumberTypeName(string? format)
        => format switch
        {
            "int32" => "int",
            "int64" => "long",
            "float" => "float",
            "double" => "double",
            _ => "double",
        };

    /// <summary>
    /// Gets the C# type name for String JsonSchemaType with format.
    /// </summary>
    /// <param name="format">The format specifier (e.g., "uuid", "date-time", "uri", "binary", "byte").</param>
    /// <param name="includeIFormFile">When true, maps "binary" format to IFormFile.</param>
    /// <param name="contentEncoding">Optional contentEncoding value (e.g., "base64", "base64url").</param>
    /// <returns>The C# string type name.</returns>
    private static string GetStringTypeName(
        string? format,
        bool includeIFormFile,
        string? contentEncoding = null)
    {
        // Check contentEncoding first (OpenAPI 3.1 / JSON Schema 2020-12)
        if (IsBase64ContentEncoding(contentEncoding))
        {
            return "byte[]";
        }

        return format?.ToLowerInvariant() switch
        {
            "binary" when includeIFormFile => "IFormFile",
            "byte" => "byte[]", // base64 encoded content - System.Text.Json handles serialization
            "uuid" => "Guid",
            "guid" => "Guid",
            "date-time" => "DateTimeOffset",
            "date" => "DateTimeOffset",
            "uri" => "Uri",
            "int32" => "int",
            "int" => "int",
            "int64" => "long",
            "long" => "long",
            _ => "string",
        };
    }

    /// <summary>
    /// Checks if the contentEncoding indicates base64-encoded content.
    /// </summary>
    /// <param name="contentEncoding">The contentEncoding value.</param>
    /// <returns>True if the content is base64-encoded.</returns>
    private static bool IsBase64ContentEncoding(string? contentEncoding)
        => contentEncoding?.ToLowerInvariant() is "base64" or "base64url";

    /// <summary>
    /// Counts the number of non-null types in a JsonSchemaType flags value.
    /// OpenAPI 3.1 supports type arrays like ["string", "integer", "null"].
    /// </summary>
    /// <param name="schemaType">The JSON schema type (can be combined flags).</param>
    /// <returns>The count of non-null types.</returns>
    public static int CountNonNullTypes(this JsonSchemaType schemaType)
    {
        var count = 0;
        var typeValue = schemaType;

        // Strip the Null flag
        if (typeValue.HasFlag(JsonSchemaType.Null))
        {
            typeValue &= ~JsonSchemaType.Null;
        }

        // Count each type flag
        if (typeValue.HasFlag(JsonSchemaType.String))
        {
            count++;
        }

        if (typeValue.HasFlag(JsonSchemaType.Integer))
        {
            count++;
        }

        if (typeValue.HasFlag(JsonSchemaType.Number))
        {
            count++;
        }

        if (typeValue.HasFlag(JsonSchemaType.Boolean))
        {
            count++;
        }

        if (typeValue.HasFlag(JsonSchemaType.Array))
        {
            count++;
        }

        if (typeValue.HasFlag(JsonSchemaType.Object))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Counts the number of non-null types in a nullable JsonSchemaType.
    /// </summary>
    /// <param name="schemaType">The nullable JSON schema type.</param>
    /// <returns>The count of non-null types, or 0 if null.</returns>
    public static int CountNonNullTypes(this JsonSchemaType? schemaType)
        => schemaType?.CountNonNullTypes() ?? 0;

    /// <summary>
    /// Gets the primary (first selected) non-null type from a combined JsonSchemaType.
    /// Uses priority order: String, Integer, Number, Boolean, Array, Object.
    /// </summary>
    /// <param name="schemaType">The JSON schema type (can be combined flags).</param>
    /// <returns>The primary type, or null if no types are set.</returns>
    public static JsonSchemaType? GetPrimaryType(this JsonSchemaType schemaType)
    {
        var typeValue = schemaType;

        // Strip the Null flag
        if (typeValue.HasFlag(JsonSchemaType.Null))
        {
            typeValue &= ~JsonSchemaType.Null;
        }

        // Return first type in priority order
        if (typeValue.HasFlag(JsonSchemaType.String))
        {
            return JsonSchemaType.String;
        }

        if (typeValue.HasFlag(JsonSchemaType.Integer))
        {
            return JsonSchemaType.Integer;
        }

        if (typeValue.HasFlag(JsonSchemaType.Number))
        {
            return JsonSchemaType.Number;
        }

        if (typeValue.HasFlag(JsonSchemaType.Boolean))
        {
            return JsonSchemaType.Boolean;
        }

        if (typeValue.HasFlag(JsonSchemaType.Array))
        {
            return JsonSchemaType.Array;
        }

        if (typeValue.HasFlag(JsonSchemaType.Object))
        {
            return JsonSchemaType.Object;
        }

        return null;
    }

    /// <summary>
    /// Gets the primary type from a nullable JsonSchemaType.
    /// </summary>
    /// <param name="schemaType">The nullable JSON schema type.</param>
    /// <returns>The primary type, or null if input is null or no types are set.</returns>
    public static JsonSchemaType? GetPrimaryType(this JsonSchemaType? schemaType)
        => schemaType?.GetPrimaryType();

    /// <summary>
    /// Gets all non-null type names from a combined JsonSchemaType.
    /// </summary>
    /// <param name="schemaType">The JSON schema type (can be combined flags).</param>
    /// <returns>A list of type names (e.g., ["string", "integer"]).</returns>
    public static IReadOnlyList<string> GetNonNullTypeNames(this JsonSchemaType schemaType)
    {
        var types = new List<string>();
        var typeValue = schemaType;

        // Strip the Null flag
        if (typeValue.HasFlag(JsonSchemaType.Null))
        {
            typeValue &= ~JsonSchemaType.Null;
        }

        if (typeValue.HasFlag(JsonSchemaType.String))
        {
            types.Add("string");
        }

        if (typeValue.HasFlag(JsonSchemaType.Integer))
        {
            types.Add("integer");
        }

        if (typeValue.HasFlag(JsonSchemaType.Number))
        {
            types.Add("number");
        }

        if (typeValue.HasFlag(JsonSchemaType.Boolean))
        {
            types.Add("boolean");
        }

        if (typeValue.HasFlag(JsonSchemaType.Array))
        {
            types.Add("array");
        }

        if (typeValue.HasFlag(JsonSchemaType.Object))
        {
            types.Add("object");
        }

        return types;
    }

    /// <summary>
    /// Gets all non-null type names from a nullable JsonSchemaType.
    /// </summary>
    /// <param name="schemaType">The nullable JSON schema type.</param>
    /// <returns>A list of type names, or an empty list if null.</returns>
    public static IReadOnlyList<string> GetNonNullTypeNames(this JsonSchemaType? schemaType)
        => schemaType?.GetNonNullTypeNames() ?? [];
}
