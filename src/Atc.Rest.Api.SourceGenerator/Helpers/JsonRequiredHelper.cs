namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// <c>[JsonRequired]</c> on the required properties of generated models, for the generators that have the JSON reader
/// check them: a client with <c>strictRequiredProperties</c>, and a server without a validation filter.
/// </summary>
internal static class JsonRequiredHelper
{
    /// <summary>
    /// Adds <c>[JsonRequired]</c> next to every <c>[Required]</c> of the client models (<c>strictRequiredProperties</c>),
    /// so System.Text.Json rejects JSON that leaves a required property out. A server with the ValidationFilter keeps
    /// DataAnnotations only - the filter answers a missing required property with a ValidationProblemDetails that
    /// names it; a server without one gets <c>[JsonRequired]</c> too, so such a body is rejected at all.
    /// </summary>
    public static RecordsParameters AddJsonRequired(
        RecordsParameters recordsParameters)
    {
        var records = recordsParameters.Parameters
            .Select(WithJsonRequired)
            .ToList();

        const string jsonSerializationUsing = "using System.Text.Json.Serialization;";
        var header = recordsParameters.HeaderContent;
        if (header is not null && !header.Contains(jsonSerializationUsing, StringComparison.Ordinal))
        {
            const string dataAnnotationsUsing = "using System.ComponentModel.DataAnnotations;";
            header = header.Contains(dataAnnotationsUsing, StringComparison.Ordinal)
                ? header.Replace(dataAnnotationsUsing, dataAnnotationsUsing + "\n" + jsonSerializationUsing)
                : header + jsonSerializationUsing + "\n";
        }

        return recordsParameters with { HeaderContent = header, Parameters = records };
    }

    /// <summary>
    /// The records without <c>[JsonRequired]</c>: a client without <c>strictRequiredProperties</c> reads a response
    /// that leaves a required property out (the extractors put <c>[JsonRequired]</c> on a required, nullable
    /// property, which the server checks).
    /// </summary>
    public static RecordsParameters RemoveJsonRequired(
        RecordsParameters recordsParameters)
        => recordsParameters with { Parameters = [.. recordsParameters.Parameters.Select(WithoutJsonRequired)] };

    /// <summary>
    /// The record without <c>[JsonRequired]</c> (a client without <c>strictRequiredProperties</c>).
    /// </summary>
    public static RecordParameters WithoutJsonRequired(RecordParameters record)
        => record with
        {
            Parameters = record.Parameters?
                .Select(param => param.Attributes?.Any(IsJsonRequired) == true
                    ? param with { Attributes = [.. param.Attributes.Where(a => !IsJsonRequired(a))] }
                    : param)
                .ToList(),
        };

    /// <summary>
    /// The record with <c>[JsonRequired]</c> next to every <c>[Required]</c> (<c>strictRequiredProperties</c>).
    /// </summary>
    public static RecordParameters WithJsonRequired(RecordParameters record)
        => record with
        {
            Parameters = record.Parameters?
                .Select(param => param.Attributes?.Any(a => a.Name == "Required") == true
                    ? param with { Attributes = [.. param.Attributes, new AttributeParameters("JsonRequired", null)] }
                    : param)
                .ToList(),
        };

    private static bool IsJsonRequired(AttributeParameters attribute)
        => attribute.Name is "JsonRequired" or "global::System.Text.Json.Serialization.JsonRequired";
}