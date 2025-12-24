namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Stores information about an inline schema discovered during result class extraction.
/// </summary>
/// <param name="TypeName">The generated type name for the inline schema.</param>
/// <param name="PathSegment">The path segment this schema belongs to.</param>
/// <param name="RecordParameters">The record parameters for code generation.</param>
public sealed record ResultClassInlineSchemaInfo(string TypeName, string PathSegment, RecordParameters RecordParameters);