namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Makes generator-derived names safe to emit as C# identifiers.
/// </summary>
/// <remarks>
/// Names such as the route-group local in a generated endpoint definition are derived from an
/// OpenAPI path segment, so the specification - not the generator - decides the text. A spec with a
/// <c>/public</c> or <c>/event</c> path yields a local named <c>public</c> or <c>event</c>, which
/// does not parse. Prefixing with <c>@</c> keeps the readable name while making it a valid
/// identifier.
/// </remarks>
public static class CSharpIdentifierHelper
{
    /// <summary>
    /// C# keywords that cannot appear as a bare identifier.
    /// </summary>
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.Ordinal)
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
    };

    /// <summary>
    /// Returns <paramref name="identifier"/> prefixed with <c>@</c> when it collides with a C#
    /// keyword, and unchanged otherwise.
    /// </summary>
    /// <param name="identifier">The candidate identifier.</param>
    /// <returns>An identifier that is safe to emit.</returns>
    public static string EscapeIfKeyword(string identifier)
        => ReservedKeywords.Contains(identifier)
            ? "@" + identifier
            : identifier;

    /// <summary>
    /// Indicates whether <paramref name="identifier"/> is a C# keyword.
    /// </summary>
    /// <param name="identifier">The candidate identifier.</param>
    /// <returns><see langword="true"/> when the value is a reserved keyword.</returns>
    public static bool IsKeyword(string identifier)
        => ReservedKeywords.Contains(identifier);
}