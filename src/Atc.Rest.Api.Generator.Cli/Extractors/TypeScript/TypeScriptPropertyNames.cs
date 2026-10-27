namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// The name a schema property has in the generated TypeScript - one rule for the interfaces, the Zod schemas
/// and the wire-name tables, so that the three always agree.
/// </summary>
internal static class TypeScriptPropertyNames
{
    /// <summary>
    /// The property's name under the naming strategy. When another property of the same model already has
    /// that name (<c>user_name</c> and <c>userName</c> are both <c>userName</c> in camelCase), the converted one keeps
    /// its name from the specification: no property is dropped, and none shadows another.
    /// </summary>
    /// <param name="key">The property's name in the schema.</param>
    /// <param name="modelKeys">Every property name of the model, in schema order.</param>
    /// <param name="namingStrategy">The naming strategy of the client.</param>
    /// <returns>The TypeScript property name (not yet quoted).</returns>
    public static string Resolve(
        string key,
        IEnumerable<string> modelKeys,
        TypeScriptNamingStrategy namingStrategy)
    {
        var name = key.ApplyNamingStrategy(namingStrategy);
        if (string.Equals(name, key, StringComparison.Ordinal))
        {
            return name;
        }

        var isEarlier = true;
        foreach (var other in modelKeys)
        {
            if (string.Equals(other, key, StringComparison.Ordinal))
            {
                isEarlier = false;
                continue;
            }

            // Another property is spelled like the converted name, or an earlier one converts to it
            if (string.Equals(other, name, StringComparison.Ordinal) ||
                (isEarlier && string.Equals(other.ApplyNamingStrategy(namingStrategy), name, StringComparison.Ordinal)))
            {
                return key;
            }
        }

        return name;
    }

    /// <summary>
    /// The name as it is written in an interface or an object literal: quoted when it is no identifier
    /// (<c>'2fa'</c>, <c>'first-name'</c>).
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>The name, or the name as a string literal.</returns>
    public static string ToMemberSyntax(string name)
        => IsIdentifier(name) ? name : TypeScriptLiteralHelper.ToStringLiteral(name);

    private static bool IsIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name) || !(char.IsLetter(name[0]) || name[0] is '_' or '$'))
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c is not ('_' or '$'))
            {
                return false;
            }
        }

        return true;
    }
}