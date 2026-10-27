namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// <c>[Display(Name = "...")]</c> on the validated members of a server's models and parameter records, naming each as
/// the caller sent it: a validation message is built from the member's display name, and without one it names the C#
/// member - <c>The Tenant field is required.</c> for a header the caller knows as <c>X-Tenant</c>, under the key
/// <c>X-Tenant</c>.
/// </summary>
internal static class WireDisplayNameHelper
{
    // Written in full: Microsoft.OpenApi, which a host often imports globally, has a DisplayAttribute too
    private const string DisplayAttributeName = "global::System.ComponentModel.DataAnnotations.Display";

    private static readonly HashSet<string> ValidationAttributes = new(StringComparer.Ordinal)
    {
        "Required",
        "ApiMinLength",
        "ApiMaxLength",
        "StringLength",
        "MinLength",
        "MaxLength",
        "Length",
        "Range",
        "RegularExpression",
        "ApiRegularExpression",
        "ApiItemsRegularExpression",
        "ApiAbsoluteUri",
        "EmailAddress",
        "Url",
    };

    /// <summary>
    /// The models, their validated properties named by the JSON name: the <c>[JsonPropertyName]</c>, else the property
    /// name as the web defaults write it (camel case).
    /// </summary>
    public static RecordsParameters ForModels(
        RecordsParameters recordsParameters)
        => WithDisplayNames(
            recordsParameters,
            parameter => parameter.Attributes?.FirstOrDefault(a => a.Name == "JsonPropertyName")?.Content?.Trim('"')
                         ?? JsonNamingPolicy.CamelCase.ConvertName(parameter.Name));

    /// <summary>
    /// The parameter records, their validated members named by the parameter's name in the specification (a cookie by
    /// its own name, not by the header it is bound from). The body is no parameter and keeps its name.
    /// </summary>
    public static RecordsParameters ForParameters(
        RecordsParameters recordsParameters)
        => WithDisplayNames(
            recordsParameters,
            parameter =>
            {
                var binding = parameter.Attributes?.FirstOrDefault(a => a.Name is "FromRoute" or "FromQuery" or "FromHeader");
                const string namePrefix = "Name = ";
                if (binding?.Content is not { } content || !content.StartsWith(namePrefix, StringComparison.Ordinal))
                {
                    return null;
                }

                var name = content.Substring(namePrefix.Length).Trim('"');
                return binding.Name == "FromHeader" && name.StartsWith(WireBindingHelper.CookieHeaderPrefix, StringComparison.Ordinal)
                    ? name.Substring(WireBindingHelper.CookieHeaderPrefix.Length)
                    : name;
            });

    /// <summary>
    /// A model property named by another wire name than <see cref="ForModels"/> gave it (the XML name of a model only an
    /// XML request carries).
    /// </summary>
    public static ParameterBaseParameters Renamed(
        ParameterBaseParameters parameter,
        string wireName)
        => WithDisplayName(
            parameter with { Attributes = parameter.Attributes?.Where(a => a.Name != DisplayAttributeName).ToList() },
            wireName);

    private static RecordsParameters WithDisplayNames(
        RecordsParameters recordsParameters,
        Func<ParameterBaseParameters, string?> wireNameOf)
        => recordsParameters with
        {
            Parameters = recordsParameters.Parameters
                .Select(record => record with
                {
                    Parameters = record.Parameters?
                        .Select(parameter => WithDisplayName(parameter, wireNameOf(parameter)))
                        .ToList(),
                })
                .ToList(),
        };

    private static ParameterBaseParameters WithDisplayName(
        ParameterBaseParameters parameter,
        string? wireName)
    {
        if (wireName is null ||
            string.Equals(wireName, parameter.Name, StringComparison.Ordinal) ||
            parameter.Attributes is null ||
            parameter.Attributes.Any(a => a.Name is "Display" or DisplayAttributeName) ||
            !parameter.Attributes.Any(a => ValidationAttributes.Contains(NameOf(a))))
        {
            return parameter;
        }

        var literal = "\"" + wireName.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        return parameter with { Attributes = [.. parameter.Attributes, new AttributeParameters(DisplayAttributeName, $"Name = {literal}")] };
    }

    // An attribute's name without the arguments the schema extractor writes into it ("ApiMaxLength(5)")
    private static string NameOf(AttributeParameters attribute)
    {
        var argumentsStart = attribute.Name.IndexOf('(');
        return argumentsStart < 0 ? attribute.Name : attribute.Name.Substring(0, argumentsStart);
    }
}