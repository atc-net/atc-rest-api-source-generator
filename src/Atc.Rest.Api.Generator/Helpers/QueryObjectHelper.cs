namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The objects in the query a generated server binds (see <see cref="OpenApiParameterExtensions.QueryObject"/>): by
/// parameters record for the wire-binding step, by model for the model's <c>TryParse</c>.
/// <para>
/// The step collects an object's properties from their keys into one value of the parameter's name -
/// <c>property=value</c> after the item separator, each - and the model's generated <c>TryParse</c> reads that value,
/// so the parameter is bound by the framework as any other query parameter (and described as one).
/// </para>
/// </summary>
public static class QueryObjectHelper
{
    /// <summary>
    /// The objects in the query of every operation, by the name of its parameters record and the parameter's name.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The objects.</returns>
    public static Dictionary<string, Dictionary<string, OpenApiParameterExtensions.QueryObject>> ByParametersRecord(
        OpenApiDocument openApiDoc)
    {
        var result = new Dictionary<string, Dictionary<string, OpenApiParameterExtensions.QueryObject>>(StringComparer.Ordinal);
        foreach (var path in openApiDoc?.Paths ?? [])
        {
            if (path.Value?.Operations is null)
            {
                continue;
            }

            foreach (var operation in path.Value.Operations)
            {
                if (operation.Value is null)
                {
                    continue;
                }

                var objects = new Dictionary<string, OpenApiParameterExtensions.QueryObject>(StringComparer.Ordinal);
                foreach (var parameter in (path.Value.Parameters ?? []).Concat(operation.Value.Parameters ?? []))
                {
                    if (parameter.Resolve().Parameter?.GetQueryObject() is { } queryObject)
                    {
                        objects[queryObject.ParameterName] = queryObject;
                    }
                }

                if (objects.Count > 0)
                {
                    var operationId = OperationIdHelper.Effective(operation.Value, operation.Key.ToString(), path.Key);
                    result[$"{operationId!.ToPascalCaseForDotNet()}Parameters"] = objects;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// The models an object in the query is, by their C# name (one of the objects each: the properties are the model's).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The models.</returns>
    public static Dictionary<string, OpenApiParameterExtensions.QueryObject> Models(
        OpenApiDocument openApiDoc)
    {
        var result = new Dictionary<string, OpenApiParameterExtensions.QueryObject>(StringComparer.Ordinal);
        foreach (var queryObject in ByParametersRecord(openApiDoc).Values.SelectMany(objects => objects.Values))
        {
            result[OpenApiSchemaExtensions.SanitizeSchemaName(queryObject.SchemaName)] = queryObject;
        }

        return result;
    }
}