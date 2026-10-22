namespace Atc.Rest.Api.SourceGenerator.Extensions;

/// <summary>
/// The packages a project references, as the generators ask about them.
/// </summary>
internal static class CompilationExtensions
{
    /// <param name="compilation">The project's compilation.</param>
    extension(Compilation compilation)
    {
        /// <summary>
        /// Whether the project references an assembly of this name (case-insensitive).
        /// </summary>
        /// <param name="assemblyName">The assembly name.</param>
        /// <returns><see langword="true"/> when it is referenced.</returns>
        public bool References(string assemblyName)
            => compilation.ReferencedAssemblyNames.Any(a => a.Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Whether the project references an assembly whose name starts with this prefix (case-insensitive), e.g.
        /// <c>Microsoft.AspNetCore</c>.
        /// </summary>
        /// <param name="prefix">The name prefix.</param>
        /// <returns><see langword="true"/> when one is referenced.</returns>
        public bool ReferencesAnyStartingWith(string prefix)
            => compilation.ReferencedAssemblyNames.Any(a => a.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The version of a referenced assembly.
        /// </summary>
        /// <param name="assemblyName">The assembly name.</param>
        /// <returns>The version, or <see langword="null"/> when it is not referenced.</returns>
        public Version? VersionOf(string assemblyName)
            => compilation.ReferencedAssemblyNames.FirstOrDefault(a => a.Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase))?.Version;
    }
}