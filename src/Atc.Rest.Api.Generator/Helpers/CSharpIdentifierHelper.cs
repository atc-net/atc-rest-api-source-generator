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
    /// Returns <paramref name="identifier"/> prefixed with <c>@</c> when it collides with a C#
    /// keyword, and unchanged otherwise.
    /// </summary>
    /// <param name="identifier">The candidate identifier.</param>
    /// <returns>An identifier that is safe to emit.</returns>
    public static string EscapeIfKeyword(string identifier)
        => CSharpKeywords.IsKeyword(identifier)
            ? "@" + identifier
            : identifier;
}