namespace Atc.Rest.Api.Generator.Extensions;

/// <summary>
/// Extension members for the marker settings that build on Atc.Rest.MinimalApi.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
public static class MinimalApiPackageModeExtensions
{
    /// <param name="mode">The setting's mode.</param>
    extension(MinimalApiPackageMode mode)
    {
        /// <summary>
        /// Whether the generated code builds on Atc.Rest.MinimalApi for this setting: always when enabled, never when
        /// disabled, and when the package is referenced for auto.
        /// </summary>
        /// <param name="packageReferenced">Whether the project references Atc.Rest.MinimalApi.</param>
        /// <returns><see langword="true"/> when the generated code uses the package.</returns>
        public bool UsesPackage(bool packageReferenced)
            => mode switch
            {
                MinimalApiPackageMode.Enabled => true,
                MinimalApiPackageMode.Disabled => false,
                _ => packageReferenced,
            };

        /// <summary>
        /// Whether the setting asks for Atc.Rest.MinimalApi although the project does not reference it - the generated
        /// code would not build.
        /// </summary>
        /// <param name="packageReferenced">Whether the project references Atc.Rest.MinimalApi.</param>
        /// <returns><see langword="true"/> when the setting is enabled and the package is missing.</returns>
        public bool MissesPackage(bool packageReferenced)
            => mode == MinimalApiPackageMode.Enabled && !packageReferenced;
    }
}