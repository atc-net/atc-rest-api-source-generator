namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts Output Cache configuration and generates DI extension method for output cache policies.
/// </summary>
public static class OutputCacheDependencyInjectionExtractor
{
    /// <summary>
    /// Extracts Output Cache DI registration from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the Output Cache DI extension class, or null if no policies needed.</returns>
    public static string? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Collect all unique Output Cache policies
        var policies = OutputCachePoliciesExtractor.CollectPolicies(openApiDoc, includeDeprecated);

        if (policies.Count == 0)
        {
            return null;
        }

        // Generate the complete file content
        return GenerateFileContent(projectName, policies);
    }

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        Dictionary<string, CacheConfiguration> policies)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public static class OutputCachingServiceCollectionExtensions");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "public static IServiceCollection AddApiOutputCaching(this IServiceCollection services)");
        contentBuilder.AppendLine(8, "services.AddOutputCache(options =>");
        contentBuilder.AppendLine(12, "options.AddPolicy(OutputCachePolicies.Default, policy =>");
        contentBuilder.AppendLine(16, "policy.Expire(TimeSpan.FromSeconds(60))");
        contentBuilder.AppendLine(16, "policy.SetVaryByQuery(\"page\")");
        contentBuilder.AppendLine(16, "policy.SetVaryByHeader(\"Accept\")");
        contentBuilder.AppendLine(16, "policy.Tag(\"products\")");
        contentBuilder.AppendLine(12, "WebApplication app;");

        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        // The shared policy's types: the output cache's, HttpMethods / StatusCodes, ValueTask and CancellationToken
        var hasShared = policies.Values.Any(p => p.Shared);
        string[] namespaces = hasShared
            ?
            [
                NamespaceConstants.SystemCodeDomCompiler,
                $"{projectName}.Generated.Caching",
                "Microsoft.AspNetCore.OutputCaching",
                NamespaceConstants.MicrosoftAspNetCoreHttp,
                NamespaceConstants.SystemThreading,
                NamespaceConstants.SystemThreadingTasks,
            ]
            : [NamespaceConstants.SystemCodeDomCompiler, $"{projectName}.Generated.Caching"];

        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, namespaces));
        builder.AppendLine($"namespace {projectName}.Generated.Caching;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Extension methods for configuring API output caching policies.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class OutputCachingServiceCollectionExtensions");
        builder.AppendLine("{");
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Registers output cache policies for the API based on OpenAPI extensions.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        builder.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        builder.AppendLine(4, "public static IServiceCollection AddApiOutputCaching(this IServiceCollection services)");
        builder.AppendLine(4, "{");

        // Generate method content
        var methodContent = GenerateMethodContent(policies);
        builder.Append(methodContent);

        builder.AppendLine(4, "}");
        builder.AppendLine("}");

        if (hasShared)
        {
            builder.AppendLine();
            GenerateSharedOutputCachePolicy(builder);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Generates <c>SharedOutputCachePolicy</c>: the output cache for an answer that is the same for every caller
    /// (<c>x-cache-shared</c>). It is what ASP.NET Core's default policy does, without its rule that skips a request
    /// with an <c>Authorization</c> header or an authenticated user.
    /// </summary>
    private static void GenerateSharedOutputCachePolicy(StringBuilder builder)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// The output cache for an answer that is the same for every caller (<c>x-cache-shared</c>): ASP.NET Core's default");
        builder.AppendLine("/// policy without its rule that skips a request with an <c>Authorization</c> header or an authenticated user. Only a");
        builder.AppendLine("/// GET or HEAD is cached, and only a 200 without a cookie is stored; authorization has run before the cache is read.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public sealed class SharedOutputCachePolicy : IOutputCachePolicy");
        builder.AppendLine("{");
        builder.AppendLine(4, "/// <inheritdoc />");
        builder.AppendLine(4, "ValueTask IOutputCachePolicy.CacheRequestAsync(");
        builder.AppendLine(8, "OutputCacheContext context,");
        builder.AppendLine(8, "CancellationToken cancellation)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "var method = context.HttpContext.Request.Method;");
        builder.AppendLine(8, "var cacheable = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);");
        builder.AppendLine(8, "context.EnableOutputCaching = true;");
        builder.AppendLine(8, "context.AllowCacheLookup = cacheable;");
        builder.AppendLine(8, "context.AllowCacheStorage = cacheable;");
        builder.AppendLine(8, "context.AllowLocking = true;");
        builder.AppendLine();
        builder.AppendLine(8, "// Every query value is part of the key, as with the default policy (a vary-by-query narrows it)");
        builder.AppendLine(8, "context.CacheVaryByRules.QueryKeys = \"*\";");
        builder.AppendLine(8, "return ValueTask.CompletedTask;");
        builder.AppendLine(4, "}");
        builder.AppendLine();
        builder.AppendLine(4, "/// <inheritdoc />");
        builder.AppendLine(4, "ValueTask IOutputCachePolicy.ServeFromCacheAsync(");
        builder.AppendLine(8, "OutputCacheContext context,");
        builder.AppendLine(8, "CancellationToken cancellation)");
        builder.AppendLine(8, "=> ValueTask.CompletedTask;");
        builder.AppendLine();
        builder.AppendLine(4, "/// <inheritdoc />");
        builder.AppendLine(4, "ValueTask IOutputCachePolicy.ServeResponseAsync(");
        builder.AppendLine(8, "OutputCacheContext context,");
        builder.AppendLine(8, "CancellationToken cancellation)");
        builder.AppendLine(4, "{");
        builder.AppendLine(8, "// Only a 200 without a cookie is stored: an error, or an answer that sets a cookie, is not every caller's");
        builder.AppendLine(8, "var response = context.HttpContext.Response;");
        builder.AppendLine(8, "if (response.StatusCode != StatusCodes.Status200OK ||");
        builder.AppendLine(8, "    !Microsoft.Extensions.Primitives.StringValues.IsNullOrEmpty(response.Headers.SetCookie))");
        builder.AppendLine(8, "{");
        builder.AppendLine(12, "context.AllowCacheStorage = false;");
        builder.AppendLine(8, "}");
        builder.AppendLine();
        builder.AppendLine(8, "return ValueTask.CompletedTask;");
        builder.AppendLine(4, "}");
        builder.AppendLine("}");
    }

    /// <summary>
    /// Generates the method content for AddApiOutputCaching.
    /// </summary>
    private static string GenerateMethodContent(
        Dictionary<string, CacheConfiguration> policies)
    {
        var builder = new StringBuilder();

        builder.AppendLine(8, "services.AddOutputCache(options =>");
        builder.AppendLine(8, "{");

        var sortedPolicies = policies
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ToList();
        var tagConstants = CacheTagConstants.Assign(policies.Values);

        var isFirst = true;

        foreach (var policyKvp in sortedPolicies)
        {
            if (!isFirst)
            {
                builder.AppendLine();
            }

            isFirst = false;

            var policyName = policyKvp.Key;
            var config = policyKvp.Value;
            var constantName = OutputCachePoliciesExtractor.GenerateConstantName(policyName);

            GeneratePolicyConfiguration(builder, constantName, config, tagConstants);
        }

        builder.AppendLine(8, "});");
        builder.AppendLine();
        builder.AppendLine(8, "return services;");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the policy configuration for a specific policy.
    /// </summary>
    private static void GeneratePolicyConfiguration(
        StringBuilder builder,
        string constantName,
        CacheConfiguration config,
        IReadOnlyDictionary<string, string> tagConstants)
    {
        // A shared answer: built without the default policy, which skips every authenticated request
        var indent = config.Shared ? 20 : 16;
        if (config.Shared)
        {
            builder.AppendLine(12, "options.AddPolicy(");
            builder.AppendLine(16, $"OutputCachePolicies.{constantName},");
            builder.AppendLine(16, "policy =>");
            builder.AppendLine(16, "{");
            builder.AppendLine(indent, "policy.AddPolicy<SharedOutputCachePolicy>();");
        }
        else
        {
            builder.AppendLine(12, $"options.AddPolicy(OutputCachePolicies.{constantName}, policy =>");
            builder.AppendLine(12, "{");
        }

        builder.AppendLine(indent, $"policy.Expire(TimeSpan.FromSeconds({config.ExpirationSeconds}));");

        // Add VaryByQuery
        if (config.VaryByQuery.Count > 0)
        {
            var queryParams = string.Join(", ", config.VaryByQuery.Select(q => $"\"{q}\""));
            builder.AppendLine(indent, $"policy.SetVaryByQuery({queryParams});");
        }

        // Add VaryByHeader
        if (config.VaryByHeader.Count > 0)
        {
            var headers = string.Join(", ", config.VaryByHeader.Select(h => $"\"{h}\""));
            builder.AppendLine(indent, $"policy.SetVaryByHeader({headers});");
        }

        // Add VaryByRouteValue
        foreach (var routeValue in config.VaryByRoute)
        {
            builder.AppendLine(indent, $"policy.SetVaryByRouteValue(\"{routeValue}\");");
        }

        // Add Tags
        if (config.Tags.Count > 0)
        {
            var tags = string.Join(", ", config.Tags.Select(t => $"OutputCacheTags.{tagConstants[t]}"));
            builder.AppendLine(indent, $"policy.Tag({tags});");
        }

        if (config.Shared)
        {
            builder.AppendLine(16, "},");
            builder.AppendLine(16, "excludeDefaultPolicy: true);");
        }
        else
        {
            builder.AppendLine(12, "});");
        }
    }
}