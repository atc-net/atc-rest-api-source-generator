namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts parameters for generating the unified Add{ProjectName}Api() service collection extension method.
/// This provides a simplified API surface while keeping granular methods for experts.
/// </summary>
public static class UnifiedServiceCollectionExtractor
{
    /// <summary>
    /// Generates the unified service collection extension class.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document for auto-detection.</param>
    /// <param name="projectName">The project name for namespace and method naming.</param>
    /// <param name="config">The server configuration.</param>
    /// <param name="pathSegments">Optional list of path segments for using statements.</param>
    /// <returns>Generated code content for the unified service collection extension class.</returns>
    public static string Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        ServerConfig config,
        List<string>? pathSegments = null)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        return GenerateFileContent(projectName, CasingHelper.GetApiName(projectName, config.ApiName), pathSegments, ServerFeatures.Of(openApiDoc, config));
    }

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        string apiName,
        List<string>? pathSegments,
        ServerFeatures features)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public static class UnifiedServiceCollectionExtensions");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "public static IServiceCollection AddApi(");
        contentBuilder.AppendLine(8, "this IServiceCollection services,");
        contentBuilder.AppendLine(8, "Action<ApiServiceOptions>? configure = null)");
        contentBuilder.AppendLine(8, "var options = new ApiServiceOptions();");
        contentBuilder.AppendLine(8, "configure?.Invoke(options);");
        if (features.HasRateLimiting)
        {
            contentBuilder.AppendLine(8, "services.AddApiRateLimiting();");
        }

        if (features.HasVersioning)
        {
            contentBuilder.AppendLine(8, "services.AddApiVersioning();");
        }

        if (features.HasSecuritySchemes)
        {
            contentBuilder.AppendLine(8, "services.AddApiSecurityPolicies();");
        }

        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, NamespaceConstants.SystemCodeDomCompiler));
        builder.AppendLine($"using {projectName}.Generated;");

        // Add namespace for handlers
        if (pathSegments is { Count: > 0 })
        {
            // A segment that resolved away yields the root Handlers namespace, so dedupe to
            // avoid emitting the same using twice.
            var handlerNamespaces = pathSegments
                .Select(segment => NamespaceBuilder.Build(projectName, NamespaceBuilder.Categories.Handlers, segment))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ns => ns, StringComparer.Ordinal);

            foreach (var handlerNamespace in handlerNamespaces)
            {
                builder.AppendLine($"using {handlerNamespace};");
            }
        }
        else
        {
            builder.AppendLine($"using {projectName}.Generated.Handlers;");
        }

        // Add namespaces for optional features
        if (features.HasRateLimiting)
        {
            builder.AppendLine($"using {projectName}.Generated.RateLimiting;");
        }

        if (features.HasVersioning)
        {
            builder.AppendLine($"using {projectName}.Generated.Versioning;");
        }

        if (features.HasSecuritySchemes)
        {
            builder.AppendLine($"using {projectName}.Generated.Security;");
        }

        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.Extensions;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// The service registration of the {apiName} API: <see cref=\"Add{apiName}Api\"/>.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class UnifiedServiceCollectionExtensions");
        builder.AppendLine("{");

        // Generate Add{ProjectName}Api method
        GenerateAddApiMethod(builder, projectName, apiName, features);

        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the Add{ProjectName}Api method.
    /// </summary>
    private static void GenerateAddApiMethod(
        StringBuilder builder,
        string projectName,
        string apiName,
        ServerFeatures features)
    {
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, $"/// Adds the services the {apiName} API needs. Call it once; the handlers are registered next to it.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        builder.AppendLine(4, "/// <param name=\"configure\">");
        builder.AppendLine(4, "/// Turn off a registration the host makes itself (each has a switch in <see cref=\"ApiServiceOptions\"/>).");
        builder.AppendLine(4, "/// </param>");
        builder.AppendLine(4, "/// <returns>The service collection, for chaining.</returns>");

        // What this method registers - a list, so a tooltip shows one per line
        var registrations = new List<string>
        {
            "ProblemDetails (RFC 9457) for the errors raised outside the handlers",
        };
        if (features.HasXmlBodies)
        {
            registrations.Add("An XML problem writer, for a caller that prefers application/problem+xml");
        }

        registrations.Add(features.HasEnums
            ? "JSON options: every generated enum on the wire as the specification writes it; a polymorphic body's discriminator anywhere in the object"
            : "JSON options: a polymorphic body's discriminator anywhere in the object");
        if (features.HasRateLimiting)
        {
            registrations.Add("Rate limiting policies (x-ratelimit-*)");
        }

        if (features.HasOutputCaching)
        {
            registrations.Add("Output cache policies (x-cache-*)");
        }

        if (features.HasVersioning)
        {
            registrations.Add("API versioning (the marker's versioningStrategy)");
        }

        if (features.HasSecuritySchemes)
        {
            registrations.Add("Authorization policies of the security schemes");
        }

        builder.AppendLine(4, "/// <remarks>");
        XmlDocCommentHelper.AppendParagraph(builder, 4, "Registers:");
        XmlDocCommentHelper.AppendBulletList(builder, 4, registrations);
        XmlDocCommentHelper.AppendParagraph(builder, 4, "Registers no handler: add them next to it with the methods the handler project generates:");
        XmlDocCommentHelper.AppendBulletList(
            builder,
            4,
            features.HasValidation
                ? new[] { "<c>services.AddApiHandlersFrom{HandlerProject}()</c> - the handlers", "<c>services.AddApiValidatorsFrom{HandlerProject}()</c> - the validators" }
                : new[] { "<c>services.AddApiHandlersFrom{HandlerProject}()</c> - the handlers" });
        builder.AppendLine(4, "/// </remarks>");
        builder.AppendLine(4, $"public static IServiceCollection Add{apiName}Api(");
        builder.AppendLine(8, "this IServiceCollection services,");
        builder.AppendLine(8, "Action<ApiServiceOptions>? configure = null)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "var options = new ApiServiceOptions();");
        builder.AppendLine(8, "configure?.Invoke(options);");

        // Note: Handlers and validators are registered from Domain project, not here
        // because the Contracts project doesn't have access to Domain

        // ProblemDetails: framework and middleware errors get application/problem+json bodies
        builder.AppendLine();
        builder.AppendLine(8, "// Register RFC 9457 ProblemDetails for errors raised outside the handlers");
        builder.AppendLine(8, "if (options.UseProblemDetails)");
        builder.AppendLine(8, "{");
        if (features.HasXmlBodies)
        {
            // application/problem+xml when the Accept prefers it: ahead of the default JSON writer, which writes too
            builder.AppendLine(12, "// application/problem+xml (RFC 9457 Appendix B) when the request's Accept prefers it - ahead of the JSON writer");

            // Only an unkeyed IProblemDetailsWriter registration counts, and ImplementationType is read on no keyed
            // descriptor: Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0/8.0.1 throw there
            builder.AppendLine(12, $"if (!services.Any(d => d.ServiceType == typeof(global::Microsoft.AspNetCore.Http.IProblemDetailsWriter) && !d.IsKeyedService && d.ImplementationType == typeof(global::{projectName}.Generated.XmlProblemDetailsWriter)))");
            builder.AppendLine(12, "{");
            builder.AppendLine(16, $"services.Insert(0, ServiceDescriptor.Singleton<global::Microsoft.AspNetCore.Http.IProblemDetailsWriter, global::{projectName}.Generated.XmlProblemDetailsWriter>());");
            builder.AppendLine(12, "}");
            builder.AppendLine();
        }

        builder.AppendLine(12, "services.AddProblemDetails();");
        builder.AppendLine(8, "}");

        // System.Text.Json reads a type discriminator only when it is the first property, unless told otherwise
        builder.AppendLine();
        builder.AppendLine(8, "// A polymorphic body's discriminator may stand anywhere in the object, not only first");
        if (features.HasEnums)
        {
            // Ahead of any enum converter the host registers: the specification decides how an enum is written
            builder.AppendLine(8, "// Every generated enum on the wire as the specification writes it, whatever enum converter the host adds");
            builder.AppendLine(8, "services.ConfigureHttpJsonOptions(o =>");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, "o.SerializerOptions.AllowOutOfOrderMetadataProperties = true;");
            builder.AppendLine(12, $"o.SerializerOptions.Converters.Insert(0, new global::{projectName}.Generated.{EnumWireConverterExtractor.ClassName}());");
            builder.AppendLine(8, "});");
        }
        else
        {
            builder.AppendLine(8, "services.ConfigureHttpJsonOptions(o => o.SerializerOptions.AllowOutOfOrderMetadataProperties = true);");
        }

        // Optional: rate limiting
        if (features.HasRateLimiting)
        {
            builder.AppendLine();
            builder.AppendLine(8, "// Register rate limiting policies from OpenAPI x-ratelimit-* extensions");
            builder.AppendLine(8, "if (options.UseRateLimiting)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, "services.AddApiRateLimiting();");
            builder.AppendLine(8, "}");
        }

        // Optional: output caching (Map…Api() adds the middleware)
        if (features.HasOutputCaching)
        {
            builder.AppendLine();
            builder.AppendLine(8, "// Register the output cache policies from OpenAPI x-cache-* extensions");
            builder.AppendLine(8, "if (options.UseOutputCaching)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, $"global::{projectName}.Generated.Caching.OutputCachingServiceCollectionExtensions.AddApiOutputCaching(services);");
            builder.AppendLine(8, "}");
        }

        // Optional: versioning
        if (features.HasVersioning)
        {
            builder.AppendLine();
            builder.AppendLine(8, "// Register API versioning");
            builder.AppendLine(8, "if (options.UseVersioning)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, $"services.Add{apiName}ApiVersioning();");
            builder.AppendLine(8, "}");
        }

        // Optional: security policies
        if (features.HasSecuritySchemes)
        {
            builder.AppendLine();
            builder.AppendLine(8, "// Register security policies from OpenAPI security schemes");
            builder.AppendLine(8, "if (options.UseSecurity)");
            builder.AppendLine(8, "{");
            builder.AppendLine(12, "services.AddApiSecurityPolicies();");
            builder.AppendLine(8, "}");
        }

        builder.AppendLine();
        builder.AppendLine(8, "return services;");
        builder.AppendLine(4, "}");
    }
}