namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Extracts security requirements from OpenAPI document and generates policy name constants.
/// </summary>
public static class SecurityPoliciesExtractor
{
    /// <summary>
    /// Extracts security policies from OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The project name for namespace.</param>
    /// <param name="includeDeprecated">Whether to include deprecated operations.</param>
    /// <returns>Generated code content for the security policies class, or null if no policies needed.</returns>
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

        // Generate the complete class content
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
                    var sortedScopes = scopes.OrderBy(s => s, StringComparer.Ordinal).ToList();
                    var policyName = GeneratePolicyName(schemeName, sortedScopes);

                    if (!policies.ContainsKey(policyName))
                    {
                        policies[policyName] = sortedScopes;
                    }

                    // Also create individual scope policies for single scopes
                    if (scopes.Count > 1)
                    {
                        foreach (var scope in scopes)
                        {
                            var singleScopePolicyName = GeneratePolicyName(schemeName, new[] { scope });
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
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.CodeDom.Compiler;");
        builder.AppendLine();
        builder.AppendLine($"namespace {projectName}.Generated.Security;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Authorization policy name constants generated from OpenAPI security requirements.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        builder.AppendLine("public static class SecurityPolicies");
        builder.AppendLine("{");

        var sortedPolicies = policies.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
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
            var constantName = GenerateConstantName(policyName);
            var scopeDescription = scopes.Count == 1
                ? $"Policy requiring scope: {scopes[0]}"
                : $"Policy requiring scopes: {string.Join(" AND ", scopes)} (all required)";

            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, $"/// {scopeDescription}");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, $"public const string {constantName} = \"{policyName}\";");
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Generates the policy name for a scheme and its scopes.
    /// </summary>
    /// <param name="schemeName">The security scheme name.</param>
    /// <param name="scopes">The required scopes.</param>
    /// <returns>A policy name string.</returns>
    public static string GeneratePolicyName(
        string schemeName,
        IEnumerable<string> scopes)
    {
        var scopeList = scopes.ToList();

        if (scopeList.Count == 0)
        {
            return schemeName;
        }

        if (scopeList.Count == 1)
        {
            return $"{schemeName}:{scopeList[0]}";
        }

        // Multiple scopes: AND logic
        return $"{schemeName}:{string.Join("+", scopeList.OrderBy(s => s, StringComparer.Ordinal))}";
    }

    /// <summary>
    /// Generates a valid C# constant name from a policy name.
    /// </summary>
    /// <param name="policyName">The policy name (e.g., "oauth2:pets:read").</param>
    /// <returns>A valid C# identifier (e.g., "OAuth2PetsRead").</returns>
    public static string GenerateConstantName(string policyName)
    {
        // Split by ':' and '+' separators
        var parts = policyName.Split(new[] { ':', '+' }, StringSplitOptions.RemoveEmptyEntries);

        var result = new StringBuilder();
        var partsList = parts.ToList();

        for (var i = 0; i < partsList.Count; i++)
        {
            var part = partsList[i];

            // Convert each part to PascalCase
            var pascalPart = part.ToPascalCaseForDotNet();

            // Handle '+' becoming 'And' for combined scopes
            if (result.Length > 0 && policyName.Contains("+") && i > 0)
            {
                // Check if this is after a '+' separator
                var beforeIndex = policyName.IndexOf(part, StringComparison.Ordinal);
                if (beforeIndex > 0 && policyName[beforeIndex - 1] == '+')
                {
                    result.Append("And");
                }
            }

            result.Append(pascalPart);
        }

        return result.ToString();
    }
}