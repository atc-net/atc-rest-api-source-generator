namespace Atc.Rest.Api.Generator.Services;

/// <summary>
/// Which handler each operation of a domain project gets, without a compilation: its name, the namespace a stub for it
/// is scaffolded in (by the sub-folder strategy), the handler registered for it - a user-written one before a leftover
/// stub of the same name - the file its stub is in, and the <c>ExecuteAsync</c> signature it must have.
/// </summary>
public static class HandlerScaffoldPlanner
{
    /// <summary>
    /// Plans the handlers of a domain project's operations (a deprecated one only when the configuration includes it).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="config">The domain project's configuration.</param>
    /// <param name="assemblyName">The domain project's assembly name, the root of a stub's namespace.</param>
    /// <param name="markerDirectory">The folder of the domain project's marker file.</param>
    /// <param name="implementedHandlers">The handlers the project has, by class name: every namespace one is in.</param>
    /// <returns>The plans, in operation order.</returns>
    public static List<HandlerPlan> Plan(
        OpenApiDocument openApiDoc,
        ServerDomainConfig config,
        string assemblyName,
        string markerDirectory,
        IReadOnlyDictionary<string, List<string>> implementedHandlers)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (implementedHandlers is null)
        {
            throw new ArgumentNullException(nameof(implementedHandlers));
        }

        var plans = new List<HandlerPlan>();
        foreach (var (path, method, operation) in openApiDoc.GetAllOperations())
        {
            if (operation is null || (!config.IncludeDeprecated && operation.Deprecated))
            {
                continue;
            }

            IOpenApiPathItem? pathItem = null;
            openApiDoc.Paths?.TryGetValue(path, out pathItem);

            var operationId = operation.GetOperationId(path, method);
            var handlerName = $"{operationId.ToPascalCaseForDotNet()}{config.HandlerSuffix}";
            var subFolder = SubFolderOf(path, operation, config.SubFolderStrategy);
            var scaffoldNamespace = NamespaceOf(assemblyName, config.GenerateHandlersOutput, subFolder);

            // A user-written handler is registered before a leftover stub of the same name in the scaffold namespace,
            // or the endpoint would answer with the stub (HTTP 501)
            string? implementedNamespace = null;
            var shadowsStub = false;
            if (implementedHandlers.TryGetValue(handlerName, out var namespaces) && namespaces.Count > 0)
            {
                implementedNamespace = namespaces.FirstOrDefault(ns => !string.Equals(ns, scaffoldNamespace, StringComparison.Ordinal))
                                       ?? namespaces[0];
                shadowsStub = namespaces.Count > 1 &&
                              namespaces.Contains(scaffoldNamespace, StringComparer.Ordinal) &&
                              !string.Equals(implementedNamespace, scaffoldNamespace, StringComparison.Ordinal);
            }

            var outputDirectory = Path.Combine(markerDirectory ?? string.Empty, config.GenerateHandlersOutput);
            if (subFolder.Length > 0)
            {
                outputDirectory = Path.Combine(outputDirectory, subFolder);
            }

            plans.Add(new HandlerPlan(
                path,
                operation,
                pathItem,
                operationId,
                handlerName,
                scaffoldNamespace,
                string.IsNullOrEmpty(implementedNamespace) ? scaffoldNamespace : implementedNamespace!,
                IsImplemented: implementedNamespace is not null,
                shadowsStub,
                Path.Combine(outputDirectory, handlerName + ".cs"),
                HandlerSignature.ExecuteAsync(operationId, HandlerSignature.TakesParameters(operation, pathItem?.Parameters))));
        }

        return plans;
    }

    /// <summary>
    /// The sub-folder of an operation's handler: none, the operation's first path segment, or its first tag.
    /// </summary>
    /// <param name="path">The operation's path.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="strategy">The sub-folder strategy.</param>
    /// <returns>The sub-folder, or an empty string.</returns>
    public static string SubFolderOf(
        string path,
        OpenApiOperation operation,
        SubFolderStrategyType strategy)
        => strategy switch
        {
            SubFolderStrategyType.FirstPathSegment => (path ?? string.Empty).Split(['/'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToPascalCaseForDotNet() ?? string.Empty,
            SubFolderStrategyType.OpenApiTag => operation?.GetFirstTag() ?? string.Empty,
            _ => string.Empty,
        };

    /// <summary>
    /// The namespace a stub is scaffolded in: the assembly's, its handler folder's (<c>ApiHandlers</c>) and its
    /// sub-folder's.
    /// </summary>
    /// <param name="assemblyName">The domain project's assembly name.</param>
    /// <param name="handlersOutput">The handler folder, relative to the marker file.</param>
    /// <param name="subFolder">The sub-folder, or an empty string.</param>
    /// <returns>The namespace.</returns>
    public static string NamespaceOf(
        string assemblyName,
        string handlersOutput,
        string subFolder)
    {
        var handlerFolder = (handlersOutput ?? string.Empty)
            .Replace("/", ".")
            .Replace("\\", ".");

        return string.IsNullOrEmpty(subFolder)
            ? $"{assemblyName}.{handlerFolder}"
            : $"{assemblyName}.{handlerFolder}.{subFolder.ToPascalCaseForDotNet()}";
    }
}