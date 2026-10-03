namespace Atc.CodeGeneration.TypeScript.CodeDocumentation;

/// <summary>
/// Renders a <see cref="JsDocComment"/> as a <c>/** ... */</c> block.
/// </summary>
public interface IJsDocCommentGenerator
{
    /// <summary>
    /// Decides whether the comment has anything worth writing.
    /// </summary>
    /// <param name="jsDocComment">The comment to inspect.</param>
    /// <returns>True when a tag or a real description is present; false for an empty or placeholder description.</returns>
    bool ShouldGenerateTags(JsDocComment jsDocComment);

    /// <summary>
    /// Renders the comment block, each line indented.
    /// </summary>
    /// <param name="indentSpaces">The number of spaces before each line.</param>
    /// <param name="jsDocComment">The comment to render.</param>
    /// <returns>The comment block ending in a line break, or an empty string when there is nothing to write.</returns>
    string GenerateTags(
        ushort indentSpaces,
        JsDocComment jsDocComment);
}