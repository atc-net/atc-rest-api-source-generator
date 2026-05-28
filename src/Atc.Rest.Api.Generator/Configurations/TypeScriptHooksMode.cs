namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Selects which variant of React Query useQuery hooks to emit. Only meaningful when
/// <see cref="TypeScriptClientConfig.HooksStyle"/> is <see cref="TypeScriptHooksStyle.ReactQuery"/>.
/// </summary>
public enum TypeScriptHooksMode
{
    /// <summary>
    /// Emit only the standard <c>useQuery</c> variant (default). Consumers handle the
    /// <c>isPending</c> / <c>isError</c> states explicitly.
    /// </summary>
    Standard,

    /// <summary>
    /// Emit only the <c>useSuspenseQuery</c> variant. Consumers wrap call sites in a
    /// <c>&lt;Suspense&gt;</c> boundary and skip the <c>isPending</c> guards because the
    /// hook throws a promise while loading.
    /// </summary>
    Suspense,

    /// <summary>
    /// Emit both — standard <c>useXxx</c> AND a <c>useXxxSuspense</c> sibling per query
    /// operation. Useful when the same app mixes suspense-boundary call sites with
    /// imperative ones.
    /// </summary>
    Both,
}