namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Defines the validation strategy for OpenAPI specification files.
/// </summary>
public enum ValidateSpecificationStrategy
{
    /// <summary>
    /// Skip all validation - no checks performed.
    /// Use for quick prototyping or when working with legacy specifications.
    /// </summary>
    None,

    /// <summary>
    /// Standard validation using Microsoft.OpenApi library errors only.
    /// Reports errors from apiDocumentContainer.Diagnostic.Errors.
    /// Validates that the OpenAPI spec is well-formed and parseable.
    /// </summary>
    Standard,

    /// <summary>
    /// Strict validation including standard validation plus custom ATC rules.
    /// Enforces naming conventions, best practices, and API design standards.
    /// Includes 50+ validation rules for security, schemas, operations, and naming.
    /// </summary>
    Strict,
}