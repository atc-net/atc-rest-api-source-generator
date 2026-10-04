namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Where the source generator puts each type the old generator kept in <c>{Project}.Api.Generated.Contracts[.{Segment}]</c>:
/// the models, handler interfaces, parameters and results, laid out from the specification the way the generator lays
/// them out (shared and segment models, a segment that resolves away, the namespaces a segment actually has).
/// </summary>
internal sealed class ContractsNamespaceMap
{
    private readonly Dictionary<string, string> typeNamespaces = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> segmentNamespaces = new(StringComparer.OrdinalIgnoreCase);

    private ContractsNamespaceMap(string generatedRoot)
    {
        GeneratedRoot = generatedRoot;
    }

    /// <summary>
    /// Gets the root of the generated namespaces, <c>{Project}.Api.Contracts.Generated</c>.
    /// </summary>
    public string GeneratedRoot { get; }

    /// <summary>
    /// Gets the shared models namespace, or <see langword="null"/> when the specification has no shared model.
    /// </summary>
    public string? SharedModelsNamespace { get; private set; }

    /// <summary>
    /// Builds the map for <paramref name="projectName"/> (the migrated contracts project is <c>{projectName}.Api.Contracts</c>).
    /// </summary>
    public static ContractsNamespaceMap Build(
        OpenApiDocument document,
        string projectName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        var root = $"{projectName}.Api.Contracts";
        var map = new ContractsNamespaceMap(root + ".Generated");
        var shared = PathSegmentHelper.GetSharedSchemas(document);
        if (shared.Count > 0)
        {
            map.SharedModelsNamespace = map.GeneratedRoot + ".Models";
            foreach (var name in shared)
            {
                map.typeNamespaces.TryAdd(name, map.SharedModelsNamespace);
            }
        }

        foreach (var segment in PathSegmentHelper.GetUniquePathSegments(document))
        {
            var effective = PathSegmentHelper.ResolveEffectivePathSegment(document, root, segment);
            var segmentRoot = effective is null ? map.GeneratedRoot : $"{map.GeneratedRoot}.{effective}";
            var flags = PathSegmentHelper.GetPathSegmentNamespaces(document, segment, shared);

            var namespaces = new List<string>();
            AddIf(namespaces, flags.HasHandlers, segmentRoot + ".Handlers");
            AddIf(namespaces, flags.HasModels, segmentRoot + ".Models");
            AddIf(namespaces, flags.HasParameters, segmentRoot + ".Parameters");
            AddIf(namespaces, flags.HasResults, segmentRoot + ".Results");
            map.segmentNamespaces[segment] = namespaces;

            foreach (var model in PathSegmentHelper.GetSegmentSpecificSchemas(document, segment, shared))
            {
                map.typeNamespaces.TryAdd(model, segmentRoot + ".Models");
            }

            foreach (var (path, method, operation) in PathSegmentHelper.GetOperationsForSegment(document, segment))
            {
                var name = OperationName(path, method, operation);
                map.typeNamespaces.TryAdd($"I{name}Handler", segmentRoot + ".Handlers");
                map.typeNamespaces.TryAdd($"{name}Parameters", segmentRoot + ".Parameters");
                map.typeNamespaces.TryAdd($"{name}Result", segmentRoot + ".Results");
            }
        }

        return map;
    }

    /// <summary>
    /// Gets the namespace the generator puts <paramref name="typeName"/> in.
    /// </summary>
    public bool TryGetTypeNamespace(
        string typeName,
        [NotNullWhen(true)] out string? namespaceName)
        => typeNamespaces.TryGetValue(typeName, out namespaceName);

    /// <summary>
    /// Gets the namespaces the generator has for an old segment namespace (<c>…Contracts.{Segment}</c>), or
    /// <see langword="null"/> when the specification has no such segment.
    /// </summary>
    public IReadOnlyList<string>? GetSegmentNamespaces(string oldSegment)
        => segmentNamespaces.TryGetValue(oldSegment, out var namespaces) ? namespaces : null;

    private static void AddIf(
        List<string> namespaces,
        bool exists,
        string namespaceName)
    {
        if (exists)
        {
            namespaces.Add(namespaceName);
        }
    }

    // The generator's operation name: the operationId, or the method and the path when there is none
    private static string OperationName(
        string path,
        string method,
        OpenApiOperation operation)
    {
        var normalizedPath = path
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal);
        return (operation.OperationId ?? $"{method}{normalizedPath}").ToPascalCaseForDotNet();
    }
}