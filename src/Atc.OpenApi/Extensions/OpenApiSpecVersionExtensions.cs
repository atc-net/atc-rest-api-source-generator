namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension methods for <see cref="OpenApiSpecVersion"/>.
/// </summary>
public static class OpenApiSpecVersionExtensions
{
    /// <summary>
    /// Returns a short display string for the specification version (e.g. "3.1").
    /// Returns "3.x" when the version is null or unknown.
    /// </summary>
    public static string ToDisplayString(this OpenApiSpecVersion? specVersion)
        => specVersion switch
        {
            OpenApiSpecVersion.OpenApi2_0 => "2.0",
            OpenApiSpecVersion.OpenApi3_0 => "3.0",
            OpenApiSpecVersion.OpenApi3_1 => "3.1",
            OpenApiSpecVersion.OpenApi3_2 => "3.2",
            _ => "3.x",
        };
}
