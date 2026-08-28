namespace Atc.Rest.Api.Generator.Tests.Serialization;

/// <summary>
/// Verifies that records shaped like the generated output - using
/// [property: JsonPropertyName("...")] for OpenAPI keys with underscores, dots
/// and non-standard casing - roundtrip correctly with System.Text.Json.
/// </summary>
public class RecordSerializationTests
{
    private const string Json = """
                                {
                                  "success": true,
                                  "errorText": null,
                                  "MyEnergyData_MarketDocument": {
                                    "mRID": "doc-1",
                                    "createdDateTime": "2024-01-01T00:00:00Z",
                                    "sender_MarketParticipant.name": "Energinet",
                                    "period.timeInterval": "PT1H"
                                  }
                                }
                                """;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Deserialize_WithNonStandardKeys_PopulatesAllProperties()
    {
        // Act
        var result = JsonSerializer.Deserialize<MyEnergyDataMarketDocumentResponse>(Json, Options);

        // Assert
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.NotNull(result.MyEnergyDataMarketDocument);
        Assert.Equal("doc-1", result.MyEnergyDataMarketDocument.MRid);
        Assert.Equal("2024-01-01T00:00:00Z", result.MyEnergyDataMarketDocument.CreatedDateTime);
        Assert.Equal("Energinet", result.MyEnergyDataMarketDocument.SenderMarketParticipantName);
        Assert.Equal("PT1H", result.MyEnergyDataMarketDocument.PeriodTimeInterval);
    }

    [Fact]
    public void Serialize_WithNonStandardKeys_WritesOriginalOpenApiKeys()
    {
        // Arrange
        var model = new MyEnergyDataMarketDocumentResponse(
            Success: true,
            ErrorText: null,
            MyEnergyDataMarketDocument: new MyEnergyDataMarketDocument(
                MRid: "doc-1",
                CreatedDateTime: "2024-01-01T00:00:00Z",
                SenderMarketParticipantName: "Energinet",
                PeriodTimeInterval: "PT1H"));

        // Act
        var json = JsonSerializer.Serialize(model, Options);

        // Assert
        Assert.Contains("\"MyEnergyData_MarketDocument\":", json, StringComparison.Ordinal);
        Assert.Contains("\"mRID\":", json, StringComparison.Ordinal);
        Assert.Contains("\"sender_MarketParticipant.name\":", json, StringComparison.Ordinal);
        Assert.Contains("\"period.timeInterval\":", json, StringComparison.Ordinal);
        Assert.Contains("\"success\":", json, StringComparison.Ordinal);
        Assert.Contains("\"createdDateTime\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Roundtrip_WithNonStandardKeys_PreservesValues()
    {
        // Act
        var deserialized = JsonSerializer.Deserialize<MyEnergyDataMarketDocumentResponse>(Json, Options);
        var json = JsonSerializer.Serialize(deserialized, Options);
        var roundtripped = JsonSerializer.Deserialize<MyEnergyDataMarketDocumentResponse>(json, Options);

        // Assert
        Assert.Equal(deserialized, roundtripped);
    }

    /// <summary>
    /// Mirrors the generated record shape for MyEnergyDataMarketDocumentResponse.
    /// </summary>
    private sealed record MyEnergyDataMarketDocumentResponse(
        bool Success,
        string? ErrorText,
        [property: JsonPropertyName("MyEnergyData_MarketDocument")] MyEnergyDataMarketDocument MyEnergyDataMarketDocument);

    /// <summary>
    /// Mirrors the generated record shape for MyEnergyDataMarketDocument.
    /// </summary>
    private sealed record MyEnergyDataMarketDocument(
        [property: JsonPropertyName("mRID")] string? MRid,
        string? CreatedDateTime,
        [property: JsonPropertyName("sender_MarketParticipant.name")] string? SenderMarketParticipantName,
        [property: JsonPropertyName("period.timeInterval")] string? PeriodTimeInterval);
}