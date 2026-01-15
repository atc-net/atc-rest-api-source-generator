namespace Atc.Rest.Api.Generator.Tests.Configurations;

public class ConfigurationTests
{
    // ========== ServerConfig Tests ==========
    [Fact]
    public void ServerConfig_DefaultValues_AreCorrect()
    {
        var config = new ServerConfig();

        Assert.True(config.Generate);
        Assert.Equal(ValidateSpecificationStrategy.Strict, config.ValidateSpecificationStrategy);
        Assert.False(config.IncludeDeprecated);
        Assert.Null(config.Namespace);
        Assert.Equal(SubFolderStrategyType.FirstPathSegment, config.SubFolderStrategy);
        Assert.Equal(MinimalApiPackageMode.Auto, config.UseMinimalApiPackage);
    }

    [Fact]
    public void ServerConfig_CanDeserializeFromJson()
    {
        var json = """
            {
                "generate": true,
                "validateSpecificationStrategy": "Standard",
                "includeDeprecated": true,
                "namespace": "MyApi",
                "subFolderStrategy": "OpenApiTag"
            }
            """;

        var config = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.True(config!.Generate);
        Assert.Equal(ValidateSpecificationStrategy.Standard, config.ValidateSpecificationStrategy);
        Assert.True(config.IncludeDeprecated);
        Assert.Equal("MyApi", config.Namespace);
        Assert.Equal(SubFolderStrategyType.OpenApiTag, config.SubFolderStrategy);
    }

    [Fact]
    public void ServerConfig_CanDeserializeKebabCaseEnums()
    {
        var json = """
            {
                "validateSpecificationStrategy": "standard",
                "subFolderStrategy": "first-path-segment"
            }
            """;

        var config = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(ValidateSpecificationStrategy.Standard, config!.ValidateSpecificationStrategy);
        Assert.Equal(SubFolderStrategyType.FirstPathSegment, config.SubFolderStrategy);
    }

    // ========== ClientConfig Tests ==========
    [Fact]
    public void ClientConfig_DefaultValues_AreCorrect()
    {
        var config = new ClientConfig();

        Assert.True(config.Generate);
        Assert.Equal(ValidateSpecificationStrategy.Strict, config.ValidateSpecificationStrategy);
        Assert.False(config.IncludeDeprecated);
        Assert.Null(config.Namespace);
        Assert.Equal(GenerationModeType.TypedClient, config.GenerationMode);
        Assert.Equal("Client", config.ClientSuffix);
        Assert.Equal(ErrorResponseFormatType.ProblemDetails, config.ErrorResponseFormat);
        Assert.Null(config.CustomErrorResponseModel);
    }

    [Fact]
    public void ClientConfig_CanDeserializeFromJson()
    {
        var json = """
            {
                "generate": false,
                "validateSpecificationStrategy": "None",
                "generationMode": "EndpointPerOperation",
                "clientSuffix": "ApiClient"
            }
            """;

        var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.False(config!.Generate);
        Assert.Equal(ValidateSpecificationStrategy.None, config.ValidateSpecificationStrategy);
        Assert.Equal(GenerationModeType.EndpointPerOperation, config.GenerationMode);
        Assert.Equal("ApiClient", config.ClientSuffix);
    }

    [Fact]
    public void ClientConfig_CanDeserializeKebabCaseEnums()
    {
        var json = """
            {
                "generationMode": "endpoint-per-operation"
            }
            """;

        var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(GenerationModeType.EndpointPerOperation, config!.GenerationMode);
    }

    // ========== ServerDomainConfig Tests ==========
    [Fact]
    public void ServerDomainConfig_DefaultValues_AreCorrect()
    {
        var config = new ServerDomainConfig();

        Assert.True(config.Generate);
        Assert.Equal(ValidateSpecificationStrategy.Strict, config.ValidateSpecificationStrategy);
        Assert.False(config.IncludeDeprecated);
        Assert.Null(config.Namespace);
        Assert.Null(config.ContractsNamespace);
        Assert.Equal("ApiHandlers", config.GenerateHandlersOutput);
        Assert.Equal(SubFolderStrategyType.None, config.SubFolderStrategy);
        Assert.Equal("Handler", config.HandlerSuffix);
        Assert.Equal("throw-not-implemented", config.StubImplementation);
    }

    [Fact]
    public void ServerDomainConfig_CanDeserializeFromJson()
    {
        var json = """
            {
                "generate": true,
                "generateHandlersOutput": "Handlers",
                "subFolderStrategy": "first-path-segment",
                "handlerSuffix": "RequestHandler",
                "stubImplementation": "error-501"
            }
            """;

        var config = JsonSerializer.Deserialize<ServerDomainConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.True(config!.Generate);
        Assert.Equal("Handlers", config.GenerateHandlersOutput);
        Assert.Equal(SubFolderStrategyType.FirstPathSegment, config.SubFolderStrategy);
        Assert.Equal("RequestHandler", config.HandlerSuffix);
        Assert.Equal("error-501", config.StubImplementation);
    }

    [Fact]
    public void ServerDomainConfig_CanDeserializeWithContractsNamespace()
    {
        var json = """
            {
                "namespace": "MyApp.Api.Domain",
                "contractsNamespace": "Contoso.IoT.Api.Contracts",
                "generateHandlersOutput": "ApiHandlers",
                "subFolderStrategy": "first-path-segment"
            }
            """;

        var config = JsonSerializer.Deserialize<ServerDomainConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal("MyApp.Api.Domain", config!.Namespace);
        Assert.Equal("Contoso.IoT.Api.Contracts", config.ContractsNamespace);
        Assert.Equal("ApiHandlers", config.GenerateHandlersOutput);
        Assert.Equal(SubFolderStrategyType.FirstPathSegment, config.SubFolderStrategy);
    }

    // ========== ValidateSpecificationStrategy Enum Tests ==========
    [Theory]
    [InlineData("None", ValidateSpecificationStrategy.None)]
    [InlineData("none", ValidateSpecificationStrategy.None)]
    [InlineData("Standard", ValidateSpecificationStrategy.Standard)]
    [InlineData("standard", ValidateSpecificationStrategy.Standard)]
    [InlineData("Strict", ValidateSpecificationStrategy.Strict)]
    [InlineData("strict", ValidateSpecificationStrategy.Strict)]
    public void ValidateSpecificationStrategy_CanDeserializeAllValues(
        string value,
        ValidateSpecificationStrategy expected)
    {
        var json = $$$"""{"validateSpecificationStrategy": "{{{value}}}"}""";
        var config = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(expected, config!.ValidateSpecificationStrategy);
    }

    // ========== SubFolderStrategyType Enum Tests ==========
    [Theory]
    [InlineData("None", SubFolderStrategyType.None)]
    [InlineData("none", SubFolderStrategyType.None)]
    [InlineData("FirstPathSegment", SubFolderStrategyType.FirstPathSegment)]
    [InlineData("first-path-segment", SubFolderStrategyType.FirstPathSegment)]
    [InlineData("OpenApiTag", SubFolderStrategyType.OpenApiTag)]
    [InlineData("openapi-tag", SubFolderStrategyType.OpenApiTag)]
    public void SubFolderStrategyType_CanDeserializeAllValues(
        string value,
        SubFolderStrategyType expected)
    {
        var json = $$$"""{"subFolderStrategy": "{{{value}}}"}""";
        var config = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(expected, config!.SubFolderStrategy);
    }

    // ========== GenerationModeType Enum Tests ==========
    [Theory]
    [InlineData("TypedClient", GenerationModeType.TypedClient)]
    [InlineData("typed-client", GenerationModeType.TypedClient)]
    [InlineData("EndpointPerOperation", GenerationModeType.EndpointPerOperation)]
    [InlineData("endpoint-per-operation", GenerationModeType.EndpointPerOperation)]
    public void GenerationModeType_CanDeserializeAllValues(
        string value,
        GenerationModeType expected)
    {
        var json = $$$"""{"generationMode": "{{{value}}}"}""";
        var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(expected, config!.GenerationMode);
    }

    // ========== ErrorResponseFormatType Enum Tests ==========
    [Theory]
    [InlineData("ProblemDetails", ErrorResponseFormatType.ProblemDetails)]
    [InlineData("problemdetails", ErrorResponseFormatType.ProblemDetails)]
    [InlineData("problem-details", ErrorResponseFormatType.ProblemDetails)]
    [InlineData("PlainText", ErrorResponseFormatType.PlainText)]
    [InlineData("plaintext", ErrorResponseFormatType.PlainText)]
    [InlineData("plain-text", ErrorResponseFormatType.PlainText)]
    [InlineData("PlainTextOnly", ErrorResponseFormatType.PlainTextOnly)]
    [InlineData("plaintextonly", ErrorResponseFormatType.PlainTextOnly)]
    [InlineData("plain-text-only", ErrorResponseFormatType.PlainTextOnly)]
    [InlineData("Custom", ErrorResponseFormatType.Custom)]
    [InlineData("custom", ErrorResponseFormatType.Custom)]
    public void ErrorResponseFormatType_CanDeserializeAllValues(
        string value,
        ErrorResponseFormatType expected)
    {
        var json = $$$"""{"errorResponseFormat": "{{{value}}}"}""";
        var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(expected, config!.ErrorResponseFormat);
    }

    [Fact]
    public void ErrorResponseFormatType_UnknownValue_DefaultsToProblemDetails()
    {
        var json = """{"errorResponseFormat": "unknown"}""";
        var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(ErrorResponseFormatType.ProblemDetails, config!.ErrorResponseFormat);
    }

    [Fact]
    public void ClientConfig_CanDeserializeWithErrorResponseFormat()
    {
        var json = """
            {
                "generationMode": "EndpointPerOperation",
                "errorResponseFormat": "PlainText"
            }
            """;

        var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(GenerationModeType.EndpointPerOperation, config!.GenerationMode);
        Assert.Equal(ErrorResponseFormatType.PlainText, config.ErrorResponseFormat);
    }

    [Fact]
    public void ClientConfig_CanDeserializeWithCustomErrorResponseModel()
    {
        var json = """
            {
                "generationMode": "EndpointPerOperation",
                "errorResponseFormat": "Custom",
                "customErrorResponseModel": {
                    "name": "ApiError",
                    "description": "Custom error response",
                    "schema": {
                        "code": { "dataType": "string?" },
                        "message": { "dataType": "string?" }
                    }
                }
            }
            """;

        var config = JsonSerializer.Deserialize<ClientConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(ErrorResponseFormatType.Custom, config!.ErrorResponseFormat);
        Assert.NotNull(config.CustomErrorResponseModel);
        Assert.Equal("ApiError", config.CustomErrorResponseModel!.Name);
        Assert.Equal("Custom error response", config.CustomErrorResponseModel.Description);
        Assert.NotNull(config.CustomErrorResponseModel.Schema);
        Assert.Equal(2, config.CustomErrorResponseModel.Schema!.Count);
    }

    // ========== MinimalApiPackageMode Enum Tests ==========
    [Theory]
    [InlineData("Auto", MinimalApiPackageMode.Auto)]
    [InlineData("auto", MinimalApiPackageMode.Auto)]
    [InlineData("Enabled", MinimalApiPackageMode.Enabled)]
    [InlineData("enabled", MinimalApiPackageMode.Enabled)]
    [InlineData("Disabled", MinimalApiPackageMode.Disabled)]
    [InlineData("disabled", MinimalApiPackageMode.Disabled)]
    public void MinimalApiPackageMode_CanDeserializeStringValues(
        string value,
        MinimalApiPackageMode expected)
    {
        var json = $$$"""{"useMinimalApiPackage": "{{{value}}}"}""";
        var config = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(expected, config!.UseMinimalApiPackage);
    }

    [Fact]
    public void MinimalApiPackageMode_CanDeserializeTrue()
    {
        var json = """{"useMinimalApiPackage": true}""";
        var config = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(MinimalApiPackageMode.Enabled, config!.UseMinimalApiPackage);
    }

    [Fact]
    public void MinimalApiPackageMode_CanDeserializeFalse()
    {
        var json = """{"useMinimalApiPackage": false}""";
        var config = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(MinimalApiPackageMode.Disabled, config!.UseMinimalApiPackage);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void MinimalApiPackageMode_CanDeserializeTrueString(string value)
    {
        var json = $$$"""{"useMinimalApiPackage": "{{{value}}}"}""";
        var config = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(MinimalApiPackageMode.Enabled, config!.UseMinimalApiPackage);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    public void MinimalApiPackageMode_CanDeserializeFalseString(string value)
    {
        var json = $$$"""{"useMinimalApiPackage": "{{{value}}}"}""";
        var config = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(MinimalApiPackageMode.Disabled, config!.UseMinimalApiPackage);
    }

    [Fact]
    public void MinimalApiPackageMode_UnknownString_DefaultsToAuto()
    {
        var json = """{"useMinimalApiPackage": "unknown"}""";
        var config = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.Equal(MinimalApiPackageMode.Auto, config!.UseMinimalApiPackage);
    }

    [Fact]
    public void ServerConfig_CanDeserializeWithUseMinimalApiPackage()
    {
        var json = """
            {
                "generate": true,
                "validateSpecificationStrategy": "Standard",
                "useMinimalApiPackage": "enabled"
            }
            """;

        var config = JsonSerializer.Deserialize<ServerConfig>(json, JsonHelper.ConfigOptions);

        Assert.NotNull(config);
        Assert.True(config!.Generate);
        Assert.Equal(ValidateSpecificationStrategy.Standard, config.ValidateSpecificationStrategy);
        Assert.Equal(MinimalApiPackageMode.Enabled, config.UseMinimalApiPackage);
    }
}