namespace Atc.CodeGeneration.TypeScript.Content.Generators;

/// <summary>
/// Writes a barrel file (<c>index.ts</c>): one <c>export * from</c> or <c>export { a, b } from</c> line per
/// module, with <c>type</c> added for type-only re-exports.
/// </summary>
public class GenerateContentForBarrelExport : IContentGenerator
{
    private readonly TypeScriptBarrelExportParameters parameters;
    private readonly JsDocCommentGenerator jsDocGenerator = new JsDocCommentGenerator();

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateContentForBarrelExport"/> class.
    /// </summary>
    /// <param name="parameters">The header and the modules to re-export.</param>
    public GenerateContentForBarrelExport(
        TypeScriptBarrelExportParameters parameters)
    {
        this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    /// <summary>
    /// Renders the barrel file.
    /// </summary>
    /// <returns>The TypeScript source of the re-export lines.</returns>
    public string Generate()
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(parameters.HeaderContent))
        {
            sb.Append(parameters.HeaderContent);
        }

        foreach (var export in parameters.Exports)
        {
            if (export.DocumentationTags is not null)
            {
                var jsDoc = jsDocGenerator.GenerateTags(0, export.DocumentationTags);
                if (!string.IsNullOrEmpty(jsDoc))
                {
                    sb.Append(jsDoc);
                }
            }

            if (export.NamedExports is null || !export.NamedExports.Any())
            {
                var typePrefix = export.IsTypeOnly ? "export type * from" : "export * from";
                sb.AppendLine($"{typePrefix} '{export.ModulePath}';");
            }
            else
            {
                var keyword = export.IsTypeOnly ? "export type" : "export";
                var names = string.Join(", ", export.NamedExports);
                sb.AppendLine($"{keyword} {{ {names} }} from '{export.ModulePath}';");
            }
        }

        return sb.ToString();
    }
}