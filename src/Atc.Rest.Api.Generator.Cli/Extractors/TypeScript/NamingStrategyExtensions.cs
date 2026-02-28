namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Extension method for applying a <see cref="TypeScriptNamingStrategy"/> to a name.
/// </summary>
internal static class NamingStrategyExtensions
{
    /// <summary>
    /// Applies the specified naming strategy to a property or parameter name.
    /// </summary>
    /// <param name="value">The original name from the OpenAPI specification.</param>
    /// <param name="strategy">The naming strategy to apply.</param>
    /// <returns>The transformed name.</returns>
    public static string ApplyNamingStrategy(
        this string value,
        TypeScriptNamingStrategy strategy)
        => strategy switch
        {
            TypeScriptNamingStrategy.Original => value,
            TypeScriptNamingStrategy.PascalCase => value.ToPascalCase(),
            _ => value.ToCamelCase(),
        };
}