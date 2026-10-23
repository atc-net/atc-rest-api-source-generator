namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// What one type declaration contributes: the handlers it implements, the validator it is, or the namespace of the
/// handler interface it declares.
/// </summary>
internal sealed record DomainTypeFacts(
    EquatableArray<HandlerInfo> Handlers,
    ValidatorInfo? Validator,
    string? InterfaceNamespace);