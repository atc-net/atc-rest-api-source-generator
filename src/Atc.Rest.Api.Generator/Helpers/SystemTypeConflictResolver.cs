namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Resolves conflicts between model type names and .NET system types.
/// Instance-based for thread safety in parallel source generator execution.
/// </summary>
/// <remarks>
/// When an OpenAPI schema defines a type with the same name as a common .NET type
/// (e.g., "Task"), this resolver ensures the .NET system type is fully qualified
/// to avoid ambiguity.
/// </remarks>
public sealed class SystemTypeConflictResolver
{
    /// <summary>
    /// Mapping of reserved .NET type names to their fully qualified names.
    /// </summary>
    private static readonly Dictionary<string, string> ReservedSystemTypes = new(StringComparer.Ordinal)
    {
        [nameof(Task)] = "System.Threading.Tasks.Task",
        [nameof(Action)] = "System.Action",
        ["Func"] = "System.Func",
        [nameof(Type)] = "System.Type",
        [nameof(Exception)] = "System.Exception",
        [nameof(Object)] = "System.Object",
        [nameof(String)] = "System.String",
        [nameof(Guid)] = "System.Guid",
        [nameof(DateTime)] = "System.DateTime",
        [nameof(TimeSpan)] = "System.TimeSpan",
        [nameof(Uri)] = "System.Uri",
        [nameof(Version)] = "System.Version",
        ["File"] = "System.IO.File",
        ["Directory"] = "System.IO.Directory",
        ["Path"] = "System.IO.Path",
        ["Stream"] = "System.IO.Stream",
        ["Thread"] = "System.Threading.Thread",
        ["Timer"] = "System.Threading.Timer",
        ["Environment"] = "System.Environment",
        ["Console"] = "System.Console",
        ["Math"] = "System.Math",
        ["Random"] = "System.Random",
        ["Array"] = "System.Array",
        ["Attribute"] = "System.Attribute",
        ["Buffer"] = "System.Buffer",
        ["Convert"] = "System.Convert",
        ["Delegate"] = "System.Delegate",
        ["Enum"] = "System.Enum",
        ["GC"] = "System.GC",
        ["Monitor"] = "System.Threading.Monitor",
        ["Mutex"] = "System.Threading.Mutex",
        ["EventArgs"] = "System.EventArgs",
    };

    /// <summary>
    /// Set of model names that conflict with reserved .NET types.
    /// </summary>
    private readonly HashSet<string> conflictingModelNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemTypeConflictResolver"/> class.
    /// </summary>
    /// <param name="modelNames">The collection of model names from the OpenAPI specification.</param>
    public SystemTypeConflictResolver(IEnumerable<string> modelNames)
    {
        // Only store model names that conflict with reserved types
        conflictingModelNames = new HashSet<string>(
            modelNames.Where(ReservedSystemTypes.ContainsKey),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns the fully qualified .NET namespace if the type name conflicts with a registered model.
    /// </summary>
    /// <param name="typeName">The type name to check (e.g., "Task").</param>
    /// <returns>
    /// The fully qualified type name if there's a conflict (e.g., "System.Threading.Tasks.Task"),
    /// otherwise the original type name.
    /// </returns>
    /// <example>
    /// <code>
    /// // If "Task" model exists in OpenAPI spec:
    /// resolver.EnsureFullNamespaceIfNeeded("Task") // Returns "System.Threading.Tasks.Task"
    ///
    /// // If no "Task" model exists:
    /// resolver.EnsureFullNamespaceIfNeeded("Task") // Returns "Task"
    /// </code>
    /// </example>
    public string EnsureFullNamespaceIfNeeded(string typeName)
    {
        if (conflictingModelNames.Contains(typeName) &&
            ReservedSystemTypes.TryGetValue(typeName, out var fullName))
        {
            return fullName;
        }

        return typeName;
    }

    /// <summary>
    /// Checks if a model name conflicts with a reserved .NET type.
    /// </summary>
    /// <param name="modelName">The model name to check.</param>
    /// <returns>True if the model name conflicts with a reserved .NET type; otherwise, false.</returns>
    public bool HasConflict(string modelName)
        => conflictingModelNames.Contains(modelName);

    /// <summary>
    /// Gets the count of conflicting model names.
    /// </summary>
    public int ConflictCount => conflictingModelNames.Count;
}