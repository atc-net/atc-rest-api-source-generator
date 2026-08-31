namespace Atc.Rest.Api.Generator.Tests.Configurations;

/// <summary>
/// Tests for the <c>clientGranularity</c> and <c>clientName</c> marker-file options.
/// </summary>
/// <remarks>
/// The default of <see cref="ClientGranularityType.PerArea"/> is load-bearing for backward
/// compatibility: every existing marker file omits these settings, so the default must reproduce
/// today's behaviour exactly.
/// </remarks>
public class ClientGranularityConfigTests
{
    // ========== Defaults (backward compatibility) ==========
    [Fact]
    public void ClientConfig_DefaultGranularity_IsPerArea()
    {
        // Act
        var config = new ClientConfig();

        // Assert
        Assert.Equal(ClientGranularityType.PerArea, config.ClientGranularity);
        Assert.Null(config.ClientName);
    }

    [Fact]
    public void ClientConfig_EmptyJson_UsesPerAreaDefault()
    {
        // Arrange
        var json = "{}";

        // Act
        var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(ClientGranularityType.PerArea, config.ClientGranularity);
        Assert.Null(config.ClientName);
    }

    // ========== PascalCase and kebab-case parsing ==========
    [Theory]
    [InlineData("Single", ClientGranularityType.Single)]
    [InlineData("single", ClientGranularityType.Single)]
    [InlineData("PerArea", ClientGranularityType.PerArea)]
    [InlineData("perarea", ClientGranularityType.PerArea)]
    [InlineData("per-area", ClientGranularityType.PerArea)]
    public void ClientConfig_CanDeserializeGranularity(
        string value,
        ClientGranularityType expected)
    {
        // Arrange
        var json = $$"""
            {
                "clientGranularity": "{{value}}"
            }
            """;

        // Act
        var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(expected, config.ClientGranularity);
    }

    /// <summary>
    /// An unrecognised value must fall back to the safe default rather than throwing, matching the
    /// behaviour of <c>GenerationModeTypeConverter</c>.
    /// </summary>
    [Theory]
    [InlineData("\"NotAGranularity\"")]
    [InlineData("\"\"")]
    [InlineData("null")]
    public void ClientConfig_UnknownGranularity_FallsBackToPerArea(
        string jsonValue)
    {
        // Arrange
        var json = $$"""
            {
                "clientGranularity": {{jsonValue}}
            }
            """;

        // Act
        var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(ClientGranularityType.PerArea, config.ClientGranularity);
    }

    // ========== clientName ==========
    [Fact]
    public void ClientConfig_CanDeserializeClientName()
    {
        // Arrange
        var json = """
            {
                "clientGranularity": "Single",
                "clientName": "MyApiClient"
            }
            """;

        // Act
        var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(ClientGranularityType.Single, config.ClientGranularity);
        Assert.Equal("MyApiClient", config.ClientName);
    }

    /// <summary>
    /// Round-trips through the converter's Write path so the emitted value stays parseable.
    /// </summary>
    [Theory]
    [InlineData(ClientGranularityType.Single)]
    [InlineData(ClientGranularityType.PerArea)]
    public void ClientConfig_Granularity_RoundTrips(
        ClientGranularityType granularity)
    {
        // Arrange
        var original = new ClientConfig { ClientGranularity = granularity };

        // Act
        var json = JsonSerializer.Serialize(original, JsonHelper.ConfigOptions);
        var roundTripped = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        // Assert
        Assert.NotNull(roundTripped);
        Assert.Equal(granularity, roundTripped.ClientGranularity);
    }

    /// <summary>
    /// Guards that adding the new options does not disturb the existing ones.
    /// </summary>
    [Fact]
    public void ClientConfig_GranularityCombinedWithExistingOptions_AllParse()
    {
        // Arrange
        var json = """
            {
                "generationMode": "TypedClient",
                "clientSuffix": "Client",
                "clientGranularity": "Single",
                "clientName": "ShowcaseApiClient",
                "includeDeprecated": false
            }
            """;

        // Act
        var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(GenerationModeType.TypedClient, config.GenerationMode);
        Assert.Equal("Client", config.ClientSuffix);
        Assert.Equal(ClientGranularityType.Single, config.ClientGranularity);
        Assert.Equal("ShowcaseApiClient", config.ClientName);
        Assert.False(config.IncludeDeprecated);
    }
}