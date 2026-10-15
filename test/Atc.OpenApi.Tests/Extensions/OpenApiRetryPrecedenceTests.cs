namespace Atc.OpenApi.Tests.Extensions;

/// <summary>
/// Every <c>x-retry-*</c> setting is read from the operation, else its path, else the document, else its default - one
/// setting at a time, so a level that is skipped or read in the wrong order shows (mutation testing found the order and
/// the defaults unpinned).
/// </summary>
public class OpenApiRetryPrecedenceTests
{
    public static TheoryData<string, string, string, string, string, string> Settings
        => new()
        {
            // key, document, path, operation, the RetryConfiguration property, its default
            { "x-retry-policy", "doc", "path", "op", nameof(RetryConfiguration.Policy), "doc" },
            { "x-retry-max-attempts", "2", "4", "6", nameof(RetryConfiguration.MaxAttempts), "3" },
            { "x-retry-delay-seconds", "0.5", "1.5", "2.5", nameof(RetryConfiguration.DelaySeconds), "1" },
            { "x-retry-backoff", "linear", "constant", "linear", nameof(RetryConfiguration.BackoffType), nameof(RetryBackoffType.Exponential) },
            { "x-retry-use-jitter", "false", "true", "false", nameof(RetryConfiguration.UseJitter), "True" },
            { "x-retry-timeout-seconds", "5", "6", "7", nameof(RetryConfiguration.TimeoutSeconds), string.Empty },
            { "x-retry-total-timeout-seconds", "50", "60", "70", nameof(RetryConfiguration.TotalTimeoutSeconds), string.Empty },
            { "x-retry-circuit-breaker", "true", "false", "true", nameof(RetryConfiguration.CircuitBreakerEnabled), "False" },
            { "x-retry-cb-failure-ratio", "0.1", "0.2", "0.3", nameof(RetryConfiguration.CircuitBreakerFailureRatio), "0.5" },
            { "x-retry-cb-sampling-duration-seconds", "10", "20", "40", nameof(RetryConfiguration.CircuitBreakerSamplingDurationSeconds), "30" },
            { "x-retry-cb-minimum-throughput", "2", "4", "6", nameof(RetryConfiguration.CircuitBreakerMinimumThroughput), "10" },
            { "x-retry-cb-break-duration-seconds", "5", "15", "25", nameof(RetryConfiguration.CircuitBreakerBreakDurationSeconds), "30" },
            { "x-retry-handle-429", "false", "true", "false", nameof(RetryConfiguration.Handle429), "True" },
        };

    [Theory]
    [MemberData(nameof(Settings))]
    public void ExtractRetryConfiguration_ASetting_IsTheOperationsElseThePathsElseTheDocumentsElseItsDefault(
        string key,
        string documentValue,
        string pathValue,
        string operationValue,
        string property,
        string defaultValue)
    {
        string Read(
            string? atDocument,
            string? atPath,
            string? atOperation)
        {
            // A document policy makes a configuration exist; the setting under test is the only other difference
            var document = key == "x-retry-policy" ? atDocument : "doc";
            var yaml = $$"""
                openapi: 3.0.0
                info: { title: Contoso, version: 1.0.0 }
                x-retry-policy: {{document}}
                {{(atDocument is null || key == "x-retry-policy" ? string.Empty : $"{key}: {atDocument}")}}
                paths:
                  /things:
                    {{(atPath is null ? string.Empty : $"{key}: {atPath}")}}
                    get:
                      operationId: listThings
                      {{(atOperation is null ? string.Empty : $"{key}: {atOperation}")}}
                      responses:
                        '200': { description: OK }
                """;
            Assert.True(OpenApiDocumentHelper.TryParseYaml(yaml, "contoso.yaml", out var parsed), yaml);
            var pathItem = (OpenApiPathItem)parsed!.Paths.First().Value;
            var configuration = pathItem.Operations!.First().Value.ExtractRetryConfiguration(pathItem, parsed);
            var value = typeof(RetryConfiguration).GetProperty(property)!.GetValue(configuration);
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        Assert.Equal(defaultValue, Read(key == "x-retry-policy" ? "doc" : null, null, null));
        Assert.Equal(Normalize(documentValue), Read(documentValue, null, null));
        Assert.Equal(Normalize(pathValue), Read(documentValue, pathValue, null));
        Assert.Equal(Normalize(operationValue), Read(documentValue, pathValue, operationValue));

        string Normalize(string value)
            => property == nameof(RetryConfiguration.BackoffType)
                ? Enum.Parse<RetryBackoffType>(value, ignoreCase: true).ToString()
                : bool.TryParse(value, out var flag) ? flag.ToString() : value;
    }

    /// <summary>
    /// An operation that turns retries off keeps the timeout of its own, else its path's, else the document's.
    /// </summary>
    [Theory]
    [InlineData("4", "5", "6", "6")]
    [InlineData("4", "5", null, "5")]
    [InlineData("4", null, null, "4")]
    [InlineData(null, null, null, null)]
    public void ExtractRetryConfiguration_AnOperationWithRetriesOff_KeepsTheNearestTimeout(
        string? atDocument,
        string? atPath,
        string? atOperation,
        string? expected)
    {
        var yaml = $$"""
            openapi: 3.0.0
            info: { title: Contoso, version: 1.0.0 }
            x-retry-policy: doc
            {{(atDocument is null ? string.Empty : $"x-retry-timeout-seconds: {atDocument}")}}
            paths:
              /things:
                {{(atPath is null ? string.Empty : $"x-retry-timeout-seconds: {atPath}")}}
                get:
                  operationId: listThings
                  x-retry-enabled: false
                  {{(atOperation is null ? string.Empty : $"x-retry-timeout-seconds: {atOperation}")}}
                  responses:
                    '200': { description: OK }
            """;
        Assert.True(OpenApiDocumentHelper.TryParseYaml(yaml, "contoso.yaml", out var parsed), yaml);
        var pathItem = (OpenApiPathItem)parsed!.Paths.First().Value;

        var configuration = pathItem.Operations!.First().Value.ExtractRetryConfiguration(pathItem, parsed);

        Assert.NotNull(configuration);
        Assert.False(configuration.Enabled);
        Assert.Equal(expected, configuration.TimeoutSeconds?.ToString(CultureInfo.InvariantCulture));
    }
}