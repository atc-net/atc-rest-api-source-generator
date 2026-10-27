namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// The handler of one operation of a domain project (<see cref="Services.HandlerScaffoldPlanner"/>).
/// </summary>
/// <param name="Path">The operation's path.</param>
/// <param name="Operation">The operation.</param>
/// <param name="PathItem">The operation's path item (its path-level parameters), or <see langword="null"/>.</param>
/// <param name="OperationId">The operation id (method and path for an operation without one).</param>
/// <param name="HandlerName">The handler's class name.</param>
/// <param name="ScaffoldNamespace">The namespace a stub for it is scaffolded in.</param>
/// <param name="Namespace">The namespace of the handler registered for it: the user's, else the stub's.</param>
/// <param name="IsImplemented">Whether the project has a handler of that name.</param>
/// <param name="ShadowsStub">Whether a user-written handler is registered before a leftover stub of the same name.</param>
/// <param name="FilePath">The file the stub is scaffolded in.</param>
/// <param name="ExpectedSignature">The <c>ExecuteAsync</c> signature the handler must have.</param>
public sealed record HandlerPlan(
    string Path,
    OpenApiOperation Operation,
    IOpenApiPathItem? PathItem,
    string OperationId,
    string HandlerName,
    string ScaffoldNamespace,
    string Namespace,
    bool IsImplemented,
    bool ShadowsStub,
    string FilePath,
    string ExpectedSignature);