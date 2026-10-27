namespace Atc.CodeGeneration.TypeScript.Content.Generators;

/// <summary>
/// A generator that renders one complete TypeScript file from the parameters it was constructed with.
/// </summary>
public interface IContentGenerator
{
    /// <summary>
    /// Renders the file.
    /// </summary>
    /// <returns>The full text of the file.</returns>
    string Generate();
}