namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// A node a JSONPath query selected, with where it is: the container it is a member or element of, so it can be
/// replaced or removed.
/// </summary>
public sealed class JsonPathMatch
{
    private readonly JsonPathMatch? parent;
    private string? normalizedPath;

    internal JsonPathMatch(
        JsonNode? value,
        JsonPathMatch? parent,
        string? memberName,
        int arrayIndex)
    {
        Value = value;
        this.parent = parent;
        MemberName = memberName;
        ArrayIndex = arrayIndex;
    }

    /// <summary>
    /// Gets the selected node; <see langword="null"/> for the JSON value <c>null</c>.
    /// </summary>
    public JsonNode? Value { get; }

    /// <summary>
    /// Gets the object or array the node is a member or element of; <see langword="null"/> for the root.
    /// </summary>
    public JsonNode? Container => parent?.Value;

    /// <summary>
    /// Gets the node's member name when its container is an object.
    /// </summary>
    public string? MemberName { get; }

    /// <summary>
    /// Gets the node's index when its container is an array; <c>-1</c> otherwise.
    /// </summary>
    public int ArrayIndex { get; }

    /// <summary>
    /// Gets a value indicating whether the node is the root of the document.
    /// </summary>
    public bool IsRoot => parent is null;

    /// <summary>
    /// Gets the node's location as an RFC 9535 normalized path, for example <c>$['paths']['/pets'][0]</c>.
    /// </summary>
    public string NormalizedPath
        => normalizedPath ??= parent is null
            ? "$"
            : parent.NormalizedPath + (MemberName is not null ? "['" + Escape(MemberName) + "']" : "[" + ArrayIndex.ToString(CultureInfo.InvariantCulture) + "]");

    private static string Escape(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            switch (c)
            {
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\'': sb.Append("\\'"); break;
                case '\\': sb.Append("\\\\"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.ToString();
    }
}