namespace Atc.Rest.Api.Generator.Extensions;

/// <summary>
/// Extension methods for StringBuilder to append conditional segment namespace usings.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
public static class StringBuilderExtensions
{
    extension(StringBuilder sb)
    {
        /// <summary>
        /// Appends conditional using directives for path segment namespaces.
        /// Only includes namespace usings for types that actually exist based on the PathSegmentNamespaces flags.
        /// </summary>
        /// <param name="projectName">The project/root namespace name.</param>
        /// <param name="pathSegment">The path segment (e.g., "Pets"). Null or empty for root namespace.</param>
        /// <param name="namespaces">The namespace availability flags.</param>
        /// <param name="includeHandlers">Whether to include Handlers namespace (default: true).</param>
        /// <param name="includeModels">Whether to include Models namespace (default: true).</param>
        /// <param name="isGlobalUsing">Whether to use "global using" syntax (default: false).</param>
        public void AppendSegmentUsings(
            string projectName,
            string? pathSegment,
            PathSegmentNamespaces namespaces,
            bool includeHandlers = true,
            bool includeModels = true,
            bool isGlobalUsing = false)
        {
            var prefix = isGlobalUsing ? "global using " : "using ";
            var segmentPart = string.IsNullOrEmpty(pathSegment) ? string.Empty : $".{pathSegment}";

            if (includeHandlers && namespaces.HasHandlers)
            {
                sb.AppendLine($"{prefix}{projectName}.Generated{segmentPart}.Handlers;");
            }

            if (includeModels && namespaces.HasModels)
            {
                sb.AppendLine($"{prefix}{projectName}.Generated{segmentPart}.Models;");
            }

            if (namespaces.HasParameters)
            {
                sb.AppendLine($"{prefix}{projectName}.Generated{segmentPart}.Parameters;");
            }

            if (namespaces.HasResults)
            {
                sb.AppendLine($"{prefix}{projectName}.Generated{segmentPart}.Results;");
            }
        }
    }
}