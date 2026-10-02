namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Gives every property of one model a valid, unique C# name. A schema may hold property names that are no
/// identifier (<c>$id</c>, <c>2fa</c>) or that become the same name after PascalCasing (<c>user_name</c> and
/// <c>userName</c>): none of them may be dropped, because each is a member on the wire.
/// </summary>
public sealed class PropertyNameRegistry
{
    private readonly HashSet<string> seenKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the C# name for a schema property, or <see langword="null"/> when the very same property
    /// (the same key) was already seen - a schema composed with <c>allOf</c> can list a property twice.
    /// </summary>
    /// <param name="propertyKey">The property's name in the schema.</param>
    /// <param name="candidateName">The PascalCased name derived from it.</param>
    /// <returns>A valid identifier that no other property of the model has, or null for a repeated key.</returns>
    public string? Resolve(
        string propertyKey,
        string candidateName)
    {
        if (!seenKeys.Add(propertyKey))
        {
            return null;
        }

        var name = candidateName;
        if (string.IsNullOrEmpty(name))
        {
            name = "Property";
        }
        else if (char.IsDigit(name[0]))
        {
            name = "_" + name;
        }

        var uniqueName = name;
        var suffix = 1;
        while (!usedNames.Add(uniqueName))
        {
            suffix++;
            uniqueName = name + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return uniqueName;
    }
}