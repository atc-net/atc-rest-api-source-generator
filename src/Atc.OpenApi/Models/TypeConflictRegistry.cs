namespace Atc.OpenApi.Models;

/// <summary>
/// Registry for detecting and resolving type name conflicts between OpenAPI schemas
/// and .NET system types (e.g., "Task" vs System.Threading.Tasks.Task) or project
/// namespace segments (e.g., schema "Device" in namespace "Contoso.Device.Management").
/// </summary>
public sealed class TypeConflictRegistry
{
    /// <summary>
    /// Reserved .NET types that could conflict with OpenAPI schema names.
    /// </summary>
    private static readonly HashSet<string> ReservedSystemTypes = new(StringComparer.Ordinal)
    {
        nameof(Task),       // System.Threading.Tasks.Task
        nameof(Action),     // System.Action
        "Func",             // System.Func
        nameof(Type),       // System.Type
        nameof(Exception),  // System.Exception
        nameof(Object),     // System.Object
        nameof(String),     // System.String
        nameof(Guid),       // System.Guid
        nameof(DateTime),   // System.DateTime
        nameof(TimeSpan),   // System.TimeSpan
        nameof(Uri),        // System.Uri
        nameof(Version),    // System.Version
    };

    /// <summary>
    /// Set of schema names that conflict with reserved .NET types.
    /// </summary>
    private readonly ISet<string> conflictingSchemaNames;

    /// <summary>
    /// Project name for namespace construction.
    /// </summary>
    private readonly string projectName;

    /// <summary>
    /// Optional path segment for segmented namespaces.
    /// </summary>
    private readonly string? pathSegment;

    /// <summary>
    /// Schemas generated into the shared <c>{Project}.Generated.Models</c> namespace (used by more than one
    /// path segment); null when the caller does not know, and every type is taken to live in this registry's segment.
    /// </summary>
    private readonly ISet<string>? sharedSchemaNames;

    private TypeConflictRegistry(
        ISet<string> conflictingSchemaNames,
        string projectName,
        string? pathSegment,
        ISet<string>? sharedSchemaNames = null)
    {
        this.conflictingSchemaNames = conflictingSchemaNames;
        this.projectName = projectName;
        this.pathSegment = pathSegment;
        this.sharedSchemaNames = sharedSchemaNames;
    }

    /// <summary>
    /// Scans an OpenAPI document once and returns the set of schema names that conflict with reserved .NET types
    /// or with segments of the project namespace.
    /// Call this once per document, then use <see cref="ForSegment"/> to create registries for each path segment.
    /// </summary>
    /// <param name="doc">The OpenAPI document to scan.</param>
    /// <param name="projectNamespace">Optional project namespace; segments are checked for conflicts with schema names.</param>
    /// <param name="pathSegmentNamespaces">
    /// Optional names of the path-segment namespaces (<c>Pet</c> for <c>/pet</c>): inside
    /// <c>{Project}.Generated.Pet.*</c> the simple name <c>Pet</c> is that namespace, so a schema of the same
    /// name must be written fully qualified.
    /// </param>
    /// <returns>Set of conflicting schema names.</returns>
    public static ISet<string> ScanForConflicts(
        OpenApiDocument doc,
        string? projectNamespace = null,
        IEnumerable<string>? pathSegmentNamespaces = null)
    {
        var conflicts = new HashSet<string>(StringComparer.Ordinal);

        if (doc.Components?.Schemas is null)
        {
            return conflicts;
        }

        HashSet<string>? namespaceSegments = null;
        if (!string.IsNullOrEmpty(projectNamespace))
        {
            namespaceSegments = new HashSet<string>(
                projectNamespace!.Split('.'),
                StringComparer.Ordinal);
        }

        if (pathSegmentNamespaces is not null)
        {
            namespaceSegments ??= new HashSet<string>(StringComparer.Ordinal);
            foreach (var segment in pathSegmentNamespaces)
            {
                if (!string.IsNullOrEmpty(segment))
                {
                    namespaceSegments.Add(segment);
                }
            }
        }

        foreach (var schema in doc.Components.Schemas)
        {
            // Only a schema that becomes a type can clash with a namespace: an array or primitive alias
                // (Accounts: array of Account) is replaced by the type it names wherever it is referenced.
            if (ReservedSystemTypes.Contains(schema.Key) ||
                (namespaceSegments?.Contains(schema.Key) == true && IsGeneratedType(schema.Value)))
            {
                conflicts.Add(schema.Key);
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Whether a component schema is generated as a C# type. A component that only names an array or a
    /// primitive (<c>Accounts: array of Account</c>, <c>ExternalId: string</c>) is not - its users take the
    /// aliased type itself.
    /// </summary>
    public static bool IsGeneratedType(IOpenApiSchema schemaValue)
    {
        if (schemaValue is not OpenApiSchema schema || schema.Type is not { } type)
        {
            return true;
        }

        if (schema.Enum is { Count: > 0 } ||
            schema.AllOf is { Count: > 0 } ||
            schema.OneOf is { Count: > 0 } ||
            schema.AnyOf is { Count: > 0 } ||
            schema.HasPrefixItems())
        {
            return true;
        }

        var withoutNull = type & ~JsonSchemaType.Null;
        return withoutNull == default || withoutNull == JsonSchemaType.Object;
    }

    /// <summary>
    /// Creates a registry for a specific path segment using pre-scanned conflicts.
    /// This is a lightweight operation - O(1) - as the conflict detection was already done.
    /// </summary>
    /// <param name="conflicts">The pre-scanned set of conflicting schema names from <see cref="ScanForConflicts"/>.</param>
    /// <param name="projectName">The project name for namespace construction.</param>
    /// <param name="pathSegment">Optional path segment for segmented namespaces.</param>
    /// <param name="sharedSchemaNames">Optional names of the schemas generated into the shared models namespace.</param>
    /// <returns>A registry configured for the specified path segment.</returns>
    public static TypeConflictRegistry ForSegment(
        ISet<string> conflicts,
        string projectName,
        string? pathSegment = null,
        ISet<string>? sharedSchemaNames = null)
        => new(conflicts, projectName, pathSegment, sharedSchemaNames);

    /// <summary>
    /// Builds a conflict registry by scanning OpenAPI document schemas.
    /// This is a convenience method that combines ScanForConflicts + ForSegment.
    /// For better performance when processing multiple path segments, use ScanForConflicts once,
    /// then ForSegment for each segment.
    /// </summary>
    /// <param name="doc">The OpenAPI document containing schema definitions.</param>
    /// <param name="projectName">The project name for namespace construction.</param>
    /// <param name="pathSegment">Optional path segment for segmented namespaces.</param>
    /// <returns>A registry containing all detected conflicts.</returns>
    public static TypeConflictRegistry Build(
        OpenApiDocument doc,
        string projectName,
        string? pathSegment = null)
        => ForSegment(ScanForConflicts(doc, projectName), projectName, pathSegment);

    /// <summary>
    /// Checks if a type name conflicts with a reserved .NET system type.
    /// </summary>
    /// <param name="typeName">The type name to check.</param>
    /// <returns>True if the type name conflicts with a system type.</returns>
    public bool IsConflicting(string typeName)
        => conflictingSchemaNames.Contains(typeName);

    /// <summary>
    /// Gets the fully qualified name for a conflicting type.
    /// </summary>
    /// <param name="typeName">The type name to resolve.</param>
    /// <returns>The fully qualified name if conflicting, otherwise the original type name.</returns>
    public string GetFullyQualifiedName(string typeName)
    {
        if (!conflictingSchemaNames.Contains(typeName))
        {
            return typeName;
        }

        // Compute namespace on-demand. Callers normalize a redundant/collapsed segment to
        // string.Empty, so guard on emptiness (not just null) to avoid emitting "Generated..Models".
        // A schema used by several segments lives in the shared models namespace, whatever segment asks.
        return !string.IsNullOrEmpty(pathSegment) && sharedSchemaNames?.Contains(typeName) != true
            ? $"{projectName}.Generated.{pathSegment}.Models.{typeName}"
            : $"{projectName}.Generated.Models.{typeName}";
    }

    /// <summary>
    /// Resolves a type name, using full namespace qualification if it conflicts.
    /// </summary>
    /// <param name="typeName">The type name to resolve.</param>
    /// <returns>The resolved type name (fully qualified if conflicting).</returns>
    public string ResolveTypeName(string typeName)
        => IsConflicting(typeName) ? GetFullyQualifiedName(typeName) : typeName;
}