// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts webhook request bodies and converts them to parameter classes.
/// </summary>
public static class WebhookParameterExtractor
{
    /// <summary>
    /// Extracts webhook parameter classes from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="systemTypeResolver">Resolver for system type conflicts.</param>
    /// <param name="includeDeprecated">Whether to include deprecated webhooks.</param>
    /// <returns>List of generated parameter class tuples (ClassName, Content).</returns>
    public static List<(string ClassName, string Content)>? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        SystemTypeConflictResolver systemTypeResolver,
        bool includeDeprecated = false)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (!openApiDoc.HasWebhooks)
        {
            return null;
        }

        var parametersList = new List<(string ClassName, string Content)>();
        var namespaceValue = NamespaceBuilder.ForWebhookParameters(projectName);

        foreach (var (webhookName, httpMethod, operation) in openApiDoc.GetAllWebhookOperations())
        {
            // Skip deprecated webhooks if not including them
            if (!includeDeprecated && operation.Deprecated)
            {
                continue;
            }

            // Only create parameter class if webhook has a request body
            if (operation.RequestBody is null)
            {
                continue;
            }

            var result = ExtractWebhookParameters(
                webhookName,
                operation,
                openApiDoc,
                projectName,
                namespaceValue,
                systemTypeResolver);

            if (result is not null)
            {
                parametersList.Add(result.Value);
            }
        }

        return parametersList.Count > 0 ? parametersList : null;
    }

    /// <summary>
    /// Whether the webhook gets a parameters class - a payload with a schema the generator types. The endpoint binds
    /// it exactly when this is true.
    /// </summary>
    /// <param name="operation">The webhook operation.</param>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>True when <c>{Op}WebhookParameters</c> is emitted.</returns>
    public static bool HasParameters(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc)
        => operation?.RequestBody?.Content is { Count: > 0 } &&
           PayloadSchema(operation) is { } schema &&
           schema.ResolveSchema(openApiDoc) is not null;

    private static (string ClassName, string Content)? ExtractWebhookParameters(
        string webhookName,
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        string projectName,
        string namespaceValue,
        SystemTypeConflictResolver systemTypeResolver)
    {
        if (operation.RequestBody?.Content is null || operation.RequestBody.Content.Count == 0)
        {
            return null;
        }

        // Get operation ID or generate from webhook name
        var operationId = operation.OperationId ?? $"On{webhookName.EnsureFirstCharacterToUpper()}";
        var className = $"{operationId.EnsureFirstCharacterToUpper()}WebhookParameters";

        // The payload's schema: a stream's record (JSON Lines, JSON Text Sequences - an itemSchema or a schema), else the
        // first media type's schema
        if (PayloadSchema(operation) is not { } schema)
        {
            return null;
        }

        var resolvedSchema = schema.ResolveSchema(openApiDoc);

        if (resolvedSchema is null)
        {
            return null;
        }

        // Get the type name for the request body
        var bodyTypeName = GetBodyTypeName(schema, resolvedSchema, systemTypeResolver);
        var isRequired = operation.RequestBody.Required;

        // A payload the generated reader reads: no [FromBody], and a list is the generated JsonRequestBodyList<T>
        var boundBody = XmlBodyHelper.ServerBoundWebhookBody(operation);
        if (boundBody is { IsList: true })
        {
            bodyTypeName = XmlBodyHelper.JsonListBodyTypeName(bodyTypeName);
        }

        // A stream of records is read record by record by the generated StreamedRequestBody<T> (bound by its BindAsync;
        // an optional one with no body is no records, never null)
        var isStreamed = operation.GetStreamedRequestBody() is not null;
        if (isStreamed)
        {
            bodyTypeName = StreamedRequestBodyExtractor.BodyTypeName(bodyTypeName);
            isRequired = true;
        }

        var propertyType = isRequired ? bodyTypeName : $"{bodyTypeName}?";

        // Get the correct model usings for this webhook's schemas
        var modelUsings = PathSegmentHelper.GetWebhookModelUsings(openApiDoc, projectName, operation);

        // Generate the complete class content
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");

        // Add model namespace usings (could be shared or segment-specific)
        foreach (var modelUsing in modelUsings)
        {
            sb.AppendLine($"using {modelUsing};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceValue};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Parameters for the {webhookName} webhook.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public sealed class {className}");
        sb.AppendLine("{");
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// The webhook payload.");
        sb.AppendLine(4, "/// </summary>");
        if (boundBody is null && !isStreamed)
        {
            sb.AppendLine(4, "[FromBody]");
        }

        if (isStreamed)
        {
            sb.AppendLine(4, $"public {propertyType} Payload {{ get; init; }} = default!;");
        }
        else if (isRequired)
        {
            sb.AppendLine(4, "[Required]");
            sb.AppendLine(4, $"public {propertyType} Payload {{ get; init; }} = default!;");
        }
        else
        {
            sb.AppendLine(4, $"public {propertyType}? Payload {{ get; init; }}");
        }

        sb.Append('}');

        return (className, sb.ToString());
    }

    private static IOpenApiSchema? PayloadSchema(OpenApiOperation operation)
    {
        if (operation.GetStreamedRequestBody() is { } streamed)
        {
            return streamed.ItemSchema;
        }

        var contentType = operation.RequestBody?.Content?.Keys.FirstOrDefault() ?? "application/json";
        return operation.RequestBody?.Content is { } content &&
               content.TryGetValue(contentType, out var mediaType)
            ? mediaType?.Schema
            : null;
    }

    private static string GetBodyTypeName(
        IOpenApiSchema schema,
        IOpenApiSchema resolvedSchema,
        SystemTypeConflictResolver systemTypeResolver)
    {
        // If it's a reference, use the reference name
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refName = schemaRef.Reference?.Id;
            if (!string.IsNullOrEmpty(refName))
            {
                return systemTypeResolver.EnsureFullNamespaceIfNeeded(refName!);
            }
        }

        // Otherwise, determine type from schema
        return resolvedSchema.ToCSharpType(isRequired: true);
    }
}