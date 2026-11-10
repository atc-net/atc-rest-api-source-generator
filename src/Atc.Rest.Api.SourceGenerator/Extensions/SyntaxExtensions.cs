namespace Atc.Rest.Api.SourceGenerator.Extensions;

/// <summary>
/// Extension methods for Roslyn syntax types.
/// </summary>
internal static class SyntaxExtensions
{
    /// <param name="classDeclarationSyntax">The class declaration syntax node.</param>
    extension(ClassDeclarationSyntax classDeclarationSyntax)
    {
        /// <summary>
        /// Extracts class modifiers as a string (e.g., "public sealed partial").
        /// Ensures "partial" is always included.
        /// </summary>
        /// <returns>The modifiers as a space-separated string.</returns>
        public string GetModifiersString()
        {
            var modifiers = new List<string>();

            foreach (var modifierText in classDeclarationSyntax.Modifiers.Select(modifier => modifier.Text))
            {
                // Include access modifiers and other relevant modifiers
                if (modifierText
                    is "public"
                    or "private"
                    or "protected"
                    or "internal"
                    or "sealed"
                    or "abstract"
                    or "static"
                    or "partial")
                {
                    modifiers.Add(modifierText);
                }
            }

            // Ensure "partial" is always included
            if (!modifiers.Contains("partial", StringComparer.Ordinal))
            {
                modifiers.Add("partial");
            }

            return string.Join(" ", modifiers);
        }
    }
}