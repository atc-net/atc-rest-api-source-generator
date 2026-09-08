namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Names of error-contract types that the generator emits itself in EndpointPerOperation mode.
/// <para>
/// In that mode <c>ProblemDetails</c> and <c>ValidationProblemDetails</c> are generated into
/// <c>{projectName}.Generated</c> and are wired into the generated endpoint results and the
/// <c>ProblemDetailsFactory</c>. A same-named schema in the specification must therefore not be
/// materialized as well: because every consumer lives under <c>{projectName}.Generated.*</c>, C#
/// resolves the unqualified name against the enclosing namespace and always binds to the built-in,
/// leaving the spec-derived record emitted but unreferenced.
/// </para>
/// </summary>
public static class BuiltInErrorContractNames
{
    /// <summary>
    /// Schema names reserved by the built-in EndpointPerOperation error contracts.
    /// </summary>
    public static readonly IReadOnlyCollection<string> All =
    [
        "ProblemDetails",
        "ValidationProblemDetails",
    ];

    /// <summary>
    /// Determines whether <paramref name="schemaName"/> collides with a built-in error contract.
    /// </summary>
    /// <param name="schemaName">The schema name from the specification.</param>
    /// <returns><see langword="true"/> when the name is reserved by the generator.</returns>
    public static bool IsReserved(string schemaName)
        => All.Contains(schemaName, StringComparer.Ordinal);
}