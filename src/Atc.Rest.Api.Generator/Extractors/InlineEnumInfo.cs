namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Stores information about an inline enum discovered during schema extraction.
/// Inline enums are defined directly on properties rather than as standalone schemas.
/// </summary>
/// <param name="TypeName">The generated type name for the inline enum (e.g., "ResendEventsRequestResourceType").</param>
/// <param name="PathSegment">The path segment this enum belongs to.</param>
/// <param name="EnumParameters">The enum parameters for code generation.</param>
/// <param name="ValuesKey">A key derived from sorted enum values for deduplication.</param>
public sealed record InlineEnumInfo(
    string TypeName,
    string PathSegment,
    EnumParameters EnumParameters,
    string ValuesKey);