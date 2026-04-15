namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates retryConfig.ts containing retry policy type definitions and
/// extracted policy constants from OpenAPI x-retry-* extensions.
/// </summary>
public static class TypeScriptRetryConfigExtractor
{
    /// <summary>
    /// Generates the retry configuration TypeScript file.
    /// Extracts x-retry-* extensions from the OpenAPI document and produces
    /// typed policy constants and an operation-to-policy mapping.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <param name="headerContent">Optional auto-generated header.</param>
    /// <returns>The generated TypeScript source code.</returns>
    public static string Generate(
        OpenApiDocument document,
        string? headerContent)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        // RetryPolicy interface
        sb.AppendLine("/** Configuration for a retry policy. */");
        sb.AppendLine("export interface RetryPolicy {");
        sb.AppendLine("  /** Maximum number of retry attempts. */");
        sb.AppendLine("  readonly maxAttempts: number;");
        sb.AppendLine("  /** Initial delay between retries in milliseconds. */");
        sb.AppendLine("  readonly delayMs: number;");
        sb.AppendLine("  /** Backoff strategy: how delay increases between retries. */");
        sb.AppendLine("  readonly backoff: 'constant' | 'linear' | 'exponential';");
        sb.AppendLine("  /** Whether to add random jitter to retry delays. */");
        sb.AppendLine("  readonly useJitter: boolean;");
        sb.AppendLine("  /** Per-attempt timeout in milliseconds, or undefined for no timeout. */");
        sb.AppendLine("  readonly timeoutMs?: number;");
        sb.AppendLine("  /** Whether to handle 429 Too Many Requests with Retry-After header. */");
        sb.AppendLine("  readonly handle429: boolean;");
        sb.AppendLine("}");
        sb.AppendLine();

        // Extract named policies from document
        var policies = CollectNamedPolicies(document);

        if (policies.Count > 0)
        {
            sb.AppendLine("/** Named retry policies extracted from the OpenAPI specification. */");
            sb.AppendLine("export const retryPolicies = {");

            foreach (var (name, config) in policies)
            {
                var safeName = ToCamelCase(name);
                sb.Append("  ").Append(safeName).AppendLine(": {");
                sb.Append("    maxAttempts: ").Append(config.MaxAttempts).AppendLine(",");
                sb.Append("    delayMs: ").Append(FormatDouble(config.DelaySeconds * 1000)).AppendLine(",");
                sb.Append("    backoff: '").Append(config.BackoffType.ToString().ToLowerInvariant()).AppendLine("',");
                sb.Append("    useJitter: ").Append(config.UseJitter ? "true" : "false").AppendLine(",");

                if (config.TimeoutSeconds.HasValue)
                {
                    sb.Append("    timeoutMs: ").Append(FormatDouble(config.TimeoutSeconds.Value * 1000)).AppendLine(",");
                }

                sb.Append("    handle429: ").Append(config.Handle429 ? "true" : "false").AppendLine(",");
                sb.AppendLine("  } satisfies RetryPolicy,");
            }

            sb.AppendLine("} as const;");
            sb.AppendLine();

            // Default policy (first one)
            var defaultPolicy = policies[0];
            sb.Append("/** Default retry policy ('").Append(defaultPolicy.Name).AppendLine("'). */");
            sb.Append("export const defaultRetryPolicy: RetryPolicy = retryPolicies.").Append(ToCamelCase(defaultPolicy.Name)).Append(';');
        }
        else
        {
            // Fallback default policy
            sb.AppendLine("/** Default retry policy. */");
            sb.AppendLine("export const defaultRetryPolicy: RetryPolicy = {");
            sb.AppendLine("  maxAttempts: 3,");
            sb.AppendLine("  delayMs: 1000,");
            sb.AppendLine("  backoff: 'exponential',");
            sb.AppendLine("  useJitter: true,");
            sb.AppendLine("  handle429: true,");
            sb.Append("};");
        }

        return sb.ToString();
    }

    private static List<(string Name, RetryConfiguration Config)> CollectNamedPolicies(
        OpenApiDocument document)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var policies = new List<(string Name, RetryConfiguration Config)>();

        // Document-level policy
        var docPolicy = document.Extensions.ExtractRetryPolicy();
        if (!string.IsNullOrEmpty(docPolicy))
        {
            var config = ExtractDocumentLevelConfig(document);
            if (config != null && seen.Add(docPolicy))
            {
                policies.Add((docPolicy, config));
            }
        }

        // Path and operation-level policies
        if (document.Paths != null)
        {
            foreach (var pathPair in document.Paths)
            {
                if (pathPair.Value is not OpenApiPathItem pathItem)
                {
                    continue;
                }

                var pathPolicy = pathItem.Extensions.ExtractRetryPolicy();
                if (!string.IsNullOrEmpty(pathPolicy) && seen.Add(pathPolicy))
                {
                    var config = BuildRetryConfigFromExtensions(pathItem.Extensions, document.Extensions);
                    if (config != null)
                    {
                        policies.Add((pathPolicy, config));
                    }
                }

                if (pathItem.Operations == null)
                {
                    continue;
                }

                foreach (var operationPair in pathItem.Operations)
                {
                    var op = operationPair.Value;
                    if (op == null)
                    {
                        continue;
                    }

                    var opPolicy = op.Extensions.ExtractRetryPolicy();
                    if (!string.IsNullOrEmpty(opPolicy) && seen.Add(opPolicy))
                    {
                        var config = op.ExtractRetryConfiguration(pathItem, document);
                        if (config is { Enabled: true })
                        {
                            policies.Add((opPolicy, config));
                        }
                    }
                }
            }
        }

        return policies;
    }

    private static RetryConfiguration? ExtractDocumentLevelConfig(
        OpenApiDocument document)
    {
        var ext = document.Extensions;
        var policy = ext.ExtractRetryPolicy();
        if (string.IsNullOrEmpty(policy))
        {
            return null;
        }

        return new RetryConfiguration
        {
            Enabled = true,
            Policy = policy,
            MaxAttempts = ext.ExtractMaxRetryAttempts() ?? 3,
            DelaySeconds = ext.ExtractRetryDelaySeconds() ?? 1.0,
            BackoffType = ParseBackoff(ext.ExtractRetryBackoff()),
            UseJitter = ext.ExtractRetryUseJitter() ?? true,
            TimeoutSeconds = ext.ExtractRetryTimeoutSeconds(),
            Handle429 = ext.ExtractRetryHandle429() ?? true,
        };
    }

    private static RetryConfiguration? BuildRetryConfigFromExtensions(
        IDictionary<string, IOpenApiExtension>? pathExt,
        IDictionary<string, IOpenApiExtension>? docExt)
    {
        var policy = pathExt.ExtractRetryPolicy() ?? docExt.ExtractRetryPolicy();
        if (string.IsNullOrEmpty(policy))
        {
            return null;
        }

        return new RetryConfiguration
        {
            Enabled = true,
            Policy = policy,
            MaxAttempts = pathExt.ExtractMaxRetryAttempts() ?? docExt.ExtractMaxRetryAttempts() ?? 3,
            DelaySeconds = pathExt.ExtractRetryDelaySeconds() ?? docExt.ExtractRetryDelaySeconds() ?? 1.0,
            BackoffType = ParseBackoff(pathExt.ExtractRetryBackoff() ?? docExt.ExtractRetryBackoff()),
            UseJitter = pathExt.ExtractRetryUseJitter() ?? docExt.ExtractRetryUseJitter() ?? true,
            TimeoutSeconds = pathExt.ExtractRetryTimeoutSeconds() ?? docExt.ExtractRetryTimeoutSeconds(),
            Handle429 = pathExt.ExtractRetryHandle429() ?? docExt.ExtractRetryHandle429() ?? true,
        };
    }

    private static RetryBackoffType ParseBackoff(string? value)
        => value?.ToLowerInvariant() switch
        {
            "constant" or "fixed" => RetryBackoffType.Constant,
            "linear" => RetryBackoffType.Linear,
            _ => RetryBackoffType.Exponential,
        };

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        // Handle kebab-case: "accounts-fast" -> "accountsFast"
        var parts = name.Split('-');
        var sb = new StringBuilder();
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
            {
                continue;
            }

            if (i == 0)
            {
                sb.Append(char.ToLowerInvariant(parts[i][0]));
            }
            else
            {
                sb.Append(char.ToUpperInvariant(parts[i][0]));
            }

            if (parts[i].Length > 1)
            {
                sb.Append(parts[i].AsSpan(1));
            }
        }

        return sb.ToString();
    }

    private static string FormatDouble(double value)
        => value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
}