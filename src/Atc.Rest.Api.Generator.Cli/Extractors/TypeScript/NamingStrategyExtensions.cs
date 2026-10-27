namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Extension method for applying a <see cref="TypeScriptNamingStrategy"/> to a name.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
internal static class NamingStrategyExtensions
{
    /// <param name="value">The original name from the OpenAPI specification.</param>
    extension(string value)
    {
        /// <summary>
        /// Applies the specified naming strategy to a property or parameter name.
        /// </summary>
        /// <param name="strategy">The naming strategy to apply.</param>
        /// <returns>The transformed name.</returns>
        public string ApplyNamingStrategy(TypeScriptNamingStrategy strategy)
            => strategy switch
            {
                TypeScriptNamingStrategy.Original => value,
                TypeScriptNamingStrategy.PascalCase => value.ToPascalCase(),
                _ => value.ToCamelCase(),
            };
    }
}