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
            => extensions.ReadString(RateLimitExtensionNameConstants.Policy);

        /// <summary>
        /// Extracts the x-ratelimit-enabled value from extensions.
        /// </summary>
        /// <returns>True if enabled, false if explicitly disabled, null if not specified.</returns>
        public bool? ExtractRateLimitEnabled()
            => extensions.ReadBool(RateLimitExtensionNameConstants.Enabled);

        /// <summary>
        /// Extracts the x-ratelimit-permit-limit value from extensions.
        /// </summary>
        /// <returns>The permit limit, or null if not specified.</returns>
        public int? ExtractPermitLimit()
            => extensions.ReadInt(RateLimitExtensionNameConstants.PermitLimit);

        /// <summary>
        /// Extracts the x-ratelimit-window-seconds value from extensions.
        /// </summary>
        /// <returns>The window in seconds, or null if not specified.</returns>
        public int? ExtractWindowSeconds()
            => extensions.ReadInt(RateLimitExtensionNameConstants.WindowSeconds);

        /// <summary>
        /// Extracts the x-ratelimit-queue-limit value from extensions.
        /// </summary>
        /// <returns>The queue limit, or null if not specified.</returns>
        public int? ExtractQueueLimit()
            => extensions.ReadInt(RateLimitExtensionNameConstants.QueueLimit);

        /// <summary>
        /// Extracts the x-ratelimit-algorithm value from extensions.
        /// </summary>
        /// <returns>The algorithm name, or null if not specified.</returns>
        public string? ExtractRateLimitAlgorithm()
            => extensions.ReadString(RateLimitExtensionNameConstants.Algorithm);

        /// <summary>
        /// Extracts the x-ratelimit-emit-retry-after value from extensions.
        /// </summary>
        /// <returns>True if the Retry-After header should be emitted, false if explicitly disabled, null if not specified.</returns>
        public bool? ExtractRateLimitEmitRetryAfter()
            => extensions.ReadBool(RateLimitExtensionNameConstants.EmitRetryAfter);

        /// <summary>
        /// Extracts the x-ratelimit-partition value from extensions.
        /// </summary>
        /// <returns>The partition strategy name, or null if not specified.</returns>
        public string? ExtractRateLimitPartition()
            => extensions.ReadString(RateLimitExtensionNameConstants.Partition);

        /// <summary>
        /// Extracts the x-ratelimit-partition-claim value from extensions.
        /// </summary>
        /// <returns>The claim type, or null if not specified.</returns>
        public string? ExtractRateLimitPartitionClaim()
            => extensions.ReadString(RateLimitExtensionNameConstants.PartitionClaim);
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

        return ResolveRateLimit(new OpenApiExtensionScope(operation.Extensions, pathItem.Extensions, document.Extensions));
    }

    /// <summary>
    /// The rate limit configuration the <c>x-ratelimit-*</c> settings give, each read from the nearest level that sets
    /// it.
    /// </summary>
    /// <param name="inherited">The operation's, path item's and document's extensions.</param>
    /// <returns>The configuration, or <see langword="null"/> when no level names a rate limit policy.</returns>
    public static RateLimitConfiguration? ResolveRateLimit(OpenApiExtensionScope inherited)
    {
        if (inherited is null)
        {
            throw new ArgumentNullException(nameof(inherited));
        }

        // Get effective policy (operation → path → document)
        var policy = inherited.First(e => e.ExtractRateLimitPolicy());

        if (string.IsNullOrEmpty(policy))
        {
            return null; // No rate limiting configured
        }

        // Get configuration values (operation → path → document)
        var permitLimit = inherited.First(e => e.ExtractPermitLimit())
                          ?? 100;

        var windowSeconds = inherited.First(e => e.ExtractWindowSeconds())
                            ?? 60;

        var queueLimit = inherited.First(e => e.ExtractQueueLimit())
                         ?? 0;

        var algorithmString = inherited.First(e => e.ExtractRateLimitAlgorithm())
                              ?? "fixed";

        var algorithm = ParseAlgorithm(algorithmString);

        var emitRetryAfter = inherited.First(e => e.ExtractRateLimitEmitRetryAfter())
                             ?? true;

        var partitionString = inherited.First(e => e.ExtractRateLimitPartition());

        var partition = ParsePartitionStrategy(partitionString);

        var partitionClaim = inherited.First(e => e.ExtractRateLimitPartitionClaim());

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
        if (document.Paths is null)
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
            if (pathItem.Operations is null)
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