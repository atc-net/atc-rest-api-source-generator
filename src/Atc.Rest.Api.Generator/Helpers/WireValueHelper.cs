namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The text a parameter value has on the wire (path, query string, header). Dates, times and
/// decimal numbers must not be formatted with the culture of the machine the client runs on:
/// <c>ToString()</c> gives <c>28-02-1990</c> and <c>1,5</c> under a Danish culture.
/// </summary>
public static class WireValueHelper
{
    private const string Invariant = "global::System.Globalization.CultureInfo.InvariantCulture";

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
        var actualSchema = schema is OpenApiSchemaReference reference ? reference.Target : schema;
        if (actualSchema is null || actualSchema.Enum is { Count: > 0 })
        {
            return null;
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
        else if (actualSchema.IsOfType(JsonSchemaType.Number))
        {
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
    /// type needs no culture-independent formatting (strings, integers, booleans, GUIDs, enums, models).
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

        var arguments = csharpType.TrimEnd('?') switch
        {
            // RFC 3339 full-date
            "DateOnly" => $"\"yyyy-MM-dd\", {Invariant}",

            // RFC 3339 date-time: no fraction when it is zero, the offset as +02:00
            "DateTimeOffset" or "DateTime" => $"\"yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK\", {Invariant}",

            // RFC 3339 partial-time
            "TimeOnly" => $"\"HH:mm:ss.FFFFFFF\", {Invariant}",

            "float" or "double" or "decimal" => $"null, {Invariant}",
            _ => null,
        };

        return arguments is null
            ? null
            : $"((global::System.IFormattable?){accessExpression})?.ToString({arguments})";
    }
}