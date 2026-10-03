namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension methods for <see cref="OpenApiSpecVersion"/>.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
public static class OpenApiSpecVersionExtensions
{
    /// <param name="specVersion">The specification version, or null when unknown.</param>
    extension(OpenApiSpecVersion? specVersion)
    {
        /// <summary>
        /// Returns a short display string for the specification version (e.g. "3.1").
        /// Returns "3.x" when the version is null or unknown.
        /// </summary>
        /// <returns>The display string.</returns>
        public string ToDisplayString()
            => specVersion switch
            {
                OpenApiSpecVersion.OpenApi2_0 => "2.0",
                OpenApiSpecVersion.OpenApi3_0 => "3.0",
                OpenApiSpecVersion.OpenApi3_1 => "3.1",
                OpenApiSpecVersion.OpenApi3_2 => "3.2",
                _ => "3.x",
            };
    }
}