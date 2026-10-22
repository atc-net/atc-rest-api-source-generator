namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The C# property name of every parameter of one operation, the same at every site that names it (the parameters
/// record, the request URL, the headers, the cookies). A parameter name becomes a PascalCase property (a header by
/// its component name, or without its <c>x-</c> prefix); when two parameters of the operation would share a property
/// (<c>id</c> in the path and <c>id</c> in the query, <c>Id</c> in a header), the path keeps the bare name and the
/// others carry their location (<c>IdQuery</c>, <c>IdHeader</c>): every parameter is on the wire, so none may be
/// dropped or merged.
/// </summary>
public sealed class ParameterPropertyNames
{
    private static readonly ParameterLocation[] Precedence =
    [
        ParameterLocation.Path,
        ParameterLocation.Query,
        ParameterLocation.Header,
        ParameterLocation.Cookie,
        ParameterLocation.QueryString,
    ];

    private readonly Dictionary<(string Name, ParameterLocation In), string> names;

    private ParameterPropertyNames(
        Dictionary<(string Name, ParameterLocation In), string> names)
        => this.names = names;

    /// <summary>
    /// Resolves the property names of an operation's parameters: the path item's, overridden by the operation's own
    /// with the same name and location.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="pathParameters">The path item's parameters, which the operation inherits.</param>
    /// <returns>The names.</returns>
    public static ParameterPropertyNames For(
        OpenApiOperation? operation,
        IList<IOpenApiParameter>? pathParameters)
    {
        var effective = new List<(string Name, ParameterLocation In, string? ReferenceId)>();
        foreach (var parameterOrReference in (pathParameters ?? []).Concat(operation?.Parameters ?? []))
        {
            var resolved = parameterOrReference.Resolve();
            if (resolved.Parameter is not { Name: { Length: > 0 } name } parameter)
            {
                continue;
            }

            var location = parameter.In ?? ParameterLocation.Query;
            effective.RemoveAll(p => string.Equals(p.Name, name, StringComparison.Ordinal) && p.In == location);
            effective.Add((name, location, resolved.ReferenceId));
        }

        var taken = new HashSet<string>(StringComparer.Ordinal);
        var result = new Dictionary<(string Name, ParameterLocation In), string>();
        foreach (var group in effective
                     .GroupBy(p => Local(p.Name, p.In, p.ReferenceId), StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(p => Array.IndexOf(Precedence, p.In)).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                // The first keeps the bare name; another location carries its location; the same location is numbered
                var candidate = i == 0 || ordered[i].In == ordered[0].In ? group.Key : group.Key + ordered[i].In;
                result[(ordered[i].Name, ordered[i].In)] = taken.Claim(candidate);
            }
        }

        return new ParameterPropertyNames(result);
    }

    /// <summary>
    /// The property name of a parameter of the operation; a parameter the operation does not list (a path-level
    /// parameter the operation overrides, a parameter resolved another way) gets the local name.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <param name="referenceId">Its component name, when it is a reference.</param>
    /// <returns>The property name.</returns>
    public string Get(
        IOpenApiParameter parameter,
        string? referenceId)
    {
        var name = parameter?.Name ?? string.Empty;
        var location = parameter?.In ?? ParameterLocation.Query;
        return names.TryGetValue((name, location), out var resolved)
            ? resolved
            : Local(name, location, referenceId);
    }

    /// <summary>
    /// The property name a parameter gets on its own: a header by its component name, else without its <c>x-</c>
    /// prefix; every other location PascalCased.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="location">Where the parameter is sent.</param>
    /// <param name="referenceId">Its component name, when it is a reference.</param>
    /// <returns>The property name.</returns>
    public static string Local(
        string name,
        ParameterLocation location,
        string? referenceId)
        => location == ParameterLocation.Header
            ? (string.IsNullOrEmpty(referenceId) ? name.ToHeaderPropertyName() : referenceId!.ToPascalCaseForDotNet())
            : name.ToPascalCaseForDotNet();
}