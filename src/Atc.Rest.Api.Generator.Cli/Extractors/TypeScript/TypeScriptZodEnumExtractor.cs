namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Extracts OpenAPI enum schema definitions and converts them to Zod enum schema content.
/// </summary>
public static class TypeScriptZodEnumExtractor
{
    /// <summary>
    /// Extracts all enum definitions from OpenAPI document components and generates Zod schemas.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="config">TypeScript client generation configuration.</param>
    /// <returns>List of (TypeName, GeneratedContent) tuples for each enum Zod schema.</returns>
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
            var content = GenerateZodEnum(headerContent, schemaName, enumValues, config.ExtensibleEnums);
            results.Add((schemaName, content));
        }

        return results;
    }

    private static string GenerateZodEnum(
        string? headerContent,
        string schemaName,
        List<string> values,
        bool open)
    {
        var sb = new StringBuilder();

        sb.Append(headerContent);

        sb.AppendLine("import { z } from 'zod';");
        sb.AppendLine();

        // Open (an extensible enum): a value a server adds later parses too
        sb.Append("export const ").Append(schemaName).Append(open ? "Schema = z.union([z.enum([" : "Schema = z.enum([");

        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(TypeScriptLiteralHelper.ToStringLiteral(values[i]));
        }

        sb.AppendLine(open ? "]), z.string()]);" : "]);");

        return sb.ToString();
    }
}