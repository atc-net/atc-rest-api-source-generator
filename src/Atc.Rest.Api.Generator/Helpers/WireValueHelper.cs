namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The text a parameter value has on the wire (path, query string, header). Dates, times and
/// decimal numbers must not be formatted with the culture of the machine the client runs on:
/// <c>ToString()</c> gives <c>28-02-1990</c> and <c>1,5</c> under a Danish culture.
/// </summary>
public static class WireValueHelper
{
    private const string Invariant = "global::System.Globalization.CultureInfo.InvariantCulture";

    // How deep a schema walk goes before it gives up: far beyond any real nesting, short of a stack overflow
    private const int MaxDepth = 64;

    /// <summary>
    /// Whether a parameter's schema is a string enum (a component or an inline one), or an array of one. Such a
    /// value is written with the generated <c>ToWireValue()</c>: the value as the specification writes it.
    /// </summary>
    /// <param name="schema">The parameter's schema.</param>
    /// <returns><see langword="true"/> for a string enum or an array of string enums.</returns>
    public static bool IsStringEnum(IOpenApiSchema? schema)
        => IsStringEnum(schema, depth: 0);

    /// <summary>
    /// Whether a parameter's schema is an integer enum (a component or an inline one). Such a value is its number on
    /// the wire - the member's name is a C# name the specification does not know.
    /// </summary>
    /// <param name="schema">The parameter's schema.</param>
    /// <returns><see langword="true"/> for an integer enum.</returns>
    public static bool IsIntegerEnum(IOpenApiSchema? schema)
    {
        var actual = schema is OpenApiSchemaReference reference ? reference.Target : schema;
        return actual is not null &&
               actual.Enum is { Count: > 0 } &&
               actual.IsOfType(JsonSchemaType.Integer);
    }

    /// <summary>
    /// A C# expression of type <c>string?</c> that writes an enum value as its number (<c>2</c>, not <c>Medium</c>).
    /// </summary>
    /// <param name="accessExpression">The expression that reads the value, nullable or not.</param>
    /// <returns>The expression.</returns>
    public static string IntegerEnumWireExpression(string accessExpression)
        => $"((global::System.IFormattable?){accessExpression})?.ToString(\"D\", {Invariant})";

    /// <summary>
    /// A C# expression that percent-encodes a string for an <c>allowReserved</c> query value: the RFC 3986 reserved
    /// characters stay as they are (<c>:/?[]@!$&amp;'()*+,;=</c>), everything else is encoded - and <c>#</c> always, as
    /// a raw one would cut the URL at the fragment.
    /// </summary>
    /// <param name="valueExpression">An expression of type <c>string</c>.</param>
    /// <returns>The expression.</returns>
    public static string AllowReservedEscapeExpression(string valueExpression)
        => $"(global::System.Text.RegularExpressions.Regex.Replace(global::System.Uri.EscapeDataString({valueExpression}), \"%(3A|2F|3F|5B|5D|40|21|24|26|27|28|29|2A|2B|2C|3B|3D)\", m => global::System.Uri.UnescapeDataString(m.Value), global::System.Text.RegularExpressions.RegexOptions.None, global::System.TimeSpan.FromSeconds(1)))";

    /// <summary>
    /// A C# expression of type <c>string?</c> that writes a boolean as the wire does: <c>true</c> / <c>false</c>, where
    /// <c>bool.ToString()</c> writes <c>True</c> / <c>False</c> (a server that compares the text sees neither).
    /// </summary>
    /// <param name="accessExpression">The expression that reads the value, nullable or not.</param>
    /// <returns>The expression.</returns>
    public static string BooleanWireExpression(string accessExpression)
        => $"((bool?){accessExpression}) switch {{ true => \"true\", false => \"false\", _ => null }}";

    /// <summary>
    /// Returns a C# expression that formats a parameter value for the wire from its schema, or
    /// <see langword="null"/> when the schema needs no culture-independent formatting. The expression
    /// works for a nullable and a non-nullable property alike and is of type <c>string?</c>.
    /// </summary>
    /// <param name="accessExpression">The expression that reads the value, e.g. <c>parameters.From</c>.</param>
    /// <param name="schema">The parameter's schema.</param>
    /// <returns>An expression of type <c>string?</c>, or null.</returns>
    public static string? TryBuildWireExpression(
        string accessExpression,
        IOpenApiSchema? schema)
    {
        if (IsIntegerEnum(schema))
        {
            return IntegerEnumWireExpression(accessExpression);
        }

        var actualSchema = schema is OpenApiSchemaReference reference ? reference.Target : schema;
        if (actualSchema is null || actualSchema.Enum is { Count: > 0 })
        {
            return null;
        }

        if (actualSchema.IsOfType(JsonSchemaType.Boolean))
        {
            return BooleanWireExpression(accessExpression);
        }

        string? format;
        if (actualSchema.IsOfType(JsonSchemaType.String))
        {
            format = actualSchema.Format?.ToLowerInvariant() switch
            {
                "date" => "\"yyyy-MM-dd\"",
                "date-time" => "\"yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK\"",
                _ => null,
            };

            if (format is null)
            {
                return null;
            }
        }
        else if (actualSchema.IsOfType(JsonSchemaType.Number) || actualSchema.IsOfType(JsonSchemaType.Integer))
        {
            // A culture may write a minus sign (U+2212 under sv-SE) or a decimal comma the server cannot read
            format = "null";
        }
        else
        {
            return null;
        }

        return $"((global::System.IFormattable?){accessExpression})?.ToString({format}, {Invariant})";
    }

    /// <summary>
    /// Returns a C# expression that formats the value for the wire, or <see langword="null"/> when the
    /// type needs no culture-independent formatting (strings, GUIDs, enums, models). A boolean is <c>true</c> /
    /// <c>false</c>, never <c>bool.ToString()</c>'s <c>True</c> / <c>False</c>.
    /// </summary>
    /// <param name="accessExpression">The expression that reads the value, e.g. <c>parameters.From</c>.</param>
    /// <param name="csharpType">The C# type of the value, e.g. <c>DateOnly?</c>.</param>
    /// <returns>An expression of type <c>string?</c> (it works for a nullable and a non-nullable value), or null.</returns>
    public static string? TryBuildWireExpression(
        string accessExpression,
        string csharpType)
    {
        if (csharpType is null)
        {
            throw new ArgumentNullException(nameof(csharpType));
        }

        if (csharpType.TrimEnd('?') == "bool")
        {
            return BooleanWireExpression(accessExpression);
        }

        var arguments = csharpType.TrimEnd('?') switch
        {
            // RFC 3339 full-date
            "DateOnly" => $"\"yyyy-MM-dd\", {Invariant}",

            // RFC 3339 date-time: no fraction when it is zero, the offset as +02:00
            "DateTimeOffset" or "DateTime" => $"\"yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK\", {Invariant}",

            // RFC 3339 partial-time
            "TimeOnly" => $"\"HH:mm:ss.FFFFFFF\", {Invariant}",

            // Numbers: a culture may write a minus sign (U+2212 under sv-SE) or a decimal comma the server cannot read
            "float" or "double" or "decimal" or "int" or "long" or "short" or "byte" or "sbyte" or "uint" or "ulong" or "ushort" => $"null, {Invariant}",
            _ => null,
        };

        return arguments is null
            ? null
            : $"((global::System.IFormattable?){accessExpression})?.ToString({arguments})";
    }

    // An array of arrays ... of a string enum; the depth bounds a cycle the validator did not catch (an array alias
    // whose items are itself)
    private static bool IsStringEnum(
        IOpenApiSchema? schema,
        int depth)
    {
        var actual = schema is OpenApiSchemaReference reference ? reference.Target : schema;
        if (actual is null || depth > MaxDepth)
        {
            return false;
        }

        if (actual.IsOfType(JsonSchemaType.Array))
        {
            return actual.Items is not null && IsStringEnum(actual.Items, depth + 1);
        }

        return actual.Enum is { Count: > 0 } && actual.IsOfType(JsonSchemaType.String);
    }
}