// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CommentTypo
// ReSharper disable InvertIf
// ReSharper disable LoopCanBeConvertedToQuery
namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// The rate-limit rules (ATC_API_RL): policies, their placement, their values and their partitioning.
/// </summary>
public static partial class OpenApiDocumentValidator
{
    /// <summary>
    /// Emits ATC_API_RL004 (Error) when two or more distinct policy names sanitize to the same C#
    /// identifier. <c>RateLimitPoliciesExtractor</c> emits one constant per policy name with no
    /// de-duplication, so a collision produces duplicate members and the generated code does not compile.
    /// </summary>
    private static void ValidateRateLimitPolicyNameCollisions(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        var namesByIdentifier = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        void Collect(string? policyName)
        {
            if (string.IsNullOrEmpty(policyName))
            {
                return;
            }

            var identifier = PolicyNamingHelper.ToConstantName(policyName!);
            if (string.IsNullOrEmpty(identifier))
            {
                return;
            }

            if (!namesByIdentifier.TryGetValue(identifier, out var names))
            {
                names = new SortedSet<string>(StringComparer.Ordinal);
                namesByIdentifier[identifier] = names;
            }

            names.Add(policyName!);
        }

        Collect(document.Extensions.ExtractRateLimitPolicy());

        if (document.Paths is not null)
        {
            foreach (var pathEntry in document.Paths)
            {
                var pathItem = pathEntry.Value;
                Collect(pathItem.Extensions.ExtractRateLimitPolicy());

                if (pathItem.Operations is null)
                {
                    continue;
                }

                foreach (var operationEntry in pathItem.Operations)
                {
                    Collect(operationEntry.Value?.Extensions.ExtractRateLimitPolicy());
                }
            }
        }

        foreach (var group in namesByIdentifier.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (group.Value.Count <= 1)
            {
                continue;
            }

            var collidingNames = string.Join("', '", group.Value);
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RateLimitPolicyNameCollision,
                $"Rate limit policy names '{collidingNames}' all sanitize to the C# identifier '{group.Key}', " +
                "which emits duplicate constants in the generated RateLimitPolicies class and breaks compilation. " +
                "Rename all but one to avoid the collision.",
                DiagnosticSeverity.Error,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Emits ATC_API_RL005 (Warning) for values the limiter constructor rejects, ATC_API_RL006 (Warning)
    /// when <c>x-ratelimit-enabled</c> is placed where it is not read, and ATC_API_RL008 (Info) when a
    /// window is declared on a concurrency policy that has no time component.
    /// </summary>
    private static void ValidateRateLimitValuesAndPlacement(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        var documentPolicy = document.Extensions.ExtractRateLimitPolicy();
        var documentAlgorithm = document.Extensions.ExtractRateLimitAlgorithm();

        ValidateRateLimitSite(diagnostics, sourceFilePath, document.Extensions, "document", documentAlgorithm);
        ValidateRateLimitEnabledPlacement(diagnostics, sourceFilePath, document.Extensions, "document", documentPolicy);

        if (document.Paths is null)
        {
            return;
        }

        foreach (var pathEntry in document.Paths)
        {
            var pathItem = pathEntry.Value;
            var pathLocation = $"path '{pathEntry.Key}'";
            var pathPolicy = pathItem.Extensions.ExtractRateLimitPolicy() ?? documentPolicy;
            var pathAlgorithm = pathItem.Extensions.ExtractRateLimitAlgorithm() ?? documentAlgorithm;

            ValidateRateLimitSite(diagnostics, sourceFilePath, pathItem.Extensions, pathLocation, pathAlgorithm);
            ValidateRateLimitEnabledPlacement(diagnostics, sourceFilePath, pathItem.Extensions, pathLocation, pathPolicy);

            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operationEntry in pathItem.Operations)
            {
                var operation = operationEntry.Value;
                if (operation is null)
                {
                    continue;
                }

                var location = $"operation '{operation.OperationId ?? operationEntry.Key.ToString()}'";
                var operationAlgorithm = operation.Extensions.ExtractRateLimitAlgorithm() ?? pathAlgorithm;

                ValidateRateLimitSite(diagnostics, sourceFilePath, operation.Extensions, location, operationAlgorithm);
            }
        }
    }

    /// <summary>
    /// Validates the numeric rate limit values declared at a single site, and reports a window declared
    /// on a concurrency policy.
    /// </summary>
    private static void ValidateRateLimitSite(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        IDictionary<string, IOpenApiExtension>? extensions,
        string location,
        string? effectiveAlgorithm)
    {
        if (extensions is null)
        {
            return;
        }

        var isConcurrency = OpenApiRateLimitExtensions.ParseAlgorithm(effectiveAlgorithm) == RateLimitAlgorithm.Concurrency;

        var permitLimit = extensions.ExtractPermitLimit();
        if (permitLimit is <= 0)
        {
            var hint = permitLimit == 0
                ? $" To switch rate limiting off, use '{RateLimitExtensionNameConstants.Enabled}: false' on the operation instead."
                : string.Empty;

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RateLimitValueOutOfRange,
                $"'{RateLimitExtensionNameConstants.PermitLimit}' is {permitLimit} on {location}, but the limiter requires " +
                $"a value greater than 0. The limiter constructor throws during AddApiRateLimiting, so the application " +
                $"fails to start.{hint}",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        var queueLimit = extensions.ExtractQueueLimit();
        if (queueLimit is < 0)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RateLimitValueOutOfRange,
                $"'{RateLimitExtensionNameConstants.QueueLimit}' is {queueLimit} on {location}, but the limiter requires " +
                "a value greater than or equal to 0. The limiter constructor throws during AddApiRateLimiting, so the " +
                "application fails to start.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }

        var windowSeconds = extensions.ExtractWindowSeconds();
        if (windowSeconds is null)
        {
            return;
        }

        if (isConcurrency)
        {
            // ConcurrencyLimiterOptions has no time component, so the value is dropped entirely -
            // it cannot be out of range because it is never used.
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RateLimitWindowIgnoredForConcurrency,
                $"'{RateLimitExtensionNameConstants.WindowSeconds}' is declared on {location} but the effective algorithm " +
                "is 'concurrency', which limits simultaneous requests and has no time window. The value is ignored - " +
                "remove it, or switch to 'fixed', 'sliding' or 'token-bucket' if a time window is intended.",
                DiagnosticSeverity.Info,
                sourceFilePath));
            return;
        }

        if (windowSeconds <= 0)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RateLimitValueOutOfRange,
                $"'{RateLimitExtensionNameConstants.WindowSeconds}' is {windowSeconds} on {location}, but the limiter " +
                "requires a value greater than 0. The limiter constructor throws during AddApiRateLimiting, so the " +
                "application fails to start.",
                DiagnosticSeverity.Warning,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Emits ATC_API_RL006 when <c>x-ratelimit-enabled: false</c> is declared somewhere the generator
    /// never reads it. Only an operation-level declaration produces <c>.DisableRateLimiting()</c>.
    /// </summary>
    private static void ValidateRateLimitEnabledPlacement(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        IDictionary<string, IOpenApiExtension>? extensions,
        string location,
        string? effectivePolicy)
    {
        if (extensions.ExtractRateLimitEnabled() != false || string.IsNullOrEmpty(effectivePolicy))
        {
            return;
        }

        diagnostics.Add(new DiagnosticMessage(
            RuleIdentifiers.RateLimitEnabledIgnoredOutsideOperation,
            $"'{RateLimitExtensionNameConstants.Enabled}: false' is declared on {location}, but the generator only " +
            $"honours it at operation level, so the endpoints covered by policy '{effectivePolicy}' remain rate limited. " +
            $"Declare '{RateLimitExtensionNameConstants.Enabled}: false' on each operation that should be exempt.",
            DiagnosticSeverity.Warning,
            sourceFilePath));
    }

    /// <summary>
    /// Emits ATC_API_RL007 (Info) once per policy whose algorithm cannot supply a Retry-After value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Verified against the runtime: a rejected <c>SlidingWindowRateLimiter</c> lease advertises the
    /// <c>RETRY_AFTER</c> metadata name but <c>TryGetMetadata</c> returns <c>false</c>, and
    /// <c>ConcurrencyLimiter</c> never lists it. The generated <c>OnRejected</c> is still emitted; its
    /// <c>if</c> simply never succeeds, so a 429 goes out with no header.
    /// </para>
    /// <para>
    /// This walks <c>CollectPolicies</c> rather than resolving per operation, because that is the exact
    /// first-wins set the generator turns into limiter registrations. Resolving per operation would
    /// describe an algorithm the generator never emits whenever a policy name is declared at several
    /// sites with differing algorithms (which ATC_API_RL003 reports separately).
    /// </para>
    /// </remarks>
    private static void ValidateRateLimitRetryAfterSupport(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        var policies = RateLimitPoliciesExtractor.CollectPolicies(document, includeDeprecated: false);

        foreach (var policyEntry in policies.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var config = policyEntry.Value;
            if (!config.Enabled || !config.EmitRetryAfter)
            {
                continue;
            }

            var algorithmName = config.Algorithm switch
            {
                RateLimitAlgorithm.Sliding => "sliding",
                RateLimitAlgorithm.Concurrency => "concurrency",
                _ => null,
            };

            if (algorithmName is null)
            {
                continue;
            }

            var reason = config.Algorithm == RateLimitAlgorithm.Concurrency
                ? "a concurrency limiter has no time component, so there is nothing to wait for"
                : "the sliding window limiter advertises the Retry-After metadata name but never attaches a value";

            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.RateLimitRetryAfterUnsupportedByAlgorithm,
                $"Policy '{policyEntry.Key}' uses the '{algorithmName}' algorithm, so no Retry-After header is sent on " +
                $"429 responses even though '{RateLimitExtensionNameConstants.EmitRetryAfter}' is enabled - {reason}. " +
                "Use 'fixed' or 'token-bucket' if clients depend on Retry-After, or supply a fallback through the " +
                "AddApiRateLimiting configure callback.",
                DiagnosticSeverity.Info,
                sourceFilePath));
        }
    }

    /// <summary>
    /// Emits ATC_API_RL003 (Warning) when a single rate limit policy name is declared with conflicting
    /// settings at more than one site.
    /// </summary>
    /// <remarks>
    /// A policy name is the unit of limiter registration in <c>RateLimiterOptions</c>, so one name maps
    /// to exactly one limiter. <c>RateLimitPoliciesExtractor.CollectPolicies</c> is first-wins, so the
    /// losing declarations are silently discarded. Only settings that are <b>explicitly declared</b> at
    /// a site are compared - re-declaring a policy name on a sub-path without repeating every setting is
    /// idiomatic and contradicts nothing, so it must not warn.
    /// </remarks>
    private static void ValidateRateLimitPolicyConflicts(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        var sitesByPolicy = new Dictionary<string, List<(string Location, Dictionary<string, string> Declared)>>(StringComparer.Ordinal);

        var documentPolicy = document.Extensions.ExtractRateLimitPolicy();
        AddRateLimitDeclarationSite(sitesByPolicy, documentPolicy, "document", document.Extensions);

        if (document.Paths is not null)
        {
            foreach (var pathEntry in document.Paths)
            {
                var pathItem = pathEntry.Value;
                var pathPolicy = pathItem.Extensions.ExtractRateLimitPolicy() ?? documentPolicy;

                AddRateLimitDeclarationSite(sitesByPolicy, pathPolicy, $"path '{pathEntry.Key}'", pathItem.Extensions);

                if (pathItem.Operations is null)
                {
                    continue;
                }

                foreach (var operationEntry in pathItem.Operations)
                {
                    var operation = operationEntry.Value;
                    if (operation is null || operation.Extensions.ExtractRateLimitEnabled() == false)
                    {
                        continue;
                    }

                    var operationPolicy = operation.Extensions.ExtractRateLimitPolicy() ?? pathPolicy;
                    var location = $"operation '{operation.OperationId ?? operationEntry.Key.ToString()}'";

                    AddRateLimitDeclarationSite(sitesByPolicy, operationPolicy, location, operation.Extensions);
                }
            }
        }

        foreach (var policyEntry in sitesByPolicy.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (policyEntry.Value.Count < 2)
            {
                continue;
            }

            var conflictingKeys = policyEntry.Value
                .SelectMany(site => site.Declared.Select(setting => setting.Key))
                .Distinct(StringComparer.Ordinal)
                .Where(key => policyEntry.Value
                    .Where(site => site.Declared.ContainsKey(key))
                    .Select(site => site.Declared[key])
                    .Distinct(StringComparer.Ordinal)
                    .Count() > 1)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            foreach (var key in conflictingKeys)
            {
                var declarations = policyEntry.Value
                    .Where(site => site.Declared.ContainsKey(key))
                    .Select(site => $"'{site.Declared[key]}' at {site.Location}")
                    .ToList();

                diagnostics.Add(new DiagnosticMessage(
                    RuleId: RuleIdentifiers.RateLimitPolicyConflictingSettings,
                    Message: $"Rate limit policy '{policyEntry.Key}' is declared with conflicting '{key}' values: " +
                             $"{string.Join(", ", declarations)}. A policy name maps to a single limiter " +
                             "registration, so only the first declaration is used and the rest are ignored. " +
                             "Use identical values at every site, or split into separate policy names if the " +
                             "endpoints genuinely need different settings.",
                    Severity: DiagnosticSeverity.Warning,
                    FilePath: sourceFilePath));
            }
        }
    }

    /// <summary>
    /// Records the rate limit settings explicitly declared at a single site, keyed by effective policy name.
    /// Sites that declare no settings contribute nothing and cannot conflict.
    /// </summary>
    private static void AddRateLimitDeclarationSite(
        Dictionary<string, List<(string Location, Dictionary<string, string> Declared)>> sitesByPolicy,
        string? policyName,
        string location,
        IDictionary<string, IOpenApiExtension>? extensions)
    {
        if (string.IsNullOrEmpty(policyName))
        {
            return;
        }

        var declared = GetDeclaredRateLimitSettings(extensions);
        if (declared.Count == 0)
        {
            return;
        }

        if (!sitesByPolicy.TryGetValue(policyName!, out var sites))
        {
            sites = [];
            sitesByPolicy[policyName!] = sites;
        }

        sites.Add((location, declared));
    }

    /// <summary>
    /// Extracts the rate limit settings explicitly declared in the given extensions, normalized for
    /// comparison. Values parsed case-insensitively are lower-cased so that, for example,
    /// <c>user</c> and <c>User</c> do not read as a conflict.
    /// </summary>
    private static Dictionary<string, string> GetDeclaredRateLimitSettings(
        IDictionary<string, IOpenApiExtension>? extensions)
    {
        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        if (extensions is null)
        {
            return declared;
        }

        var permitLimit = extensions.ExtractPermitLimit();
        if (permitLimit.HasValue)
        {
            declared[RateLimitExtensionNameConstants.PermitLimit] = permitLimit.Value.ToString(NumberFormatInfo.InvariantInfo);
        }

        var windowSeconds = extensions.ExtractWindowSeconds();
        if (windowSeconds.HasValue)
        {
            declared[RateLimitExtensionNameConstants.WindowSeconds] = windowSeconds.Value.ToString(NumberFormatInfo.InvariantInfo);
        }

        var queueLimit = extensions.ExtractQueueLimit();
        if (queueLimit.HasValue)
        {
            declared[RateLimitExtensionNameConstants.QueueLimit] = queueLimit.Value.ToString(NumberFormatInfo.InvariantInfo);
        }

        var algorithm = extensions.ExtractRateLimitAlgorithm();
        if (!string.IsNullOrEmpty(algorithm))
        {
            declared[RateLimitExtensionNameConstants.Algorithm] = algorithm!.ToLowerInvariant();
        }

        var partition = extensions.ExtractRateLimitPartition();
        if (!string.IsNullOrEmpty(partition))
        {
            declared[RateLimitExtensionNameConstants.Partition] = partition!.ToLowerInvariant();
        }

        // Claim names are case-sensitive, so this one is compared verbatim.
        var partitionClaim = extensions.ExtractRateLimitPartitionClaim();
        if (!string.IsNullOrEmpty(partitionClaim))
        {
            declared[RateLimitExtensionNameConstants.PartitionClaim] = partitionClaim!;
        }

        var emitRetryAfter = extensions.ExtractRateLimitEmitRetryAfter();
        if (emitRetryAfter.HasValue)
        {
            declared[RateLimitExtensionNameConstants.EmitRetryAfter] = emitRetryAfter.Value
                ? "true"
                : "false";
        }

        return declared;
    }

    /// <summary>
    /// Emits ATC_API_RL001 (Warning) for every <c>x-ratelimit-partition</c> declaration whose value is
    /// not <c>global</c>, <c>ip</c> or <c>user</c>, and ATC_API_RL002 (Warning) for every
    /// <c>x-ratelimit-partition-claim</c> declaration that no operation in its scope can actually use.
    /// </summary>
    /// <remarks>
    /// Both fall back silently today: an unrecognized partition value degrades to <c>global</c> - one
    /// shared bucket for every caller, which is the exact failure mode partitioning exists to prevent -
    /// and a claim is only read when the effective partition is <c>user</c>.
    /// </remarks>
    private static void ValidateRateLimitPartitioning(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        OpenApiDocument document)
    {
        ValidatePartitionValue(diagnostics, sourceFilePath, document.Extensions, "document");

        var documentPartition = document.Extensions.ExtractRateLimitPartition();
        var documentClaim = document.Extensions.ExtractRateLimitPartitionClaim();

        // Collects whether any operation anywhere resolves to user partitioning, which decides
        // whether a document-level claim is actually reachable.
        var anyOperationUsesUserPartition = false;

        if (document.Paths is not null)
        {
            foreach (var pathEntry in document.Paths)
            {
                var pathItem = pathEntry.Value;

                ValidatePartitionValue(diagnostics, sourceFilePath, pathItem.Extensions, $"path '{pathEntry.Key}'");

                var pathPartition = pathItem.Extensions.ExtractRateLimitPartition() ?? documentPartition;
                var pathClaim = pathItem.Extensions.ExtractRateLimitPartitionClaim();

                var anyOperationInPathUsesUserPartition = false;

                if (pathItem.Operations is not null)
                {
                    foreach (var operationEntry in pathItem.Operations)
                    {
                        var operation = operationEntry.Value;
                        if (operation is null)
                        {
                            continue;
                        }

                        var location = $"operation '{operation.OperationId ?? operationEntry.Key.ToString()}'";

                        ValidatePartitionValue(diagnostics, sourceFilePath, operation.Extensions, location);

                        var effectivePartition = operation.Extensions.ExtractRateLimitPartition() ?? pathPartition;
                        var usesUserPartition = OpenApiRateLimitExtensions.ParsePartitionStrategy(effectivePartition)
                                                == RateLimitPartitionStrategy.User;

                        anyOperationInPathUsesUserPartition |= usesUserPartition;
                        anyOperationUsesUserPartition |= usesUserPartition;

                        // An operation-level claim is scoped to exactly this operation.
                        if (!string.IsNullOrEmpty(operation.Extensions.ExtractRateLimitPartitionClaim()) && !usesUserPartition)
                        {
                            AddPartitionClaimIgnoredDiagnostic(diagnostics, sourceFilePath, location, effectivePartition);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(pathClaim) && !anyOperationInPathUsesUserPartition)
                {
                    AddPartitionClaimIgnoredDiagnostic(diagnostics, sourceFilePath, $"path '{pathEntry.Key}'", pathPartition);
                }
            }
        }

        if (!string.IsNullOrEmpty(documentClaim) && !anyOperationUsesUserPartition)
        {
            AddPartitionClaimIgnoredDiagnostic(diagnostics, sourceFilePath, "document", documentPartition);
        }
    }

    /// <summary>
    /// Emits ATC_API_RL001 when the extensions declare an <c>x-ratelimit-partition</c> value that the
    /// generator does not recognize.
    /// </summary>
    private static void ValidatePartitionValue(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        IDictionary<string, IOpenApiExtension>? extensions,
        string location)
    {
        var declaredValue = extensions.ExtractRateLimitPartition();
        if (string.IsNullOrEmpty(declaredValue))
        {
            return;
        }

        var normalized = declaredValue!.ToLowerInvariant();
        if (normalized is "global" or "ip" or "user")
        {
            return;
        }

        diagnostics.Add(new DiagnosticMessage(
            RuleId: RuleIdentifiers.RateLimitPartitionValueUnrecognized,
            Message: $"Unrecognized '{RateLimitExtensionNameConstants.Partition}' value '{declaredValue}' on {location}. " +
                     "Expected 'global', 'ip' or 'user'. The value falls back to 'global', which means one shared " +
                     "rate-limit bucket for all callers.",
            Severity: DiagnosticSeverity.Warning,
            FilePath: sourceFilePath));
    }

    /// <summary>
    /// Emits ATC_API_RL002 for a <c>x-ratelimit-partition-claim</c> declaration that cannot take effect.
    /// </summary>
    private static void AddPartitionClaimIgnoredDiagnostic(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string location,
        string? effectivePartition)
    {
        var effective = string.IsNullOrEmpty(effectivePartition)
            ? "global"
            : effectivePartition;

        diagnostics.Add(new DiagnosticMessage(
            RuleId: RuleIdentifiers.RateLimitPartitionClaimWithoutUserPartition,
            Message: $"'{RateLimitExtensionNameConstants.PartitionClaim}' is declared on {location} but the effective " +
                     $"'{RateLimitExtensionNameConstants.Partition}' is '{effective}', so the claim is ignored. " +
                     $"Set '{RateLimitExtensionNameConstants.Partition}: user' to partition by claim.",
            Severity: DiagnosticSeverity.Warning,
            FilePath: sourceFilePath));
    }
}