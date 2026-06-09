namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Emits a <c>Webhooks.ts</c> file containing typed aliases for every inbound payload
/// declared on an operation's <c>callbacks:</c> block. Each alias resolves to the
/// callback request body's schema reference, so a consumer that registers a webhook
/// handler can write <c>req.body as CreateSubscriptionOnEventPayload</c> with full
/// IDE support — no hand-written DTO duplication.
/// </summary>
public static class TypeScriptWebhookExtractor
{
    /// <summary>
    /// Generates <c>Webhooks.ts</c> content, or <c>null</c> when the spec declares no
    /// callbacks (or none of them reference a named schema body). Inline-schema callback
    /// bodies are deliberately skipped — they have no canonical name to alias.
    /// </summary>
    public static string? Generate(
        OpenApiDocument openApiDoc,
        string? headerContent)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        if (openApiDoc.Paths == null || openApiDoc.Paths.Count == 0)
        {
            return null;
        }

        var entries = new List<(string Alias, string TargetType)>();
        var imports = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var (path, pathValue) in openApiDoc.Paths)
        {
            if (pathValue is not IOpenApiPathItem pathItem || pathItem.Operations == null)
            {
                continue;
            }

            foreach (var (verb, operation) in pathItem.Operations)
            {
                if (operation.Callbacks == null || operation.Callbacks.Count == 0)
                {
                    continue;
                }

                var operationIdPascal = operation
                    .GetOperationId(path, verb.ToString())
                    .ToCamelCase()
                    .ToTypeScriptIdentifier()
                    .ToPascalCase();

                foreach (var (callbackName, callback) in operation.Callbacks)
                {
                    AppendCallbackAliases(
                        operationIdPascal,
                        callbackName,
                        callback,
                        entries,
                        imports);
                }
            }
        }

        if (entries.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        if (imports.Count > 0)
        {
            sb.Append("import type { ").Append(string.Join(", ", imports)).AppendLine(" } from '../models';");
            sb.AppendLine();
        }

        foreach (var (alias, target) in entries)
        {
            sb.Append("export type ").Append(alias).Append(" = ").Append(target).AppendLine(";");
        }

        return sb.ToString();
    }

    private static void AppendCallbackAliases(
        string operationIdPascal,
        string callbackName,
        IOpenApiCallback callback,
        List<(string Alias, string TargetType)> entries,
        SortedSet<string> imports)
    {
        if (callback is not OpenApiCallback concreteCallback || concreteCallback.PathItems == null)
        {
            return;
        }

        var callbackPascal = callbackName.ToPascalCase().ToTypeScriptIdentifier();

        foreach (var (_, pathItem) in concreteCallback.PathItems)
        {
            if (pathItem.Operations == null)
            {
                continue;
            }

            foreach (var (_, callbackOp) in pathItem.Operations)
            {
                var (bodySchema, _) = callbackOp.GetRequestBodySchemaWithContentType();
                if (bodySchema is not OpenApiSchemaReference bodyRef)
                {
                    // Inline body schemas have no canonical name to alias. Skipping is
                    // safer than synthesizing — the model emitter doesn't produce a type
                    // for inline shapes either.
                    continue;
                }

                var refName = bodyRef.Reference.Id ?? bodyRef.Id;
                if (refName == null)
                {
                    continue;
                }

                var alias = operationIdPascal + callbackPascal + "Payload";
                entries.Add((alias, refName));
                imports.Add(refName);
            }
        }
    }
}