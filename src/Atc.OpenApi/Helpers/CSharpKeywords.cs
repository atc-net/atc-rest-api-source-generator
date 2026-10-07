namespace Atc.OpenApi.Helpers;

/// <summary>
/// The C# keywords: a name the specification uses for a schema, a namespace or a member cannot be one of them.
/// </summary>
public static class CSharpKeywords
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while",

        // Reserved too, though undocumented
        "__arglist", "__makeref", "__reftype", "__refvalue",
    };

    /// <summary>
    /// Whether the name is a C# keyword.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> for a keyword.</returns>
    public static bool IsKeyword(string? name)
        => name is not null && Keywords.Contains(name);
}