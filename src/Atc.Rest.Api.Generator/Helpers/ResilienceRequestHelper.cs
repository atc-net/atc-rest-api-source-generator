namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Emits the statement a generated C# client uses to tell the API resilience handler how to run one
/// operation's request (<c>ApiResilienceRequest.UsePolicy</c> / <c>DisableRetry</c>). Uses the same
/// resolution as the TypeScript client (<see cref="OpenApiRetryExtensions.ResolveOperationRetry"/>).
/// </summary>
public static class ResilienceRequestHelper
{
    /// <summary>
    /// Returns the stamp statement for an operation, or null when the handler's own behavior already
    /// matches (its default policy for safe methods, no retry for POST/PATCH) or the specification has
    /// no retry configuration at all.
    /// </summary>
    /// <param name="operation">The OpenAPI operation.</param>
    /// <param name="pathItem">The path item containing the operation.</param>
    /// <param name="document">The OpenAPI document.</param>
    /// <param name="httpMethod">The HTTP method of the operation.</param>
    /// <param name="requestVariable">The name of the <c>HttpRequestMessage</c> variable.</param>
    /// <returns>The statement (ending in a semicolon), or null.</returns>
    public static string? BuildStampStatement(
        OpenApiOperation operation,
        IOpenApiPathItem? pathItem,
        OpenApiDocument document,
        string httpMethod,
        string requestVariable)
    {
        if (operation is null ||
            pathItem is null ||
            document is null ||
            !document.HasRetryConfiguration())
        {
            return null;
        }

        var retry = operation.ResolveOperationRetry(pathItem, document, httpMethod);

        // A request body sent as a stream of records is read once, as it is written: it is never retried (its policy's
        // timeout still applies)
        if (operation.GetStreamedRequestBody() is not null)
        {
            if (retry.Mode != OperationRetryMode.Retry &&
                retry.PolicyName is null &&
                retry.TimeoutSeconds is null &&
                OpenApiRetryExtensions.IsRetryUnsafeHttpMethod(httpMethod))
            {
                return null;
            }

            var streamedArguments = new List<string> { requestVariable };
            if (retry.PolicyName is not null)
            {
                streamedArguments.Add("ResiliencePolicies." + ResiliencePoliciesExtractor.GenerateConstantName(retry.PolicyName));
            }

            if (retry.TimeoutSeconds is { } streamedTimeoutSeconds)
            {
                streamedArguments.Add("timeoutSeconds: " + streamedTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
            }

            return $"ApiResilienceRequest.DisableRetry({string.Join(", ", streamedArguments)});";
        }

        if (retry.IsClientDefault(httpMethod))
        {
            return null;
        }

        var arguments = new List<string> { requestVariable };
        if (retry.PolicyName is not null)
        {
            arguments.Add("ResiliencePolicies." + ResiliencePoliciesExtractor.GenerateConstantName(retry.PolicyName));
        }

        if (retry.Mode == OperationRetryMode.Retry)
        {
            return $"ApiResilienceRequest.UsePolicy({string.Join(", ", arguments)});";
        }

        if (retry.TimeoutSeconds is { } timeoutSeconds)
        {
            arguments.Add("timeoutSeconds: " + timeoutSeconds.ToString(CultureInfo.InvariantCulture));
        }

        return $"ApiResilienceRequest.DisableRetry({string.Join(", ", arguments)});";
    }

    /// <summary>
    /// Returns the statement that has the API resilience handler read a response's body inside the attempt
    /// (<c>ApiResilienceRequest.BufferResponse</c>), so the policy's timeout and retry cover the body too; null when
    /// the specification has no retry configuration (no resilience handler is generated).
    /// For every response a generated client reads whole - never for one it streams.
    /// </summary>
    /// <param name="document">The OpenAPI document.</param>
    /// <param name="requestVariable">The name of the <c>HttpRequestMessage</c> variable.</param>
    /// <returns>The statement (ending in a semicolon), or null.</returns>
    public static string? BuildBufferStatement(
        OpenApiDocument document,
        string requestVariable)
        => document is not null && document.HasRetryConfiguration()
            ? $"ApiResilienceRequest.BufferResponse({requestVariable});"
            : null;
}