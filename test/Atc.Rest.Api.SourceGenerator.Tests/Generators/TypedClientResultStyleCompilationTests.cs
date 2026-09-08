namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Verifies that a typed client generated with <c>typedClientResultStyle: Result</c> compiles for
/// real against Atc.Rest.Client. The extractor unit tests only assert on MethodParameters and the
/// snapshot tests only compare text, so this is the only place that proves the emitted envelope
/// builders actually bind to the real EndpointResponse constructors.
/// </summary>
public class TypedClientResultStyleCompilationTests
{
    private const string ScenarioName = "TypedClientResultStyle";
    private const string YamlFileName = "TypedClientResultStyle.yaml";

    [Fact]
    public void ResultStyleClient_Compiles()
    {
        // Arrange & Act
        var generatedSources = CompilationVerificationHarness.RunClient(ScenarioName, YamlFileName);
        var errors = CompilationVerificationHarness.CompileGeneratedSources(generatedSources);

        // Assert
        Assert.True(
            errors.Count == 0,
            "Generated Result-style client failed to compile:\n" + string.Join("\n", errors));
    }

    [Fact]
    public void ResultStyleClient_EmitsEnvelopeReturnTypes()
    {
        // Arrange & Act
        var client = GetClientSource();

        // Assert - each payload kind maps to its own envelope shape.
        Assert.Contains("Task<EndpointResponse<List<Device>>> ListDevicesAsync", client, StringComparison.Ordinal);
        Assert.Contains("Task<EndpointResponse<Device>> GetDeviceByIdAsync", client, StringComparison.Ordinal);
        Assert.Contains("Task<EndpointResponse> DeleteDeviceAsync", client, StringComparison.Ordinal);
        Assert.Contains("Task<EndpointResponse<byte[]>> DownloadFirmwareAsync", client, StringComparison.Ordinal);
        Assert.Contains("Task<EndpointResponse<string>> GetDeviceLogAsync", client, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultStyleClient_ImportsAtcRestClient()
    {
        // Arrange & Act
        var client = GetClientSource();

        // Assert - the envelope types live in Atc.Rest.Client. Regression guard: the CLI/snapshot
        // path builds its own using list, so this import has to be added in two separate places.
        Assert.Contains("using Atc.Rest.Client;", client, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultStyleClient_DoesNotThrowOnNonSuccessStatus()
    {
        // Arrange & Act
        var client = GetClientSource();

        // Assert - Result style reports the status through the envelope instead of throwing, so no
        // operation body may call the throwing helper.
        Assert.DoesNotContain("await EnsureSuccessAsync(", client, StringComparison.Ordinal);
        Assert.Contains("BuildJsonResponseAsync<Device>(response", client, StringComparison.Ordinal);
        Assert.Contains("BuildEmptyResponseAsync(response", client, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultStyleClient_OnlyDeserializesOnSuccess()
    {
        // Arrange & Act
        var client = GetClientSource();

        // Assert - EndpointResponse<T>.SuccessContent casts contentObject and throws on a mismatch,
        // so an error body must never be deserialized into TSuccess.
        Assert.Contains(
            "if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(content))",
            client,
            StringComparison.Ordinal);
    }

    private static string GetClientSource()
    {
        var generatedSources = CompilationVerificationHarness.RunClient(ScenarioName, YamlFileName);

        var client = generatedSources
            .FirstOrDefault(s => s.HintName.EndsWith("DeviceApiClient.g.cs", StringComparison.Ordinal));

        Assert.False(
            string.IsNullOrEmpty(client.Source),
            "DeviceApiClient was not generated. Generated: " +
            string.Join(", ", generatedSources.Select(s => s.HintName)));

        return client.Source;
    }
}