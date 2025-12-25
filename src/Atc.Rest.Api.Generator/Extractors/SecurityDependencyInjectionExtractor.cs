namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts security configuration and generates DI extension method for authorization policies.
/// </summary>
public static class SecurityDependencyInjectionExtractor
{
    /// <summary>
    /// Extracts security DI registration from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the security DI extension class, or null if no policies needed.</returns>
    public static string? Extract(
        OpenApiDocument openApiDoc,
        string projectName,
        bool includeDeprecated = false)
    {
        if (openApiDoc == null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Collect all unique policies from security requirements
        var policies = CollectPolicies(openApiDoc, includeDeprecated);

        if (policies.Count == 0)
        {
            return null;
        }

        // Generate the complete file content
        return GenerateFileContent(projectName, policies);
    }

    /// <summary>
    /// Collects all unique policies from security requirements across all operations.
    /// </summary>
    private static Dictionary<string, List<string>> CollectPolicies(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
    {
        var policies = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (openApiDoc.Paths == null || openApiDoc.Paths.Count == 0)
        {
            return policies;
        }

        foreach (var pathPair in openApiDoc.Paths)
        {
            if (pathPair.Value is not OpenApiPathItem pathItem || pathItem.Operations == null)
            {
                continue;
            }

            foreach (var operationPair in pathItem.Operations)
            {
                var operation = operationPair.Value;

                // Skip deprecated operations if not including them
                if (!includeDeprecated && operation?.Deprecated == true)
                {
                    continue;
                }

                if (operation == null)
                {
                    continue;
                }

                // Extract security requirements for this operation
                var securityRequirements = operation.ExtractSecurityRequirements(openApiDoc);

                if (securityRequirements == null || securityRequirements.Count == 0)
                {
                    continue;
                }

                // Group requirements by scheme to handle AND logic within same scheme
                var schemeScopes = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                foreach (var requirement in securityRequirements)
                {
                    if (requirement.Scopes.Count == 0)
                    {
                        continue;
                    }

                    if (!schemeScopes.TryGetValue(requirement.SchemeName, out var scopeList))
                    {
                        scopeList = new List<string>();
                        schemeScopes[requirement.SchemeName] = scopeList;
                    }

                    foreach (var scope in requirement.Scopes)
                    {
                        if (!scopeList.Contains(scope, StringComparer.Ordinal))
                        {
                            scopeList.Add(scope);
                        }
                    }
                }

                // Create policies for each scheme's scopes
                foreach (var schemeKvp in schemeScopes)
                {
                    var schemeName = schemeKvp.Key;
                    var scopes = schemeKvp.Value;

                    if (scopes.Count == 0)
                    {
                        continue;
                    }

                    // Sort scopes for consistent policy names
                    var sortedScopes = scopes
                        .OrderBy(s => s, StringComparer.Ordinal)
                        .ToList();

                    var policyName = SecurityPoliciesExtractor.GeneratePolicyName(schemeName, sortedScopes);

                    if (!policies.ContainsKey(policyName))
                    {
                        policies[policyName] = sortedScopes;
                    }

                    // Also create individual scope policies for single scopes
                    if (scopes.Count > 1)
                    {
                        foreach (var scope in scopes)
                        {
                            var singleScopePolicyName = SecurityPoliciesExtractor.GeneratePolicyName(
                                schemeName,
                                new[] { scope });
                            if (!policies.ContainsKey(singleScopePolicyName))
                            {
                                policies[singleScopePolicyName] = new List<string> { scope };
                            }
                        }
                    }
                }
            }
        }

        return policies;
    }

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        Dictionary<string, List<string>> policies)
    {
        // Generate content first to analyze for required usings
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        contentBuilder.AppendLine("public static class SecurityServiceCollectionExtensions");
        contentBuilder.AppendLine("{");
        contentBuilder.AppendLine(4, "public static IServiceCollection AddApiSecurityPolicies(this IServiceCollection services)");
        contentBuilder.AppendLine(8, "services.AddAuthorization(options =>");
        contentBuilder.AppendLine(12, "options.AddPolicy(SecurityPolicies.Policy, policy =>");
        contentBuilder.AppendLine(16, "policy.RequireClaim(\"scope\", \"read\"));");
        contentBuilder.AppendLine(16, "policy.RequireAssertion(context =>");
        contentBuilder.AppendLine("}");
        var content = contentBuilder.ToString();

        // Build header with only required usings
        var builder = new StringBuilder();
        builder.Append(UsingStatementHelper.BuildHeader(content, "System.CodeDom.Compiler"));
        builder.AppendLine($"using {projectName}.Generated.Security;");
        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.Security;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Extension methods for configuring API security policies.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class SecurityServiceCollectionExtensions");
        builder.AppendLine("{");
        builder.AppendLine(4, "/// <summary>");
        builder.AppendLine(4, "/// Registers authorization policies for the API based on OpenAPI security requirements.");
        builder.AppendLine(4, "/// </summary>");
        builder.AppendLine(4, "/// <param name=\"services\">The service collection.</param>");
        builder.AppendLine(4, "/// <returns>The service collection for method chaining.</returns>");
        builder.AppendLine(4, "public static IServiceCollection AddApiSecurityPolicies(this IServiceCollection services)");
        builder.AppendLine(4, "{");

        // Generate method content
        var methodContent = GenerateMethodContent(policies);
        builder.Append(methodContent);

        builder.AppendLine(4, "}");
        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the method content for AddApiSecurityPolicies.
    /// </summary>
    private static string GenerateMethodContent(
        Dictionary<string, List<string>> policies)
    {
        var builder = new StringBuilder();

        builder.AppendLine(8, "services.AddAuthorization(options =>");
        builder.AppendLine(8, "{");

        var sortedPolicies = policies
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ToList();

        var isFirst = true;

        foreach (var policyKvp in sortedPolicies)
        {
            if (!isFirst)
            {
                builder.AppendLine();
            }

            isFirst = false;

            var policyName = policyKvp.Key;
            var scopes = policyKvp.Value;
            var constantName = SecurityPoliciesExtractor.GenerateConstantName(policyName);

            if (scopes.Count == 1)
            {
                // Single scope: RequireClaim
                builder.AppendLine(12, $"options.AddPolicy(SecurityPolicies.{constantName}, policy =>");
                builder.AppendLine(16, $"policy.RequireClaim(\"scope\", \"{scopes[0]}\"));");
            }
            else
            {
                // Multiple scopes (AND logic): RequireAssertion
                builder.AppendLine(12, "// Combined policy (AND logic - all scopes required)");
                builder.AppendLine(12, $"options.AddPolicy(SecurityPolicies.{constantName}, policy =>");
                builder.AppendLine(16, "policy.RequireAssertion(context =>");

                var scopeConditions = scopes
                    .Select(s => $"context.User.HasClaim(\"scope\", \"{s}\")")
                    .ToList();
                var condition = string.Join(" &&\n                    ", scopeConditions);

                builder.AppendLine(20, $"{condition}));");
            }
        }

        builder.AppendLine(8, "});");
        builder.AppendLine();
        builder.AppendLine(8, "return services;");

        return builder.ToString();
    }
}