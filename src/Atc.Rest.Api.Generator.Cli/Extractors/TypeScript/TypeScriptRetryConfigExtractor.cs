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

        if (headerContent is not null)
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
        sb.AppendLine("  /** Timeout in milliseconds over all attempts and the waits between them, or undefined for none. */");
        sb.AppendLine("  readonly totalTimeoutMs?: number;");
        sb.AppendLine("  /** Whether to handle 429 Too Many Requests with Retry-After header. */");
        sb.AppendLine("  readonly handle429: boolean;");
        sb.AppendLine("}");
        sb.AppendLine();

        // Per-request retry, passed by the generated client methods for operations that differ
        // from the ApiClient default (x-retry-policy / x-retry-enabled on the operation or path).
        sb.AppendLine("/**");
        sb.AppendLine(" * Retry for one request. Omitted, the ApiClient uses its policy for safe methods and does not");
        sb.AppendLine(" * retry POST/PATCH (not idempotent: a resend can apply them twice).");
        sb.AppendLine(" */");
        sb.AppendLine("export interface RequestRetry {");
        sb.AppendLine("  /** The operation's own policy; defaults to the ApiClient's. */");
        sb.AppendLine("  readonly policy?: RetryPolicy;");
        sb.AppendLine("  /** false: one attempt only (the policy's timeout still applies). true: retry even a POST/PATCH. */");
        sb.AppendLine("  readonly enabled?: boolean;");
        sb.AppendLine("  /** Per-attempt timeout override in milliseconds. */");
        sb.AppendLine("  readonly timeoutMs?: number;");
        sb.AppendLine("}");
        sb.AppendLine();

        // Extract named policies from document
        var policies = document.CollectNamedRetryPolicies();

        if (policies.Count > 0)
        {
            sb.AppendLine("/** Named retry policies extracted from the OpenAPI specification. */");
            sb.AppendLine("export const retryPolicies = {");

            foreach (var config in policies)
            {
                var safeName = ToCamelCase(config.Policy!);
                sb.Append("  ").Append(safeName).AppendLine(": {");
                sb.Append("    maxAttempts: ").Append(config.MaxAttempts).AppendLine(",");
                sb.Append("    delayMs: ").Append(FormatDouble(config.DelaySeconds * 1000)).AppendLine(",");
                sb.Append("    backoff: '").Append(config.BackoffType.ToString().ToLowerInvariant()).AppendLine("',");
                sb.Append("    useJitter: ").Append(config.UseJitter ? "true" : "false").AppendLine(",");

                if (config.TimeoutSeconds.HasValue)
                {
                    sb.Append("    timeoutMs: ").Append(FormatDouble(config.TimeoutSeconds.Value * 1000)).AppendLine(",");
                }

                if (config.TotalTimeoutSeconds.HasValue)
                {
                    sb.Append("    totalTimeoutMs: ").Append(FormatDouble(config.TotalTimeoutSeconds.Value * 1000)).AppendLine(",");
                }

                sb.Append("    handle429: ").Append(config.Handle429 ? "true" : "false").AppendLine(",");
                sb.AppendLine("  } satisfies RetryPolicy,");
            }

            sb.AppendLine("} as const;");
            sb.AppendLine();

            // Default policy (first one)
            var defaultPolicy = policies[0];
            sb.Append("/** Default retry policy ('").Append(defaultPolicy.Policy).AppendLine("'). */");
            sb.Append("export const defaultRetryPolicy: RetryPolicy = retryPolicies.").Append(ToCamelCase(defaultPolicy.Policy!)).Append(';');
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

    /// <summary>
    /// The property name of a policy in the generated <c>retryPolicies</c> object ("accounts-fast" → "accountsFast").
    /// </summary>
    /// <param name="name">The policy name from <c>x-retry-policy</c>.</param>
    /// <returns>The camelCase property name.</returns>
    internal static string ToCamelCase(string name)
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