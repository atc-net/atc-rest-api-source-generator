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
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        // Collect all unique policies from security requirements
        var (policies, deprecatedPolicies, mutualTlsPolicies, metadataUrls, anyOfPolicies) = CollectPolicies(openApiDoc, includeDeprecated);

        if (policies.Count == 0 && anyOfPolicies.Count == 0)
        {
            return null;
        }

        // Generate the complete class content
        return GenerateFileContent(projectName, policies, deprecatedPolicies, mutualTlsPolicies, metadataUrls, anyOfPolicies);
    }

    /// <summary>
    /// Whether the document generates a <c>SecurityPolicies</c> class: some operation requires a scope.
    /// </summary>
    public static bool HasPolicies(
        OpenApiDocument openApiDoc,
        bool includeDeprecated = false)
    {
        var (policies, _, _, _, anyOfPolicies) = CollectPolicies(openApiDoc, includeDeprecated);
        return policies.Count > 0 || anyOfPolicies.Count > 0;
    }

    /// <summary>
    /// The policy an operation's security requirements call for beyond being authenticated: the scope policy of
    /// its one requirement with scopes, or - when the alternatives differ - an any-of policy that is met by any of
    /// them (a requirement without scopes by the caller its scheme authenticated, one with scopes by a caller its
    /// scheme authenticated who has them all). <see langword="null"/> when no requirement has scopes.
    /// </summary>
    /// <param name="requirements">The operation's requirements (alternatives).</param>
    /// <returns>The policy name, or <see langword="null"/>.</returns>
    public static string? OperationPolicyName(
        IReadOnlyList<SecurityRequirement> requirements)
    {
        if (requirements is null || requirements.All(r => r.Scopes.Count == 0))
        {
            return null;
        }

        var names = requirements
            .Select(AlternativeName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        return names.Count == 1 ? names[0] : string.Join("|", names);
    }

    /// <summary>
    /// One alternative of an operation's security: the scheme the host authenticates with, and the scopes it must find.
    /// </summary>
    internal sealed record PolicyAlternative(
        string AuthenticationScheme,
        IReadOnlyList<string> Scopes);

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
    /// Generates a valid C# constant name from a policy name: each part in PascalCase, <c>+</c> read as
    /// <c>And</c>, <c>|</c> as <c>Or</c>.
    /// </summary>
    /// <param name="policyName">The policy name (e.g., "oauth2:pets:read", "api_key|oauth2:pets:read").</param>
    /// <returns>A valid C# identifier (e.g., "Oauth2PetsRead", "ApiKeyOrOauth2PetsRead").</returns>
    public static string GenerateConstantName(string policyName)
    {
        if (policyName is null)
        {
            throw new ArgumentNullException(nameof(policyName));
        }

        var result = new StringBuilder();
        var part = new StringBuilder();
        var joiner = string.Empty;
        foreach (var character in policyName + ":")
        {
            if (character is not (':' or '+' or '|'))
            {
                part.Append(character);
                continue;
            }

            if (part.Length > 0)
            {
                result.Append(joiner).Append(part.ToString().ToPascalCaseForDotNet());
                part.Clear();
            }

            joiner = character switch
            {
                '+' => "And",
                '|' => "Or",
                _ => string.Empty,
            };
        }

        return result.ToString();
    }

    /// <summary>
    /// Collects all unique policies from security requirements across all operations.
    /// Returns the policy dict, deprecated policy names, mutualTLS policy names, OAuth2 metadata URLs, and the
    /// any-of policies (an operation whose alternatives differ) with their alternatives.
    /// </summary>
    internal static (Dictionary<string, List<string>> Policies, HashSet<string> DeprecatedPolicies, HashSet<string> MutualTlsPolicies, Dictionary<string, Uri> MetadataUrls, Dictionary<string, List<PolicyAlternative>> AnyOfPolicies) CollectPolicies(
        OpenApiDocument openApiDoc,
        bool includeDeprecated)
    {
        var policies = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var deprecatedPolicies = new HashSet<string>(StringComparer.Ordinal);
        var mutualTlsPolicies = new HashSet<string>(StringComparer.Ordinal);
        var metadataUrls = CollectMetadataUrls(openApiDoc);
        var anyOfPolicies = new Dictionary<string, List<PolicyAlternative>>(StringComparer.Ordinal);

        if (openApiDoc.Paths is null || openApiDoc.Paths.Count == 0)
        {
            return (policies, deprecatedPolicies, mutualTlsPolicies, metadataUrls, anyOfPolicies);
        }

        foreach (var pathPair in openApiDoc.Paths)
        {
            if (pathPair.Value is not IOpenApiPathItem pathItem || pathItem.Operations is null)
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

                if (operation is null)
                {
                    continue;
                }

                // Extract security requirements for this operation
                var securityRequirements = operation.ExtractSecurityRequirements(openApiDoc);

                if (securityRequirements is null || securityRequirements.Count == 0)
                {
                    continue;
                }

                // Alternatives that differ: one any-of policy for the operation
                var operationPolicy = OperationPolicyName(securityRequirements);
                if (operationPolicy is not null && operationPolicy.Contains('|') && !anyOfPolicies.ContainsKey(operationPolicy))
                {
                    anyOfPolicies[operationPolicy] = securityRequirements
                        .GroupBy(AlternativeName, StringComparer.Ordinal)
                        .OrderBy(g => g.Key, StringComparer.Ordinal)
                        .Select(g => new PolicyAlternative(g.First().AuthenticationScheme, g.First().Scopes.OrderBy(s => s, StringComparer.Ordinal).ToList()))
                        .ToList();
                }

                // Group requirements by scheme to handle AND logic within same scheme
                var schemeScopes = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                foreach (var requirement in securityRequirements)
                {
                    if (requirement.Scopes.Count == 0)
                    {
                        // mutualTLS schemes have no HTTP scopes by design — record with empty list
                        // so the "Create policies" loop can emit a policy constant for them.
                        if (IsMutualTlsScheme(openApiDoc, requirement.SchemeName) &&
                            !schemeScopes.ContainsKey(requirement.SchemeName))
                        {
                            schemeScopes[requirement.SchemeName] = [];
                        }

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
                        // mutualTLS schemes have no HTTP scopes by design — generate a policy using
                        // the scheme name directly so callers can still reference the policy constant.
                        if (IsMutualTlsScheme(openApiDoc, schemeName) && !policies.ContainsKey(schemeName))
                        {
                            policies[schemeName] = [];
                            mutualTlsPolicies.Add(schemeName);
                        }

                        continue;
                    }

                    var isSchemeDeprecated =
                        openApiDoc.Components?.SecuritySchemes is not null &&
                        openApiDoc.Components.SecuritySchemes.TryGetValue(schemeName, out var schemeEntry) &&
                        schemeEntry.Deprecated;

                    // Sort scopes for consistent policy names
                    var sortedScopes = scopes
                        .OrderBy(s => s, StringComparer.Ordinal)
                        .ToList();

                    var policyName = GeneratePolicyName(schemeName, sortedScopes);

                    if (!policies.ContainsKey(policyName))
                    {
                        policies[policyName] = sortedScopes;
                        if (isSchemeDeprecated)
                        {
                            deprecatedPolicies.Add(policyName);
                        }
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
                                if (isSchemeDeprecated)
                                {
                                    deprecatedPolicies.Add(singleScopePolicyName);
                                }
                            }
                        }
                    }
                }
            }
        }

        return (policies, deprecatedPolicies, mutualTlsPolicies, metadataUrls, anyOfPolicies);
    }

    /// <summary>
    /// The name of one alternative: the scheme alone (<c>api_key</c>) or the scheme with its scopes
    /// (<c>oauth2:orders:read</c>, <c>oauth2:admin+orders:write</c>).
    /// </summary>
    private static string AlternativeName(SecurityRequirement requirement)
        => GeneratePolicyName(requirement.SchemeName, requirement.Scopes.OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>
    /// Generates the complete file content.
    /// </summary>
    private static string GenerateFileContent(
        string projectName,
        Dictionary<string, List<string>> policies,
        HashSet<string> deprecatedPolicies,
        HashSet<string> mutualTlsPolicies,
        Dictionary<string, Uri> metadataUrls,
        Dictionary<string, List<PolicyAlternative>> anyOfPolicies)
    {
        // The scope policies, then the any-of policies (an operation whose alternatives differ), as the constants
        var constants = policies
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new PolicyConstant(
                GenerateConstantName(p.Key),
                p.Key,
                mutualTlsPolicies.Contains(p.Key)
                    ? "Requires TLS client certificate — no HTTP credential injected; configure at HttpClient transport level."
                    : p.Value.Count == 1
                        ? $"Policy requiring scope: {p.Value[0]}"
                        : $"Policy requiring scopes: {string.Join(" AND ", p.Value)} (all required)",
                deprecatedPolicies.Contains(p.Key) ? "[Obsolete(\"Security scheme marked deprecated in OpenAPI spec.\")]" : null))
            .Concat(anyOfPolicies
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new PolicyConstant(
                    GenerateConstantName(p.Key),
                    p.Key,
                    "Policy met by any of: " + string.Join("; ", p.Value.Select(a => a.Scopes.Count == 0
                        ? $"{a.AuthenticationScheme} (authenticated)"
                        : $"{a.AuthenticationScheme} with {(a.Scopes.Count == 1 ? "scope" : "scopes")} {string.Join(" AND ", a.Scopes)}")))));

        var builder = new StringBuilder();
        PolicyConstantsClassBuilder.AppendClass(
            builder,
            projectName,
            "Security",
            "SecurityPolicies",
            "Authorization policy name constants generated from OpenAPI security requirements.",
            constants);

        // Emit RFC 8414 metadata URL constants when oauth2MetadataUrl is set on any scheme
        if (metadataUrls.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(4, "/// <summary>");
            builder.AppendLine(4, "/// OAuth 2.0 authorization server metadata URLs (RFC 8414).");
            builder.AppendLine(4, "/// </summary>");
            builder.AppendLine(4, "public static class Metadata");
            builder.AppendLine(4, "{");

            var isFirstUrl = true;

            foreach (var kvp in metadataUrls.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (!isFirstUrl)
                {
                    builder.AppendLine();
                }

                isFirstUrl = false;

                var urlConstantName = kvp.Key.ToPascalCaseForDotNet() + "MetadataUrl";

                builder.AppendLine(8, "/// <summary>");
                builder.AppendLine(8, $"/// RFC 8414 discovery URL for the {kvp.Key} scheme.");
                builder.AppendLine(8, "/// </summary>");
                builder.AppendLine(8, $"public const string {urlConstantName} = \"{kvp.Value.AbsoluteUri}\";");
            }

            builder.AppendLine(4, "}");
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Collects OAuth2MetadataUrl values from all defined security schemes.
    /// </summary>
    private static Dictionary<string, Uri> CollectMetadataUrls(
        OpenApiDocument openApiDoc)
    {
        var result = new Dictionary<string, Uri>(StringComparer.Ordinal);

        if (openApiDoc.Components?.SecuritySchemes is null)
        {
            return result;
        }

        foreach (var kvp in openApiDoc.Components.SecuritySchemes)
        {
            if (kvp.Value is OpenApiSecurityScheme scheme &&
                scheme.OAuth2MetadataUrl is not null)
            {
                result[kvp.Key] = scheme.OAuth2MetadataUrl;
            }
        }

        return result;
    }

    private static bool IsMutualTlsScheme(
        OpenApiDocument openApiDoc,
        string schemeName)
        => openApiDoc.Components?.SecuritySchemes is not null &&
           openApiDoc.Components.SecuritySchemes.TryGetValue(schemeName, out var scheme) &&
           scheme.Type == Microsoft.OpenApi.SecuritySchemeType.MutualTLS;
}