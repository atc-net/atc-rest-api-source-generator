namespace Atc.CodeGeneration.TypeScript.Helpers;

/// <summary>
/// Turns <see cref="TypeScriptModifiers"/> flags into keyword text.
/// </summary>
public static class TypeScriptModifiersHelper
{
    /// <summary>
    /// Renders the set flags as space-separated keywords in the order <c>export [default]</c>, <c>abstract</c>,
    /// <c>async</c>, <c>readonly</c>.
    /// </summary>
    /// <param name="modifiers">The flags to render.</param>
    /// <returns>The keywords, or an empty string for <see cref="TypeScriptModifiers.None"/>.</returns>
    public static string Render(TypeScriptModifiers modifiers)
    {
        if (modifiers == TypeScriptModifiers.None)
        {
            return string.Empty;
        }

        var parts = new List<string>();

        if ((modifiers & TypeScriptModifiers.ExportDefault) == TypeScriptModifiers.ExportDefault)
        {
            parts.Add("export default");
        }
        else if ((modifiers & TypeScriptModifiers.Export) == TypeScriptModifiers.Export)
        {
            parts.Add("export");
        }

        if ((modifiers & TypeScriptModifiers.Abstract) == TypeScriptModifiers.Abstract)
        {
            parts.Add("abstract");
        }

        if ((modifiers & TypeScriptModifiers.Async) == TypeScriptModifiers.Async)
        {
            parts.Add("async");
        }

        if ((modifiers & TypeScriptModifiers.Readonly) == TypeScriptModifiers.Readonly)
        {
            parts.Add("readonly");
        }

        return string.Join(" ", parts);
    }
}