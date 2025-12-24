namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Helper class for JSON serialization operations.
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// Shared JSON options for deserializing marker file configurations.
    /// </summary>
    public static JsonSerializerOptions ConfigOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}