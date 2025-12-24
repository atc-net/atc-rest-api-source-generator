namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Represents information about a discovered endpoint interface for dependency injection.
/// </summary>
/// <param name="InterfaceName">The full interface name (e.g., "IListAccountsEndpoint").</param>
/// <param name="PathSegment">The path segment extracted from namespace (e.g., "Accounts").</param>
/// <param name="FieldName">The camelCase field name (e.g., "listAccountsEndpoint").</param>
/// <param name="FullNamespace">The full namespace of the interface.</param>
public sealed record EndpointInfo(
    string InterfaceName,
    string PathSegment,
    string FieldName,
    string FullNamespace);