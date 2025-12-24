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

    /// <param name="node">The syntax node to check.</param>
    extension(SyntaxNode node)
    {
        /// <summary>
        /// Checks if the syntax node is a partial class with at least one attribute.
        /// This is a quick syntax-only check used for incremental generator predicates.
        /// </summary>
        /// <returns>True if the node is a partial class with attributes.</returns>
        public bool IsPartialClassWithAttribute()
        {
            // Only process class declarations
            if (node is not ClassDeclarationSyntax classDecl)
            {
                return false;
            }

            // Must be a partial class
            if (!classDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return false;
            }

            // Must have at least one attribute
            return classDecl.AttributeLists.Count > 0;
        }
    }
}