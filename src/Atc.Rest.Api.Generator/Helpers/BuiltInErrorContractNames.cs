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
/// <para>
/// The server never materializes them either: it binds both names to
/// <c>Microsoft.AspNetCore.Mvc</c>, which every generated Results file imports, so a generated
/// namesake would make every reference ambiguous (CS0104).
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

    /// <summary>
    /// Whether a reserved schema is RFC 9457's shape: nothing but <c>type</c>, <c>title</c>, <c>detail</c>,
    /// <c>instance</c> (strings) and <c>status</c> (an integer) - and <c>errors</c> on a <c>ValidationProblemDetails</c>.
    /// The built-in type that takes its place holds all of it, so nothing of the schema is lost.
    /// </summary>
    /// <param name="schemaName">The schema name from the specification.</param>
    /// <param name="schema">The schema.</param>
    /// <returns><see langword="true"/> when the built-in type holds everything the schema declares.</returns>
    public static bool IsRfc9457Shape(
        string schemaName,
        IOpenApiSchema? schema)
    {
        if (!IsReserved(schemaName) ||
            schema is not { } actual ||
            actual.AllOf is { Count: > 0 } ||
            actual.OneOf is { Count: > 0 } ||
            actual.AnyOf is { Count: > 0 })
        {
            return false;
        }

        foreach (var property in actual.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            var fits = property.Key switch
            {
                "type" or "title" or "detail" or "instance" => property.Value.IsOfType(JsonSchemaType.String),
                "status" => property.Value.IsOfType(JsonSchemaType.Integer),
                "errors" => string.Equals(schemaName, "ValidationProblemDetails", StringComparison.Ordinal) &&
                            property.Value.IsOfType(JsonSchemaType.Object),
                _ => false,
            };

            if (!fits)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The reserved schemas that another schema of the document references (extends through <c>allOf</c>,
    /// holds as a property, …). Those are generated as models like any other schema - the models that use
    /// them need them - and files that also import <c>Microsoft.AspNetCore.Mvc</c> alias the name to the
    /// framework type (see <see cref="AppendFrameworkAliases"/>).
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>The referenced reserved schema names.</returns>
    public static HashSet<string> ReferencedByOtherSchemas(
        OpenApiDocument document)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (document?.Components?.Schemas is not { } schemas)
        {
            return result;
        }

        foreach (var schema in schemas.Where(s => !IsReserved(s.Key)).Select(s => s.Value))
        {
            CollectReservedReferences(schema, result, new HashSet<IOpenApiSchema>());
        }

        return result;
    }

    /// <summary>
    /// Appends <c>using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;</c> (and the same for
    /// <c>ValidationProblemDetails</c>) for every reserved name that is also generated as a model: the alias
    /// takes precedence over the imported models namespace, so the file keeps meaning the framework type.
    /// </summary>
    /// <param name="sb">The file content.</param>
    /// <param name="generatedReservedModels">The reserved names generated as models.</param>
    public static void AppendFrameworkAliases(
        StringBuilder sb,
        IEnumerable<string> generatedReservedModels)
    {
        if (sb is null || generatedReservedModels is null)
        {
            return;
        }

        foreach (var name in generatedReservedModels.OrderBy(n => n, StringComparer.Ordinal))
        {
            sb.AppendLine($"using {name} = Microsoft.AspNetCore.Mvc.{name};");
        }
    }

    private static void CollectReservedReferences(
        IOpenApiSchema? schema,
        HashSet<string> result,
        HashSet<IOpenApiSchema> visited)
    {
        if (schema is null || !visited.Add(schema))
        {
            return;
        }

        if (schema is OpenApiSchemaReference reference)
        {
            var name = reference.ComponentName;
            if (name is not null && IsReserved(name))
            {
                result.Add(name);
            }

            // Named schemas are walked from the components loop.
            return;
        }

        foreach (var child in (schema.AllOf ?? []).Concat(schema.OneOf ?? []).Concat(schema.AnyOf ?? []))
        {
            CollectReservedReferences(child, result, visited);
        }

        foreach (var property in schema.Properties?.Values ?? [])
        {
            CollectReservedReferences(property, result, visited);
        }

        CollectReservedReferences(schema.Items, result, visited);
        CollectReservedReferences(schema.AdditionalProperties, result, visited);
    }
}