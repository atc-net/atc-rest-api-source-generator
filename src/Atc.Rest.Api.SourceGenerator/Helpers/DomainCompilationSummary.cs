namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Represents a handler's identity: name and containing namespace.
/// </summary>
internal readonly record struct HandlerInfo(string Name, string Namespace) : IEquatable<HandlerInfo>;

/// <summary>
/// Represents a validator's identity: name, namespace, and the model type it validates.
/// </summary>
internal readonly record struct ValidatorInfo(string Name, string Namespace, string ModelType) : IEquatable<ValidatorInfo>;

/// <summary>
/// A stable, equatable summary of compilation state relevant to the domain generator.
/// Extracted in a cached <c>.Select()</c> transform so that the downstream
/// <c>RegisterSourceOutput</c> callback only fires when meaningful compilation
/// state actually changes (new handlers, new validators, or changed assembly references),
/// rather than on every C# keystroke.
/// </summary>
internal sealed record DomainCompilationSummary(
    bool HasAspNetCore,
    string? AssemblyName,
    EquatableArray<HandlerInfo> ImplementedHandlers,
    EquatableArray<string> InterfaceNamespaces,
    EquatableArray<ValidatorInfo> ImplementedValidators);