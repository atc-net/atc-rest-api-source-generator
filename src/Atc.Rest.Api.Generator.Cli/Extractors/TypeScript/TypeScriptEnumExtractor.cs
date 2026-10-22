namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Extracts OpenAPI enum schema definitions and converts them to TypeScript enum or union type content.
/// </summary>
public static class TypeScriptEnumExtractor
{
    /// <summary>
    /// Extracts all enum definitions from OpenAPI document components.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="config">TypeScript client generation configuration.</param>
    /// <returns>List of (TypeName, GeneratedContent) tuples for each enum.</returns>
    public static List<(string Name, string Content)> Extract(
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

        var headerContent = config.FileHeader;

        foreach (var (schemaName, actualSchema, enumValues) in TypeScriptSchemaCatalog.StringEnums(openApiDoc, config.IncludeDeprecated))
        {
            // Build JSDoc from schema description plus @deprecated when the spec marks
            // the enum deprecated (e.g. legacy status values being phased out).
            JsDocComment? docTags = null;
            if (!string.IsNullOrEmpty(actualSchema.Description) || actualSchema.Deprecated)
            {
                docTags = new JsDocComment(
                    description: actualSchema.Description,
                    parameters: null,
                    returns: null,
                    isDeprecated: actualSchema.Deprecated,
                    deprecatedMessage: null,
                    example: null);
            }

            string content;
            if (config.EnumStyle == TypeScriptEnumStyle.Union)
            {
                content = GenerateUnionType(headerContent, docTags, schemaName, enumValues, config.EnumRuntimeValues, config.ExtensibleEnums);
            }
            else
            {
                content = GenerateEnum(headerContent, docTags, schemaName, enumValues);
            }

            results.Add((schemaName, content));
        }

        return results;
    }

    /// <summary>
    /// Generates a string union type alias. When <paramref name="includeRuntimeValues"/> is
    /// true, a runtime <c>{typeName}Values = [...] as const</c> array is appended beside the
    /// type so consumers can iterate the values (e.g. to populate a dropdown) without a TS
    /// enum or Zod.
    /// When <paramref name="open"/> is true, the union also takes any string (an extensible enum).
    /// </summary>
    private static string GenerateUnionType(
        string? headerContent,
        JsDocComment? docTags,
        string typeName,
        List<string> values,
        bool includeRuntimeValues,
        bool open)
    {
        var parameters = TypeScriptTypeAliasParametersFactory.CreateStringUnion(
            headerContent,
            docTags,
            typeName,
            values);

        // Open (an extensible enum): the listed values still autocomplete, and a value a server adds later type-checks
        if (open)
        {
            parameters = parameters with { Definition = parameters.Definition + " | (string & {})" };
        }

        var generator = new GenerateContentForTypeAlias(
            new JsDocCommentGenerator(),
            parameters);

        var content = generator.Generate();

        if (!includeRuntimeValues)
        {
            return content;
        }

        // The type-alias output already ends with a newline; add one blank line, then the
        // runtime const array. Built manually because the TypeScript code-generation library
        // has no const/variable primitive (matches the StringBuilder pattern used by other
        // extractors such as TypeScriptServersExtractor / TypeScriptRetryConfigExtractor).
        var sb = new StringBuilder(content);
        sb.AppendLine();
        sb.Append("export const ").Append(typeName).Append("Values = [");
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(TypeScriptLiteralHelper.ToStringLiteral(values[i]));
        }

        sb.AppendLine("] as const;");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a TypeScript enum declaration.
    /// </summary>
    private static string GenerateEnum(
        string? headerContent,
        JsDocComment? docTags,
        string typeName,
        List<string> values)
    {
        // Whatever a value holds (quotes, slashes, a leading digit), the member name is a valid
        // identifier, and two values that give the same name stay two members.
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var nameValuePairs = new List<KeyValuePair<string, string>>();
        foreach (var value in values)
        {
            var name = TypeScriptLiteralHelper.ToEnumMemberName(value.ToPascalCase());
            var uniqueName = name;
            var suffix = 1;
            while (!usedNames.Add(uniqueName))
            {
                suffix++;
                uniqueName = name + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            nameValuePairs.Add(new KeyValuePair<string, string>(uniqueName, TypeScriptLiteralHelper.ToStringLiteral(value)));
        }

        var parameters = TypeScriptEnumParametersFactory.CreateFromNameValuePairs(
            headerContent,
            docTags,
            typeName,
            nameValuePairs);

        var generator = new GenerateContentForEnum(
            new JsDocCommentGenerator(),
            parameters);

        return generator.Generate();
    }
}