namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// An <c>x-retry-*</c> setting no generated policy reads is a warning (<c>ATC_API_RTY001</c>). A named policy takes its
/// values from where its name is first declared, inheriting operation → path → document (as
/// <see cref="OpenApiRetryExtensions.CollectNamedRetryPolicies"/> builds them), so a setting at a level no such
/// declaration covers is dropped - with no <c>x-retry-policy</c> in the document, every one. The exception is the
/// timeout an operation that is not retried keeps (<see cref="OpenApiRetryExtensions.ResolveOperationRetry"/>).
/// </summary>
internal static class RetrySettingValidator
{
    private const string TimeoutSetting = RetryExtensionNameConstants.TimeoutSeconds;

    // The settings a policy reads; x-retry-policy names one and x-retry-enabled is read per operation
    private static readonly string[] PolicySettings =
    [
        RetryExtensionNameConstants.MaxAttempts,
        RetryExtensionNameConstants.DelaySeconds,
        RetryExtensionNameConstants.Backoff,
        RetryExtensionNameConstants.UseJitter,
        RetryExtensionNameConstants.TimeoutSeconds,
        RetryExtensionNameConstants.TotalTimeoutSeconds,
        RetryExtensionNameConstants.CircuitBreaker,
        RetryExtensionNameConstants.CircuitBreakerFailureRatio,
        RetryExtensionNameConstants.CircuitBreakerSamplingDurationSeconds,
        RetryExtensionNameConstants.CircuitBreakerMinimumThroughput,
        RetryExtensionNameConstants.CircuitBreakerBreakDurationSeconds,
        RetryExtensionNameConstants.Handle429,
    ];

    public static List<DiagnosticMessage> Validate(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        // The levels a policy reads: each first declaration of a name, and every level above it
        var read = new HashSet<object>(ByReference.Instance);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (PolicyName(document.Extensions) is { } documentPolicy && seen.Add(documentPolicy))
        {
            read.Add(document);
        }

        var paths = (document.Paths ?? [])
            .Where(p => p.Value is not null)
            .Select(p => (Path: p.Key, Item: p.Value!))
            .ToList();
        foreach (var (_, pathItem) in paths)
        {
            if (PolicyName(pathItem.Extensions) is { } pathPolicy && seen.Add(pathPolicy))
            {
                read.Add(pathItem);
                read.Add(document);
            }

            foreach (var operation in (pathItem.Operations ?? []).Values.Where(o => o is not null))
            {
                if (PolicyName(operation.Extensions) is { } operationPolicy && seen.Add(operationPolicy))
                {
                    read.Add(operation);
                    read.Add(pathItem);
                    read.Add(document);
                }
            }
        }

        var hasPolicy = seen.Count > 0;
        Report(document.Extensions, read.Contains(document), "the document", timeoutRead: false);
        foreach (var (path, pathItem) in paths)
        {
            // A not-retried operation without a timeout of its own keeps its path's (when the path names no policy)
            var pathTimeoutRead = hasPolicy &&
                                  PolicyName(pathItem.Extensions) is null &&
                                  (pathItem.Operations ?? []).Any(o => o.Value is not null &&
                                      !o.Value.Extensions.HasSetting(TimeoutSetting) &&
                                      o.Value.ResolveOperationRetry(pathItem, document, o.Key.Method).Mode == OperationRetryMode.NoRetry);
            Report(pathItem.Extensions, read.Contains(pathItem), $"path '{path}'", pathTimeoutRead);

            foreach (var pair in (pathItem.Operations ?? []).Where(o => o.Value is not null))
            {
                var method = pair.Key;
                var operation = pair.Value;

                // A not-retried operation keeps its own timeout
                var timeoutRead = hasPolicy && operation.ResolveOperationRetry(pathItem, document, method.Method).Mode == OperationRetryMode.NoRetry;
                Report(operation.Extensions, read.Contains(operation), $"operation '{operation.OperationId ?? $"{method.Method} {path}"}'", timeoutRead);
            }
        }

        return diagnostics;

        void Report(
            IDictionary<string, IOpenApiExtension>? extensions,
            bool levelRead,
            string where,
            bool timeoutRead)
        {
            if (levelRead || extensions is null)
            {
                return;
            }

            foreach (var setting in PolicySettings.Where(extensions.HasSetting))
            {
                if (setting == TimeoutSetting && timeoutRead)
                {
                    continue;
                }

                diagnostics.Add(new DiagnosticMessage(
                    RuleIdentifiers.RetrySettingNotApplied,
                    hasPolicy
                        ? $"'{setting}' on {where} is not applied: a policy takes its values from where its name is first declared, and no x-retry-policy declared there or below reads this level."
                        : $"'{setting}' on {where} is not applied: the document declares no x-retry-policy, so no retry code is generated.",
                    DiagnosticSeverity.Warning,
                    sourceFilePath,
                    Suggestions: ["Give this level its own x-retry-policy (a policy name declared once), or move the setting to where the policy is declared"]));
            }
        }
    }

    private static string? PolicyName(
        IDictionary<string, IOpenApiExtension>? extensions)
        => extensions.ExtractRetryPolicy() is { Length: > 0 } name ? name : null;

    private sealed class ByReference : IEqualityComparer<object>
    {
        public static ByReference Instance { get; } = new();

        public new bool Equals(
            object? x,
            object? y)
            => ReferenceEquals(x, y);

        public int GetHashCode(object obj)
            => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    private static bool HasSetting(
        this IDictionary<string, IOpenApiExtension>? extensions,
        string setting)
        => extensions?.ContainsKey(setting) == true;
}