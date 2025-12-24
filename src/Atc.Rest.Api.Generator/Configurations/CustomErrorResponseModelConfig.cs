namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Configuration for a custom error response model to use instead of ProblemDetails.
/// </summary>
public class CustomErrorResponseModelConfig
{
    /// <summary>
    /// The class name of the custom error response model. Default: "ErrorResponse".
    /// </summary>
    public string Name { get; set; } = "ErrorResponse";

    /// <summary>
    /// The description/documentation for the custom error response model.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The schema properties defining the error response model.
    /// Key is the property name, value defines the property configuration.
    /// </summary>
    public Dictionary<string, CustomErrorPropertyConfig>? Schema { get; set; }
}