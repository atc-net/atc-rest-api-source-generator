// ReSharper disable CommentTypo
namespace Atc.OpenApi.Extensions;

/// <summary>
/// Extension methods for extracting rate limit configuration from OpenAPI extensions.
/// Supports ATC-specific extensions (x-ratelimit-*) for configuring ASP.NET Core rate limiting.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S3398:Move this method inside", Justification = "OK - CLang14 - extension")]
public static class OpenApiRateLimitExtensions
{
    /// <param name="extensions">The OpenAPI extensions dictionary.</param>
    extension(IDictionary<string, IOpenApiExtension>? extensions)
    {
        /// <summary>
        /// Extracts the x-ratelimit-policy value from extensions.
        /// </summary>
        /// <returns>The policy name, or null if not specified.</returns>
        public string? ExtractRateLimitPolicy()
            => ExtractStringValue(extensions, RateLimitExtensionNameConstants.Policy);

        /// <summary>
        /// Extracts the x-ratelimit-enabled value from extensions.
        /// </summary>
        /// <returns>True if enabled, false if explicitly disabled, null if not specified.</returns>
        public bool? ExtractRateLimitEnabled()
            => ExtractBoolValue(extensions, RateLimitExtensionNameConstants.Enabled);

        /// <summary>
        /// Extracts the x-ratelimit-permit-limit value from extensions.
        /// </summary>
        /// <returns>The permit limit, or null if not specified.</returns>
        public int? ExtractPermitLimit()
            => ExtractIntValue(extensions, RateLimitExtensionNameConstants.PermitLimit);

        /// <summary>
        /// Extracts the x-ratelimit-window-seconds value from extensions.
        /// </summary>
        /// <returns>The window in seconds, or null if not specified.</returns>
        public int? ExtractWindowSeconds()
            => ExtractIntValue(extensions, RateLimitExtensionNameConstants.WindowSeconds);

        /// <summary>
        /// Extracts the x-ratelimit-queue-limit value from extensions.
        /// </summary>
        /// <returns>The queue limit, or null if not specified.</returns>
        public int? ExtractQueueLimit()
            => ExtractIntValue(extensions, RateLimitExtensionNameConstants.QueueLimit);

        /// <summary>
        /// Extracts the x-ratelimit-algorithm value from extensions.
        /// </summary>
        /// <returns>The algorithm name, or null if not specified.</returns>
        public string? ExtractRateLimitAlgorithm()
            => ExtractStringValue(extensions, RateLimitExtensionNameConstants.Algorithm);

        /// <summary>
        /// Extracts the x-ratelimit-emit-retry-after value from extensions.
        /// </summary>
        /// <returns>True if the Retry-After header should be emitted, false if explicitly disabled, null if not specified.</returns>
        public bool? ExtractRateLimitEmitRetryAfter()
            => ExtractBoolValue(extensions, RateLimitExtensionNameConstants.EmitRetryAfter);

        /// <summary>
        /// Extracts the x-ratelimit-partition value from extensions.
        /// </summary>
        /// <returns>The partition strategy name, or null if not specified.</returns>
        public string? ExtractRateLimitPartition()
            => ExtractStringValue(extensions, RateLimitExtensionNameConstants.Partition);

        /// <summary>
        /// Extracts the x-ratelimit-partition-claim value from extensions.
        /// </summary>
        /// <returns>The claim type, or null if not specified.</returns>
        public string? ExtractRateLimitPartitionClaim()
            => ExtractStringValue(extensions, RateLimitExtensionNameConstants.PartitionClaim);
    }

    /// <summary>
    /// Extracts the complete rate limit configuration for an operation.
    /// Applies hierarchical inheritance: operation overrides path, path overrides document.
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="pathItem">The path item containing the operation.</param>
    /// <param name="document">The OpenAPI document (for root-level settings).</param>
    /// <returns>A RateLimitConfiguration, or null if no rate limiting is configured.</returns>
    public static RateLimitConfiguration? ExtractRateLimitConfiguration(
        this OpenApiOperation operation,
        IOpenApiPathItem pathItem,
        OpenApiDocument document)
    {
        // Check for operation-level explicit disable
        var operationEnabled = operation.Extensions.ExtractRateLimitEnabled();
        if (operationEnabled == false)
        {
            return new RateLimitConfiguration { Enabled = false };
        }

        // Get effective policy (operation → path → document)
        var policy = operation.Extensions.ExtractRateLimitPolicy()
                     ?? pathItem.Extensions.ExtractRateLimitPolicy()
                     ?? document.Extensions.ExtractRateLimitPolicy();

        if (string.IsNullOrEmpty(policy))
        {
            return null; // No rate limiting configured
        }

        // Get configuration values (operation → path → document)
        var permitLimit = operation.Extensions.ExtractPermitLimit()
                          ?? pathItem.Extensions.ExtractPermitLimit()
                          ?? document.Extensions.ExtractPermitLimit()
                          ?? 100;

        var windowSeconds = operation.Extensions.ExtractWindowSeconds()
                            ?? pathItem.Extensions.ExtractWindowSeconds()
                            ?? document.Extensions.ExtractWindowSeconds()
                            ?? 60;

        var queueLimit = operation.Extensions.ExtractQueueLimit()
                         ?? pathItem.Extensions.ExtractQueueLimit()
                         ?? document.Extensions.ExtractQueueLimit()
                         ?? 0;

        var algorithmString = operation.Extensions.ExtractRateLimitAlgorithm()
                              ?? pathItem.Extensions.ExtractRateLimitAlgorithm()
                              ?? document.Extensions.ExtractRateLimitAlgorithm()
                              ?? "fixed";

        var algorithm = ParseAlgorithm(algorithmString);

        var emitRetryAfter = operation.Extensions.ExtractRateLimitEmitRetryAfter()
                             ?? pathItem.Extensions.ExtractRateLimitEmitRetryAfter()
                             ?? document.Extensions.ExtractRateLimitEmitRetryAfter()
                             ?? true;

        var partitionString = operation.Extensions.ExtractRateLimitPartition()
                              ?? pathItem.Extensions.ExtractRateLimitPartition()
                              ?? document.Extensions.ExtractRateLimitPartition();

        var partition = ParsePartitionStrategy(partitionString);

        var partitionClaim = operation.Extensions.ExtractRateLimitPartitionClaim()
                             ?? pathItem.Extensions.ExtractRateLimitPartitionClaim()
                             ?? document.Extensions.ExtractRateLimitPartitionClaim();

        return new RateLimitConfiguration
        {
            Enabled = true,
            Policy = policy,
            PermitLimit = permitLimit,
            WindowSeconds = windowSeconds,
            QueueLimit = queueLimit,
            Algorithm = algorithm,
            EmitRetryAfter = emitRetryAfter,
            Partition = partition,
            PartitionClaim = partitionClaim,
        };
    }

    /// <summary>
    /// Checks if the document has any rate limiting configuration.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <returns>True if rate limiting is configured at any level.</returns>
    public static bool HasRateLimiting(this OpenApiDocument document)
    {
        // Check document-level
        if (!string.IsNullOrEmpty(document.Extensions.ExtractRateLimitPolicy()))
        {
            return true;
        }

        // Check path and operation levels
        if (document.Paths == null)
        {
            return false;
        }

        foreach (var pathPair in document.Paths)
        {
            if (pathPair.Value is not IOpenApiPathItem pathItem)
            {
                continue;
            }

            // Check path-level
            if (!string.IsNullOrEmpty(pathItem.Extensions.ExtractRateLimitPolicy()))
            {
                return true;
            }

            // Check operation-level
            if (pathItem.Operations == null)
            {
                continue;
            }

            foreach (var operationPair in pathItem.Operations)
            {
                if (!string.IsNullOrEmpty(operationPair.Value?.Extensions.ExtractRateLimitPolicy()))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts a string value from an OpenAPI extension.
    /// </summary>
    private static string? ExtractStringValue(
        IDictionary<string, IOpenApiExtension>? extensions,
        string key)
    {
        if (extensions is null ||
            !extensions.TryGetValue(key, out var extension) ||
            extension is null)
        {
            return null;
        }

        // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
        {
            return null;
        }

        var node = nodeProperty.GetValue(extension);
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        return null;
    }

    /// <summary>
    /// Extracts a boolean value from an OpenAPI extension.
    /// </summary>
    private static bool? ExtractBoolValue(
        IDictionary<string, IOpenApiExtension>? extensions,
        string key)
    {
        if (extensions is null ||
            !extensions.TryGetValue(key, out var extension) ||
            extension is null)
        {
            return null;
        }

        // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
        {
            return null;
        }

        var node = nodeProperty.GetValue(extension);
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var boolValue))
        {
            return boolValue;
        }

        return null;
    }

    /// <summary>
    /// Extracts an integer value from an OpenAPI extension.
    /// </summary>
    private static int? ExtractIntValue(
        IDictionary<string, IOpenApiExtension>? extensions,
        string key)
    {
        if (extensions is null ||
            !extensions.TryGetValue(key, out var extension) ||
            extension is null)
        {
            return null;
        }

        // Use reflection to access Node property (Microsoft.OpenApi v3.0.1 pattern)
        var extensionType = extension.GetType();
        var nodeProperty = extensionType.GetProperty("Node");
        if (nodeProperty == null)
        {
            return null;
        }

        var node = nodeProperty.GetValue(extension);
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            // Also try long and clamp to int range
            if (jsonValue.TryGetValue<long>(out var longValue))
            {
                return longValue > int.MaxValue ? int.MaxValue : longValue < int.MinValue ? int.MinValue : (int)longValue;
            }

            // YAML-sourced integer scalars are boxed as decimal (JsonValuePrimitive<decimal>)
            // by the OpenAPI YAML reader, not int/long, so neither branch above ever matches
            // for a document parsed from YAML (the common case for this generator).
            if (jsonValue.TryGetValue<decimal>(out var decimalValue))
            {
                return decimalValue > int.MaxValue ? int.MaxValue : decimalValue < int.MinValue ? int.MinValue : (int)decimalValue;
            }
        }

        return null;
    }

    /// <summary>
    /// Parses a rate limit algorithm string to the enum value.
    /// Unknown or empty values fall back to <see cref="RateLimitAlgorithm.Fixed"/>.
    /// </summary>
    public static RateLimitAlgorithm ParseAlgorithm(string? algorithmString)
    {
        if (string.IsNullOrEmpty(algorithmString))
        {
            return RateLimitAlgorithm.Fixed;
        }

        return algorithmString.ToLowerInvariant() switch
        {
            "sliding" or "sliding-window" => RateLimitAlgorithm.Sliding,
            "token-bucket" or "tokenbucket" => RateLimitAlgorithm.TokenBucket,
            "concurrency" => RateLimitAlgorithm.Concurrency,
            _ => RateLimitAlgorithm.Fixed,
        };
    }

    /// <summary>
    /// Parses a rate limit partition strategy string to the enum value.
    /// Unknown or empty values fall back to <see cref="RateLimitPartitionStrategy.Global"/>.
    /// </summary>
    public static RateLimitPartitionStrategy ParsePartitionStrategy(string? partitionString)
    {
        if (string.IsNullOrEmpty(partitionString))
        {
            return RateLimitPartitionStrategy.Global;
        }

        return partitionString.ToLowerInvariant() switch
        {
            "ip" => RateLimitPartitionStrategy.Ip,
            "user" => RateLimitPartitionStrategy.User,
            _ => RateLimitPartitionStrategy.Global,
        };
    }
}