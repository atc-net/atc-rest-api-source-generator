namespace Atc.CodeGeneration.TypeScript.Content.Generators;

public class GenerateContentForBarrelExport : IContentGenerator
{
    private readonly TypeScriptBarrelExportParameters parameters;
    private readonly JsDocCommentGenerator jsDocGenerator = new JsDocCommentGenerator();

    public GenerateContentForBarrelExport(
        TypeScriptBarrelExportParameters parameters)
    {
        this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

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