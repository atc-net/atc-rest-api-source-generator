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
/// The references and the assembly name the domain generator reads from the compilation.
/// </summary>
internal readonly record struct DomainReferences(
    bool HasAspNetCore,
    bool HasLogging,
    string? AssemblyName);

/// <summary>
/// A stable, equatable summary of compilation state relevant to the domain generator: the facts of the project's
/// type declarations (read per declaration, see <see cref="DomainTypeScanner"/>) and its references. The downstream
/// <c>RegisterSourceOutput</c> callback only fires when it changes (new handlers, new validators, or changed assembly
/// references), not on every C# keystroke.
/// </summary>
internal sealed record DomainCompilationSummary(
    bool HasAspNetCore,
    bool HasLogging,
    string? AssemblyName,
    EquatableArray<HandlerInfo> ImplementedHandlers,
    EquatableArray<string> InterfaceNamespaces,
    EquatableArray<ValidatorInfo> ImplementedValidators);