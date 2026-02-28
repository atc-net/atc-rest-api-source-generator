namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Defines the style of React hooks to generate alongside the TypeScript client.
/// </summary>
public enum TypeScriptHooksStyle
{
    /// <summary>
    /// Do not generate any hooks (default).
    /// </summary>
    None,

    /// <summary>
    /// Generate TanStack Query (React Query) hooks wrapping each client method.
    /// GET operations become useQuery hooks; POST/PUT/PATCH/DELETE become useMutation hooks.
    /// </summary>
    ReactQuery,
}