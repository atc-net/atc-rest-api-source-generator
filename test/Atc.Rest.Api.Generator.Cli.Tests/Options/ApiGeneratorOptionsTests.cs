namespace Atc.Rest.Api.Generator.Cli.Tests.Options;

public class ApiGeneratorOptionsTests
{
    [Fact]
    public void ToClientConfig_CopiesGeneralAndClientSettings()
    {
        var options = new ApiGeneratorOptions
        {
            General = { ValidateSpecificationStrategy = ValidateSpecificationStrategy.Standard, IncludeDeprecated = true },
            Client =
            {
                Namespace = "Demo.Client",
                GenerationMode = GenerationModeType.EndpointPerOperation,
                ClientSuffix = "Gateway",
                ClientGranularity = ClientGranularityType.Single,
                ClientName = "DemoGateway",
                GenerateOAuthTokenManagement = false,
                ExtensibleEnums = false,
            },
        };

        var config = options.ToClientConfig();

        Assert.True(config.Generate);
        Assert.Equal(ValidateSpecificationStrategy.Standard, config.ValidateSpecificationStrategy);
        Assert.True(config.IncludeDeprecated);
        Assert.Equal("Demo.Client", config.Namespace);
        Assert.Equal(GenerationModeType.EndpointPerOperation, config.GenerationMode);
        Assert.Equal("Gateway", config.ClientSuffix);
        Assert.Equal(ClientGranularityType.Single, config.ClientGranularity);
        Assert.Equal("DemoGateway", config.ClientName);
        Assert.False(config.GenerateOAuthTokenManagement);
        Assert.False(config.ExtensibleEnums);
    }

    [Fact]
    public void ToServerConfig_CopiesGeneralAndServerSettings()
    {
        var options = new ApiGeneratorOptions
        {
            General = { RemoveNamespaceGroupSeparatorInGlobalUsings = true },
            Server =
            {
                Namespace = "Demo.Api",
                SubFolderStrategy = SubFolderStrategyType.OpenApiTag,
                VersioningStrategy = VersioningStrategyType.UrlSegment,
                DefaultApiVersion = "2.0",
                VersionHeaderName = "X-Version",
                ReportApiVersions = false,
            },
        };

        var config = options.ToServerConfig();

        Assert.True(config.Generate);
        Assert.True(config.RemoveNamespaceGroupSeparatorInGlobalUsings);
        Assert.Equal("Demo.Api", config.Namespace);
        Assert.Equal(SubFolderStrategyType.OpenApiTag, config.SubFolderStrategy);
        Assert.Equal(VersioningStrategyType.UrlSegment, config.VersioningStrategy);
        Assert.Equal("2.0", config.DefaultApiVersion);
        Assert.Equal("X-Version", config.VersionHeaderName);
        Assert.False(config.ReportApiVersions);
    }

    [Fact]
    public void ToServerDomainConfig_CopiesGeneralAndDomainSettings()
    {
        var options = new ApiGeneratorOptions
        {
            General = { IncludeDeprecated = true },
            Server =
            {
                Domain =
                {
                    GenerateHandlersOutput = "Handlers",
                    SubFolderStrategy = SubFolderStrategyType.FirstPathSegment,
                    HandlerSuffix = "Service",
                    StubImplementation = "error-501",
                },
            },
        };

        var config = options.ToServerDomainConfig();

        Assert.True(config.Generate);
        Assert.True(config.IncludeDeprecated);
        Assert.Equal("Handlers", config.GenerateHandlersOutput);
        Assert.Equal(SubFolderStrategyType.FirstPathSegment, config.SubFolderStrategy);
        Assert.Equal("Service", config.HandlerSuffix);
        Assert.Equal("error-501", config.StubImplementation);
    }
}