namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class JsonPropertyNameHelperTests
{
    [Theory]
    [InlineData("Status", "Status")]
    [InlineData("id", "Id")]
    [InlineData("errorText", "ErrorText")]
    public void CreateJsonPropertyNameAttribute_WhenNotRequired_ReturnsNull(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.CreateJsonPropertyNameAttribute(
            jsonKey,
            csharpPropertyName);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("error_code", "ErrorCode", "error_code")]
    [InlineData("x-correlation-id", "XCorrelationId", "x-correlation-id")]
    [InlineData("sender_MarketParticipant.name", "SenderMarketParticipantName", "sender_MarketParticipant.name")]
    [InlineData("mRID", "MRid", "mRID")]
    public void CreateJsonPropertyNameAttribute_WhenRequired_ReturnsExpectedAttribute(
        string jsonKey,
        string csharpPropertyName,
        string expectedJsonKey)
    {
        var result = JsonPropertyNameHelper.CreateJsonPropertyNameAttribute(
            jsonKey,
            csharpPropertyName);

        Assert.NotNull(result);
        Assert.Equal("JsonPropertyName", result!.Name);
        Assert.Equal($"\"{expectedJsonKey}\"", result.Content);
    }

    [Theory]
    [InlineData(null, "PropertyName")]
    [InlineData("", "PropertyName")]
    [InlineData("propertyName", null)]
    [InlineData("propertyName", "")]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void RequiresJsonPropertyName_WithNullOrEmptyValue_ReturnsFalse(
    string? jsonKey,
    string? csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey!,
            csharpPropertyName!);

        Assert.False(result);
    }

    [Theory]
    [InlineData("Status", "Status")]
    [InlineData("Id", "Id")]
    [InlineData("ErrorText", "ErrorText")]
    public void RequiresJsonPropertyName_WithExactMatch_ReturnsFalse(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.False(result);
    }

    [Theory]
    [InlineData("status", "Status")]
    [InlineData("id", "Id")]
    [InlineData("errorText", "ErrorText")]
    [InlineData("createdDateTime", "CreatedDateTime")]
    public void RequiresJsonPropertyName_WithStandardCamelCase_ReturnsFalse(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.False(result);
    }

    [Theory]
    [InlineData("MyEnergyData_MarketDocument", "MyEnergyDataMarketDocument")]
    [InlineData("error_code", "ErrorCode")]
    [InlineData("created_date_time", "CreatedDateTime")]
    public void RequiresJsonPropertyName_WithUnderscore_ReturnsTrue(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.True(result);
    }

    [Theory]
    [InlineData("sender_MarketParticipant.name", "SenderMarketParticipantName")]
    [InlineData("period.timeInterval", "PeriodTimeInterval")]
    public void RequiresJsonPropertyName_WithDots_ReturnsTrue(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.True(result);
    }

    [Theory]
    [InlineData("x-correlation-id", "XCorrelationId")]
    [InlineData("api-key", "ApiKey")]
    public void RequiresJsonPropertyName_WithHyphens_ReturnsTrue(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.True(result);
    }

    [Theory]
    [InlineData("mRID", "MRid")]
    [InlineData("eICCode", "EicCode")]
    public void RequiresJsonPropertyName_WithAcronymCasingMismatch_ReturnsTrue(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.True(result);
    }

    [Theory]
    [InlineData("StatusValue", "StatusValue")]
    [InlineData("statusValue", "StatusValue")]
    public void RequiresJsonPropertyName_WithEnclosingTypeCollisionRename_ReturnsFalse(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.False(result);
    }

    [Theory]
    [InlineData("differentName", "PropertyName")]
    [InlineData("property_name", "PropertyName")]
    [InlineData("property.name", "PropertyName")]
    [InlineData("property-name", "PropertyName")]
    [InlineData("PropertyName2", "PropertyName")]
    public void RequiresJsonPropertyName_WithDifferentName_ReturnsTrue(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.True(result);
    }

    [Theory]
    [InlineData("name", "Name")]
    [InlineData("errorText", "ErrorText")]
    [InlineData("id", "Id")]
    public void RequiresJsonPropertyName_StandardCamelCase_ReturnsFalse(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.False(result);
    }

    [Theory]
    [InlineData("Success", "Success")]
    [InlineData("Pet", "Pet")]
    public void RequiresJsonPropertyName_StandardPascalCase_ReturnsFalse(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.False(result);
    }

    [Fact]
    public void RequiresJsonPropertyName_PropertyWithUnderscore_ReturnsTrue()
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            "MyEnergyData_MarketDocument",
            "MyEnergyDataMarketDocument");

        Assert.True(result);
    }

    [Theory]
    [InlineData("error_code", "ErrorCode")]
    [InlineData("created_date_time", "CreatedDateTime")]
    public void RequiresJsonPropertyName_SnakeCase_ReturnsTrue(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.True(result);
    }

    [Theory]
    [InlineData("sender_MarketParticipant.name", "SenderMarketParticipantName")]
    [InlineData("period.timeInterval", "PeriodTimeInterval")]
    public void RequiresJsonPropertyName_DotNotation_ReturnsTrue(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.True(result);
    }

    [Theory]
    [InlineData("api-key", "ApiKey")]
    [InlineData("content-type", "ContentType")]
    public void RequiresJsonPropertyName_PropertyWithHyphen_ReturnsTrue(
        string jsonKey,
        string csharpPropertyName)
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            jsonKey,
            csharpPropertyName);

        Assert.True(result);
    }

    [Fact]
    public void RequiresJsonPropertyName_AcronymCasingMismatch_ReturnsTrue()
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            "mRID",
            "MRid");

        Assert.True(result);
    }

    [Fact]
    public void RequiresJsonPropertyName_RenamedPropertyMatchingEnclosingClass_ReturnsTrue()
    {
        var result = JsonPropertyNameHelper.RequiresJsonPropertyName(
            "Status",
            "StatusValue");

        Assert.True(result);
    }

    [Fact]
    public void CreateJsonPropertyNameAttribute_WithUnderscore_ReturnsExpectedAttributeParameters()
    {
        var result = JsonPropertyNameHelper.CreateJsonPropertyNameAttribute(
            "MyEnergyData_MarketDocument",
            "MyEnergyDataMarketDocument");

        Assert.NotNull(result);
        Assert.Equal("JsonPropertyName", result.Name);
        Assert.Equal("\"MyEnergyData_MarketDocument\"", result.Content);
    }
}