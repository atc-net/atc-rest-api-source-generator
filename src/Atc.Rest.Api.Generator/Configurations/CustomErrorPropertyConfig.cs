namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Configuration for a property in the custom error response model.
/// </summary>
public class CustomErrorPropertyConfig
{
    /// <summary>
    /// The C# data type for this property (e.g., "string?", "int", "object?").
    /// </summary>
    public string DataType { get; set; } = "string?";

    /// <summary>
    /// The description/documentation for this property.
    /// </summary>
    public string? Description { get; set; }
}