namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class HealthCheckExtractorTests
{
    [Fact]
    public void GenerateEndpoints_DefaultConfig_ProducesMapHealthChecks()
    {
        // Arrange
        var config = new HealthCheckConfig { Enabled = true };

        // Act
        var result = HealthCheckExtractor.GenerateEndpoints("TestApi", config);

        // Assert
        Assert.Contains("MapHealthChecks", result, StringComparison.Ordinal);
        Assert.Contains("/health", result, StringComparison.Ordinal);
        Assert.Contains("MapHealthCheckEndpoints", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateEndpoints_IncludesLivenessAndReadiness()
    {
        // Arrange
        var config = new HealthCheckConfig
        {
            Enabled = true,
            IncludeLiveness = true,
            IncludeReadiness = true,
        };

        // Act
        var result = HealthCheckExtractor.GenerateEndpoints("TestApi", config);

        // Assert
        Assert.Contains("/live", result, StringComparison.Ordinal);
        Assert.Contains("/ready", result, StringComparison.Ordinal);
        Assert.Contains("\"live\"", result, StringComparison.Ordinal);
        Assert.Contains("\"ready\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateEndpoints_LivenessDisabled_ExcludesLive()
    {
        // Arrange
        var config = new HealthCheckConfig
        {
            Enabled = true,
            IncludeLiveness = false,
            IncludeReadiness = true,
        };

        // Act
        var result = HealthCheckExtractor.GenerateEndpoints("TestApi", config);

        // Assert
        Assert.DoesNotContain("/live", result, StringComparison.Ordinal);
        Assert.Contains("/ready", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateEndpoints_ReadinessDisabled_ExcludesReady()
    {
        // Arrange
        var config = new HealthCheckConfig
        {
            Enabled = true,
            IncludeLiveness = true,
            IncludeReadiness = false,
        };

        // Act
        var result = HealthCheckExtractor.GenerateEndpoints("TestApi", config);

        // Assert
        Assert.Contains("/live", result, StringComparison.Ordinal);
        Assert.DoesNotContain("/ready", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateEndpoints_SecurityNone_NoFilter()
    {
        // Arrange
        var config = new HealthCheckConfig
        {
            Enabled = true,
            Security = "none",
        };

        // Act
        var result = HealthCheckExtractor.GenerateEndpoints("TestApi", config);

        // Assert
        Assert.DoesNotContain("AddEndpointFilter", result, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiKey", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Unauthorized", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateEndpoints_SecurityApiKey_GeneratesFilterWithQueryAndHeader()
    {
        // Arrange
        var config = new HealthCheckConfig
        {
            Enabled = true,
            Security = "apiKey",
            ApiKeyQueryParameterName = "api-key",
            ApiKeyHeaderName = "X-Health-Api-Key",
        };

        // Act
        var result = HealthCheckExtractor.GenerateEndpoints("TestApi", config);

        // Assert
        Assert.Contains("AddEndpointFilter", result, StringComparison.Ordinal);
        Assert.Contains("HealthChecks:ApiKey", result, StringComparison.Ordinal);
        Assert.Contains("api-key", result, StringComparison.Ordinal);
        Assert.Contains("X-Health-Api-Key", result, StringComparison.Ordinal);
        Assert.Contains("Unauthorized", result, StringComparison.Ordinal);
        Assert.Contains("MapGroup", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateEndpoints_CustomQueryParamAndHeaderName()
    {
        // Arrange
        var config = new HealthCheckConfig
        {
            Enabled = true,
            Security = "apiKey",
            ApiKeyQueryParameterName = "health-token",
            ApiKeyHeaderName = "X-Probe-Key",
        };

        // Act
        var result = HealthCheckExtractor.GenerateEndpoints("TestApi", config);

        // Assert
        Assert.Contains("health-token", result, StringComparison.Ordinal);
        Assert.Contains("X-Probe-Key", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateEndpoints_CustomPath_Respected()
    {
        // Arrange
        var config = new HealthCheckConfig
        {
            Enabled = true,
            Path = "/healthz",
        };

        // Act
        var result = HealthCheckExtractor.GenerateEndpoints("TestApi", config);

        // Assert
        Assert.Contains("/healthz", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\"/health\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateEndpoints_CorrectNamespace()
    {
        // Arrange
        var config = new HealthCheckConfig { Enabled = true };

        // Act
        var result = HealthCheckExtractor.GenerateEndpoints("MyCompany.Api", config);

        // Assert
        Assert.Contains("namespace MyCompany.Api.Generated.Health;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateServiceExtensions_ProducesAddHealthChecks()
    {
        // Act
        var result = HealthCheckExtractor.GenerateServiceExtensions("TestApi");

        // Assert
        Assert.Contains("AddApiHealthChecks", result, StringComparison.Ordinal);
        Assert.Contains("AddHealthChecks", result, StringComparison.Ordinal);
        Assert.Contains("IServiceCollection", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateServiceExtensions_CorrectNamespace()
    {
        // Act
        var result = HealthCheckExtractor.GenerateServiceExtensions("MyCompany.Api");

        // Assert
        Assert.Contains("namespace MyCompany.Api.Generated.Health;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateEndpoints_SecurityApiKey_PassthroughWhenNoKeyConfigured()
    {
        // Arrange
        var config = new HealthCheckConfig
        {
            Enabled = true,
            Security = "apiKey",
        };

        // Act
        var result = HealthCheckExtractor.GenerateEndpoints("TestApi", config);

        // Assert — filter checks IsNullOrEmpty so no key = passthrough
        Assert.Contains("!string.IsNullOrEmpty(expectedKey)", result, StringComparison.Ordinal);
    }
}