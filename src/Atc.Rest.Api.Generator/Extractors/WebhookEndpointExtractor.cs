// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts OpenAPI webhooks and converts them to endpoint registration code.
/// Webhooks in OpenAPI define how your API sends data to external systems,
/// but this generates endpoints for receiving webhook callbacks (the receiver side).
/// </summary>
public static class WebhookEndpointExtractor
{
    /// <summary>
    /// Extracts webhook endpoint registration class parameters from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing webhook definitions.</param>
    /// <param name="projectName">The name of the project (used for namespace).</param>
    /// <param name="config">The server configuration (used for webhook base path).</param>
    /// <param name="useValidationFilter">Whether a webhook's payload is validated by the ValidationFilter, as the
    /// operations' parameters and bodies are.</param>
    /// <returns>ClassParameters for the webhook endpoint registration class, or null if no webhooks exist.</returns>
    public static ClassParameters? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config,
        bool useValidationFilter = false)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (!openApiDoc.HasWebhooks)
        {
            return null;
        }

        // Generate method content
        var webhookBasePath = config.WebhookBasePath ?? "/webhooks";
        var methodContent = GenerateMethodContent(openApiDoc, projectName, config.IncludeDeprecated, webhookBasePath, useValidationFilter);
        if (string.IsNullOrEmpty(methodContent))
        {
            return null;
        }

        // Build method parameters
        var methodParams = new List<ParameterBaseParameters>
        {
            new(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: "this IEndpointRouteBuilder",
                IsNullableType: false,
                IsReferenceType: true,
                Name: "endpoints",
                DefaultValue: null),
        };

        var methodDocParams = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "endpoints", "The endpoint route builder." },
        };

        var method = new MethodParameters(
            DocumentationTags: new CodeDocumentationTags(
                summary: "Maps all webhook endpoints for receiving webhook callbacks.",
                parameters: methodDocParams,
                remark: $"Webhooks are registered under the {webhookBasePath} path group.",
                code: null,
                example: null,
                exceptions: null,
                @return: "The endpoint route builder for method chaining."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicStatic,
            ReturnGenericTypeName: null,
            ReturnTypeName: "IEndpointRouteBuilder",
            Name: $"Map{CasingHelper.GetApiName(projectName, config.ApiName)}Webhooks",
            Parameters: methodParams,
            AlwaysBreakDownParameters: false,
            UseExpressionBody: false,
            Content: methodContent);

        var headerContent = GenerateHeaderContent(projectName);

        return new ClassParameters(
            HeaderContent: headerContent,
            Namespace: NamespaceBuilder.ForWebhookEndpoints(projectName),
            DocumentationTags: new CodeDocumentationTags("Extension methods for registering webhook endpoints."),
            Attributes: new List<AttributeParameters>
            {
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            },
            DeclarationModifier: DeclarationModifiers.PublicStaticClass,
            ClassTypeName: "WebhookEndpointExtensions",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: new List<MethodParameters> { method },
            GenerateToStringMethod: false);
    }

    /// <summary>
    /// The model namespaces of the webhook payloads the generated reader reads (a model, a list, a stream of records),
    /// which the endpoints name in <c>Accepts&lt;T&gt;</c>.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name.</param>
    /// <returns>The namespaces, in order.</returns>
    public static IReadOnlyList<string> BoundPayloadModelUsings(
        OpenApiDocument openApiDoc,
        string projectName)
        => openApiDoc is null
            ? []
            : openApiDoc.GetAllWebhookOperations()
                .Where(w => XmlBodyHelper.ServerBoundWebhookBody(w.Operation) is not null ||
                            w.Operation.GetStreamedRequestBody() is not null)
                .SelectMany(w => PathSegmentHelper.GetWebhookModelUsings(openApiDoc, projectName, w.Operation))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(u => u, StringComparer.Ordinal)
                .ToList();

    private static string GenerateHeaderContent(string projectName)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.CodeDom.Compiler;");
        builder.AppendLine("using System.Threading;");
        builder.AppendLine("using Microsoft.AspNetCore.Builder;");
        builder.AppendLine("using Microsoft.AspNetCore.Http;");
        builder.AppendLine("using Microsoft.AspNetCore.Mvc;");
        builder.AppendLine("using Microsoft.AspNetCore.Routing;");
        builder.AppendLine($"using {projectName}.Generated.Webhooks.Handlers;");
        builder.AppendLine($"using {projectName}.Generated.Webhooks.Parameters;");
        builder.AppendLine($"using {projectName}.Generated.Webhooks.Results;");
        return builder.ToString();
    }

    private static string GenerateMethodContent(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated,
        string webhookBasePath,
        bool useValidationFilter)
    {
        var builder = new StringBuilder();

        // Create a webhooks route group
        builder.AppendLine($"var webhooksGroup = endpoints.MapGroup(\"{webhookBasePath}\")");
        builder.AppendLine(4, ".WithTags(\"Webhooks\");");
        builder.AppendLine();

        var isFirst = true;

        foreach (var (webhookName, httpMethod, operation) in openApiDoc.GetAllWebhookOperations())
        {
            // Skip deprecated webhooks if not including them
            if (!includeDeprecated && operation.Deprecated)
            {
                continue;
            }

            GenerateWebhookEndpoint(builder, openApiDoc, projectName, webhookName, httpMethod, operation, isFirst, useValidationFilter);
            isFirst = false;
        }

        builder.AppendLine();
        builder.Append("return endpoints;");

        return builder.ToString();
    }

    private static void GenerateWebhookEndpoint(
        StringBuilder builder,
        OpenApiDocument openApiDoc,
        string projectName,
        string webhookName,
        string httpMethod,
        OpenApiOperation operation,
        bool isFirst,
        bool useValidationFilter)
    {
        // Get operation ID or generate from webhook name
        var operationId = OperationNamesExtractor.WebhookEndpointName(operation, webhookName);
        var handlerName = operationId.EnsureFirstCharacterToUpper();
        var handlerInterfaceName = $"I{handlerName}WebhookHandler";

        // Webhooks typically use POST, but the declared verb is what the subscriber will call.
        // Falling back to MapPost for anything unrecognised meant an OpenAPI 3.2 query: webhook,
        // or a custom additionalOperations verb, was served on the wrong method with no diagnostic.
        // Standard verbs keep their dedicated Map{Verb}; the rest go through MapMethods, exactly as
        // path operations do.
        var isStandardVerb = EndpointMapHelper.IsStandardMappableMethod(httpMethod);
        var mapMethod = isStandardVerb
            ? $"Map{httpMethod.ToUpperInvariant()[0]}{httpMethod.Substring(1).ToLowerInvariant()}"
            : "MapMethods";

        // The webhook binds its parameters class exactly when one is emitted (a payload with a schema - a 3.2 itemSchema
        // too)
        var hasParameters = WebhookParameterExtractor.HasParameters(operation, openApiDoc);
        var streamedBody = hasParameters ? operation.GetStreamedRequestBody() : null;
        var parameterTypeName = hasParameters ? $"{handlerName}WebhookParameters" : null;

        // Add blank line before each endpoint except the first one
        if (!isFirst)
        {
            builder.AppendLine();
        }

        // Build the route path (use lowercase with hyphens for webhook name)
        var routePath = $"/{ConvertToKebabCase(webhookName)}";

        // Generate parameters for the lambda
        var parameters = new List<string>();

        if (hasParameters)
        {
            parameters.Add($"[AsParameters] {parameterTypeName} parameters");
        }

        parameters.Add($"{handlerInterfaceName} handler");
        parameters.Add("CancellationToken cancellationToken");

        // Build handler call parameters
        var handlerParams = new List<string>();
        if (hasParameters)
        {
            handlerParams.Add("parameters");
        }

        handlerParams.Add("cancellationToken");

        // Start endpoint mapping
        builder.AppendLine("webhooksGroup");
        builder.AppendLine(4, $".{mapMethod}(");
        builder.AppendLine(8, $"\"{routePath}\",");

        // MapMethods takes the verb list between the pattern and the handler.
        if (!isStandardVerb)
        {
            builder.AppendLine(8, $"new[] {{ \"{httpMethod.ToUpperInvariant()}\" }},");
        }

        builder.AppendLine(8, "async (");

        // Output each parameter on its own line
        for (var i = 0; i < parameters.Count; i++)
        {
            var separator = i < parameters.Count - 1 ? "," : string.Empty;
            builder.AppendLine(12, $"{parameters[i]}{separator}");
        }

        builder.AppendLine(8, ") =>");
        builder.AppendLine(8, "{");

        // A stream of records: the rules the specification declares for it are set before the handler runs
        if (streamedBody is not null)
        {
            var expect = StreamedRequestBodyExtractor.ExpectStatement(
                streamedBody,
                useValidationFilter && ResponseBodyClassifier.IsModelSchema(streamedBody.ItemSchema),
                "parameters.Payload",
                useValidationFilter ? StreamedRequestBodyExtractor.RecordAttributeExpressions(streamedBody.ItemSchema) : [],
                StreamedRequestBodyExtractor.RecordsMayBeNull(streamedBody.ItemSchema));
            foreach (var line in expect.Split('\n'))
            {
                builder.AppendLine(12, line.TrimEnd('\r'));
            }

            builder.AppendLine();
        }

        // Build the handler call
        builder.AppendLine(12, "return await handler.ExecuteAsync(");
        for (var i = 0; i < handlerParams.Count; i++)
        {
            var separator = i < handlerParams.Count - 1 ? "," : ");";
            builder.AppendLine(16, $"{handlerParams[i]}{separator}");
        }

        builder.AppendLine(8, "})");

        // Add operation metadata
        if (!string.IsNullOrEmpty(operation.Summary))
        {
            builder.AppendLine(4, $".WithSummary({CSharpLiteralHelper.ToStringLiteral(operation.Summary!.Trim())})");
        }

        builder.AppendLine(4, $".WithName({OperationNamesExtractor.WebhookConstantReference(projectName, operationId)})");

        // The operation's own x- extensions, for a middleware or a filter to read
        foreach (var call in OperationExtensionMetadataExtractor.MetadataCalls(operation, projectName))
        {
            builder.AppendLine(4, call);
        }

        // The payload is validated as an operation's body is: its constraints, keyed by its wire names
        if (useValidationFilter && hasParameters)
        {
            builder.AppendLine(4, $".AddEndpointFilter<ValidationFilter<{parameterTypeName}>>()");
        }

        // A payload the generated reader reads (a JSON model, a polymorphic base or a list): a value it cannot read is
        // a problem naming it, and Accepts names the body for the runtime OpenAPI document
        if (XmlBodyHelper.ServerBoundWebhookBody(operation) is { } boundBody)
        {
            builder.AppendLine(4, ".WithRequestBodyProblems()");
            builder.AppendLine(4, $".Accepts<{XmlBodyHelper.ModelTypeName(operation.RequestBody!.Content!, boundBody)}>({string.Join(", ", boundBody.JsonOnlyMediaTypes!.Select(m => $"\"{m}\""))})");
        }
        else if (streamedBody is not null)
        {
            // A stream of records: a record that cannot be read or is not valid is a problem naming it
            builder.AppendLine(4, ".WithRequestBodyProblems()");
            builder.AppendLine(4, $".Accepts<{streamedBody.ItemSchema.ToCSharpType(isRequired: true).TrimEnd('?')}>({string.Join(", ", streamedBody.AcceptedMediaTypes.Select(m => $"\"{m}\""))})");
        }
        else if (TextRequestBodyExtractor.AcceptedMediaTypes(operation) is { Count: > 0 } textMediaTypes)
        {
            // Text: a charset the server does not know is a problem; Accepts names its media types
            builder.AppendLine(4, ".WithRequestBodyProblems()");
            builder.AppendLine(4, $".Accepts<string>({string.Join(", ", textMediaTypes.Select(m => $"\"{m}\""))})");
        }

        // Add Produces metadata based on responses
        GenerateProducesMetadata(builder, operation);

        // Add final semicolon
        builder.AppendLine(";");
    }

    private static void GenerateProducesMetadata(
        StringBuilder builder,
        OpenApiOperation operation)
    {
        if (operation.Responses is null || operation.Responses.Count == 0)
        {
            // Default to 200 OK for webhooks
            builder.Append(4, ".Produces(StatusCodes.Status200OK)");
            return;
        }

        var producesLines = new List<string>();

        foreach (var response in operation.Responses)
        {
            var statusCode = response.Key;

            // Error responses (4xx, 5xx, default): the same metadata the endpoint definitions emit
            if (ErrorResponseShapeResolver.IsErrorStatusCode(statusCode))
            {
                var shape = ErrorResponseShapeResolver.Resolve(response.Value);
                var modelTypeName = shape is { Kind: ErrorResponseKind.JsonObject or ErrorResponseKind.XmlObject, Schema: not null }
                    ? shape.Schema.ToCSharpType(true)
                    : null;
                producesLines.Add("    " + ErrorResponseShapeResolver.ToProducesCall(statusCode, shape, modelTypeName));
                continue;
            }

            if (!int.TryParse(statusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                continue;
            }

            // The StatusCodes constant, or the numeric literal for codes without one
            producesLines.Add($"    .Produces({HttpStatusCodeHelper.ToStatusCodesExpression(code)})");
        }

        // Output all lines
        for (var i = 0; i < producesLines.Count; i++)
        {
            if (i < producesLines.Count - 1)
            {
                builder.AppendLine(producesLines[i]);
            }
            else
            {
                builder.Append(producesLines[i]);
            }
        }
    }

    /// <summary>
    /// Converts a string to kebab-case (e.g., "newPet" -> "new-pet").
    /// </summary>
    private static string ConvertToKebabCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    result.Append('-');
                }

                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}