namespace Atc.CodeGeneration.TypeScript.Content;

[Flags]
public enum TypeScriptModifiers
{
    None = 0,
    Export = 1 << 0,
    ExportDefault = 1 << 1,
    Abstract = 1 << 2,
    Readonly = 1 << 3,
    Async = 1 << 4,
}
