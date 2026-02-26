namespace Atc.CodeGeneration.TypeScript.Helpers;

public static class TypeScriptModifiersHelper
{
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
