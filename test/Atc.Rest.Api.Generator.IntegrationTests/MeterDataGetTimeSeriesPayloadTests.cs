namespace Atc.Rest.Api.Generator.IntegrationTests;

/// <summary>
/// Tests based on a real raw response body captured from the Eloverblik third-party API
/// endpoint POST /thirdpartyapi/api/meterdata/gettimeseries/{dateFrom}/{dateTo}/{aggregation},
/// which returns a MyEnergyDataMarketDocumentResponseListApiResponse.
///
/// The payload is dense with non-standard OpenAPI property keys - underscores, dots and
/// custom acronym casing (mRID, sender_MarketParticipant.name, period.timeInterval,
/// measurement_Unit.name, out_Quantity.quantity, MyEnergyData_MarketDocument) - which makes
/// it an ideal end-to-end regression guard for the JsonPropertyName generation.
/// </summary>
public class MeterDataGetTimeSeriesPayloadTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static string PayloadPath
        => Path.Combine(AppContext.BaseDirectory, "TestData", "meterdata-gettimeseries-response.json");

    private static string SpecificationPath
        => Path.Combine(AppContext.BaseDirectory, "TestData", "eloverblik-api-1.yaml");

    [Fact]
    public void GeneratedModels_CanMapEveryPropertyKeyInRealPayload()
    {
        // Arrange - generate the models straight from the Eloverblik OpenAPI specification
        var document = GeneratorTestHelper.LoadOpenApiDocument(SpecificationPath);
        var schemaNames = new HashSet<string>(document.Components!.Schemas!.Keys, StringComparer.Ordinal);

        var records = SchemaExtractor.ExtractForSchemas(
            document,
            "EloverblikThirdPartyApiClient",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(records);

        // Every JSON key the generated models are able to bind - either through an explicit
        // JsonPropertyName attribute or through the default (case-insensitive) name matching.
        var bindableKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records!.Parameters)
        {
            if (record.Parameters is null)
            {
                continue;
            }

            foreach (var parameter in record.Parameters)
            {
                var jsonPropertyName = parameter.Attributes?
                    .FirstOrDefault(a => string.Equals(a.Name, "JsonPropertyName", StringComparison.Ordinal));

                if (jsonPropertyName?.Content is { } content)
                {
                    bindableKeys.Add(content.Trim('"'));
                }
                else
                {
                    bindableKeys.Add(parameter.Name);
                }
            }
        }

        // Act - collect every distinct property key that actually occurs in the real payload
        using var payload = JsonDocument.Parse(File.ReadAllText(PayloadPath));
        var payloadKeys = new HashSet<string>(StringComparer.Ordinal);
        CollectPropertyNames(payload.RootElement, payloadKeys);

        // Assert
        var unmappedKeys = payloadKeys
            .Where(key => !bindableKeys.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unmappedKeys.Count == 0,
            $"The generated models cannot bind the following JSON keys from the real Eloverblik payload: {string.Join(", ", unmappedKeys)}");

        // Sanity check that the payload really does exercise the non-standard keys
        Assert.Contains("MyEnergyData_MarketDocument", payloadKeys, StringComparer.Ordinal);
        Assert.Contains("sender_MarketParticipant.mRID", payloadKeys, StringComparer.Ordinal);
        Assert.Contains("out_Quantity.quantity", payloadKeys, StringComparer.Ordinal);
        Assert.Contains("measurement_Unit.name", payloadKeys, StringComparer.Ordinal);
    }

    [Fact]
    public void Deserialize_RealPayload_PopulatesNonStandardProperties()
    {
        // Arrange
        var json = File.ReadAllText(PayloadPath);

        // Act
        var response = JsonSerializer.Deserialize<MyEnergyDataMarketDocumentResponseListApiResponse>(json, Options);

        // Assert
        Assert.NotNull(response);
        var item = Assert.Single(response!.Result!);

        Assert.True(item.Success);
        Assert.Equal(10000, item.ErrorCode);
        Assert.Equal("NoError", item.ErrorText);
        Assert.Equal("571313155411588152", item.Id);
        Assert.Null(item.StackTrace);

        var marketDocument = item.MyEnergyDataMarketDocument;
        Assert.NotNull(marketDocument);
        Assert.Equal("0HNO41GQN8CGL:00000051", marketDocument!.MRid);
        Assert.Equal("2026-08-27T13:01:56Z", marketDocument.CreatedDateTime);
        Assert.Equal(string.Empty, marketDocument.SenderMarketParticipantName);
        Assert.NotNull(marketDocument.SenderMarketParticipantMRid);
        Assert.Equal("2026-08-24T22:00:00Z", marketDocument.PeriodTimeInterval!.Start);
        Assert.Equal("2026-08-25T22:00:00Z", marketDocument.PeriodTimeInterval.End);

        var timeSeries = Assert.Single(marketDocument.TimeSeries!);
        Assert.Equal("571313155411588152", timeSeries.MRid);
        Assert.Equal("A04", timeSeries.BusinessType);
        Assert.Equal("A01", timeSeries.CurveType);
        Assert.Equal("KWH", timeSeries.MeasurementUnitName);
        Assert.Equal("A10", timeSeries.MarketEvaluationPoint!.MRid!.CodingScheme);
        Assert.Equal("571313155411588152", timeSeries.MarketEvaluationPoint.MRid.Name);

        var period = Assert.Single(timeSeries.Period!);
        Assert.Equal("PT15M", period.Resolution);
        Assert.Equal("2026-08-24T22:00:00Z", period.TimeInterval!.Start);

        // 96 quarter-of-an-hour points for a full day
        Assert.Equal(96, period.Point!.Count);
        Assert.Equal("1", period.Point[0].Position);
        Assert.Equal("7.522", period.Point[0].OutQuantityQuantity);
        Assert.Equal("A04", period.Point[0].OutQuantityQuality);
        Assert.Equal("96", period.Point[95].Position);
        Assert.Equal("7.997", period.Point[95].OutQuantityQuantity);
    }

    [Fact]
    public void Serialize_RealPayload_WritesOriginalOpenApiKeys()
    {
        // Arrange
        var json = File.ReadAllText(PayloadPath);
        var response = JsonSerializer.Deserialize<MyEnergyDataMarketDocumentResponseListApiResponse>(json, Options);

        // Act
        var serialized = JsonSerializer.Serialize(response, Options);

        // Assert - the original wire format keys must be preserved
        Assert.Contains("\"MyEnergyData_MarketDocument\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"mRID\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"sender_MarketParticipant.name\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"sender_MarketParticipant.mRID\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"period.timeInterval\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"measurement_Unit.name\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"out_Quantity.quantity\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"out_Quantity.quality\":", serialized, StringComparison.Ordinal);

        // Standard camelCase keys stay untouched
        Assert.Contains("\"createdDateTime\":", serialized, StringComparison.Ordinal);
        Assert.Contains("\"businessType\":", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Roundtrip_RealPayload_PreservesAllValues()
    {
        // Arrange
        var json = File.ReadAllText(PayloadPath);

        // Act
        var first = JsonSerializer.Deserialize<MyEnergyDataMarketDocumentResponseListApiResponse>(json, Options);
        var serialized = JsonSerializer.Serialize(first, Options);
        var second = JsonSerializer.Deserialize<MyEnergyDataMarketDocumentResponseListApiResponse>(serialized, Options);

        // Assert - a re-serialized payload must deserialize into an equivalent graph
        Assert.NotNull(second);
        var firstItem = Assert.Single(first!.Result!);
        var secondItem = Assert.Single(second!.Result!);

        Assert.Equal(firstItem.MyEnergyDataMarketDocument!.MRid, secondItem.MyEnergyDataMarketDocument!.MRid);
        Assert.Equal(
            firstItem.MyEnergyDataMarketDocument.TimeSeries![0].MeasurementUnitName,
            secondItem.MyEnergyDataMarketDocument.TimeSeries![0].MeasurementUnitName);
        Assert.Equal(
            firstItem.MyEnergyDataMarketDocument.TimeSeries[0].Period![0].Point!.Select(p => p.OutQuantityQuantity),
            secondItem.MyEnergyDataMarketDocument.TimeSeries[0].Period![0].Point!.Select(p => p.OutQuantityQuantity),
            StringComparer.Ordinal);
    }

    private static void CollectPropertyNames(
        JsonElement element,
        HashSet<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    CollectPropertyNames(property.Value, names);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectPropertyNames(item, names);
                }

                break;
        }
    }

    // The record definitions below mirror the shape emitted by the source generator for
    // the Eloverblik specification - see the generated *.g.cs output of the sample project
    // sample/ThridParty-Typed-Clients/EloverblikThirdPartyApiClient.
    private sealed record MyEnergyDataMarketDocumentResponseListApiResponse(
        List<MyEnergyDataMarketDocumentResponse>? Result);

    private sealed record MyEnergyDataMarketDocumentResponse(
        bool Success,
        string? ErrorText,
        string? Id,
        string? StackTrace,
        [property: JsonPropertyName("MyEnergyData_MarketDocument")] MyEnergyDataMarketDocument MyEnergyDataMarketDocument,
        int? ErrorCode = null);

    private sealed record MyEnergyDataMarketDocument(
        [property: JsonPropertyName("mRID")] string? MRid,
        string? CreatedDateTime,
        [property: JsonPropertyName("sender_MarketParticipant.name")] string? SenderMarketParticipantName,
        [property: JsonPropertyName("sender_MarketParticipant.mRID")] Eic SenderMarketParticipantMRid,
        [property: JsonPropertyName("period.timeInterval")] PeriodtimeInterval PeriodTimeInterval,
        List<TimeSeries>? TimeSeries);

    private sealed record Eic(
        string? CodingScheme,
        string? Name);

    private sealed record PeriodtimeInterval(
        string? Start,
        string? End);

    private sealed record TimeSeries(
        [property: JsonPropertyName("mRID")] string? MRid,
        string? BusinessType,
        string? CurveType,
        [property: JsonPropertyName("measurement_Unit.name")] string? MeasurementUnitName,
        MarketEvaluationPoint MarketEvaluationPoint,
        List<Period>? Period);

    private sealed record MarketEvaluationPoint(
        [property: JsonPropertyName("mRID")] MarketEvaluationMeteringPoint? MRid);

    private sealed record MarketEvaluationMeteringPoint(
        string? CodingScheme,
        string? Name);

    private sealed record Period(
        string? Resolution,
        PeriodtimeInterval TimeInterval,
        List<Point>? Point);

    private sealed record Point(
        string? Position,
        [property: JsonPropertyName("out_Quantity.quantity")] string? OutQuantityQuantity,
        [property: JsonPropertyName("out_Quantity.quality")] string? OutQuantityQuality);
}