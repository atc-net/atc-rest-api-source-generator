namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// A marker file is read leniently - a misspelled setting is ignored, an unknown value is read as the setting's default - so the
/// inspector names both, for a diagnostic.
/// </summary>
public sealed class MarkerConfigInspectorTests
{
    [Fact]
    public void FindProblems_AValidMarker_HasNone()
    {
        const string json = """
            {
              "$schema": "https://example.com/atc-rest-api-server.schema.json",
              "Namespace": "Contoso.Api.Contracts",
              "validateSpecificationStrategy": "Strict",
              "useMinimalApiPackage": true,
              "useValidationFilter": "enabled",
              "subFolderStrategy": "first-path-segment",
              "includePaths": [ "/pets" ],
              "healthChecks": { "enabled": true, "security": "apiKey" },
              "multiPartConfiguration": { "discovery": "explicit", "parts": [ "Pets.yaml" ], "pathsMergeStrategy": "LastWins" }
            }
            """;

        Assert.Empty(MarkerConfigInspector.FindProblems(json, typeof(ServerConfig)));
    }

    [Fact]
    public void FindProblems_AMisspelledSetting_IsNamed()
    {
        var problems = MarkerConfigInspector.FindProblems("""{ "validateSpecificationStrategyy": "Strict" }""", typeof(ServerConfig));

        Assert.Equal(["'validateSpecificationStrategyy' is not a setting of this marker file and is ignored"], problems);
    }

    [Fact]
    public void FindProblems_ASettingOfAnotherMarker_IsNamed()
    {
        var problems = MarkerConfigInspector.FindProblems("""{ "subFolderStrategy": "None" }""", typeof(ClientConfig));

        Assert.Equal(["'subFolderStrategy' is not a setting of this marker file and is ignored"], problems);
    }

    [Fact]
    public void FindProblems_ANestedSetting_IsNamedWithItsPath()
    {
        const string json = """
            {
              "healthChecks": { "pathh": "/health" },
              "multiPartConfiguration": { "tagsMergeStrategy": "AppendUnique" }
            }
            """;

        var problems = MarkerConfigInspector.FindProblems(json, typeof(ServerConfig));

        Assert.Equal(
            [
                "'healthChecks.pathh' is not a setting of this marker file and is ignored",
                "'multiPartConfiguration.tagsMergeStrategy' is not a setting of this marker file and is ignored",
            ],
            problems);
    }

    [Theory]
    [InlineData("""{ "validateSpecificationStrategy": "Minimal" }""", "'validateSpecificationStrategy' has the unknown value \"Minimal\" (expected none, standard or strict), so strict is used")]
    [InlineData("""{ "useMinimalApiPackage": "yes" }""", "'useMinimalApiPackage' has the unknown value \"yes\" (expected auto, enabled, disabled, true or false), so auto is used")]
    [InlineData("""{ "subFolderStrategy": 1 }""", "'subFolderStrategy' has the unknown value 1 (expected None, FirstPathSegment or OpenApiTag), so FirstPathSegment is used")]
    [InlineData("""{ "multiPartConfiguration": { "pathsMergeStrategy": "Merge" } }""", "'multiPartConfiguration.pathsMergeStrategy' has the unknown value \"Merge\" (expected ErrorOnDuplicate, MergeIfIdentical, AppendUnique, FirstWins or LastWins), so ErrorOnDuplicate is used")]
    [InlineData("""{ "multiPartConfiguration": { "parametersMergeStrategy": "Merge" } }""", "'multiPartConfiguration.parametersMergeStrategy' has the unknown value \"Merge\" (expected ErrorOnDuplicate, MergeIfIdentical, AppendUnique, FirstWins or LastWins), so MergeIfIdentical is used")]
    [InlineData("""{ "healthChecks": { "security": "bearer" } }""", "'healthChecks.security' has the unknown value \"bearer\" (expected none or apiKey), so none is used")]
    public void FindProblems_AnUnknownValue_IsNamedWithWhatIsUsed(
        string json,
        string expected)
    {
        Assert.Equal([expected], MarkerConfigInspector.FindProblems(json, typeof(ServerConfig)));
    }

    [Theory]
    [InlineData("""{ "stubImplementation": "error-501" }""", null)]
    [InlineData("""{ "stubImplementation": "Default-Value" }""", null)]
    [InlineData("""{ "stubImplementation": "error-500" }""", "'stubImplementation' has the unknown value \"error-500\" (expected throw-not-implemented, error-501 or default-value), so throw-not-implemented is used")]
    public void FindProblems_AChoiceOfStrings_IsChecked(
        string json,
        string? expected)
    {
        var problems = MarkerConfigInspector.FindProblems(json, typeof(ServerDomainConfig));

        Assert.Equal(expected is null ? [] : [expected], problems);
    }

    [Fact]
    public void FindProblems_TheKeysOfACustomErrorSchema_AreNames_AndItsPropertiesAreChecked()
    {
        const string json = """
            {
              "errorResponseFormat": "custom",
              "customErrorResponseModel": {
                "name": "ContosoError",
                "schema": {
                  "traceId": { "dataType": "string?" },
                  "code": { "dataType": "int", "descriptionn": "The error code." }
                }
              }
            }
            """;

        var problems = MarkerConfigInspector.FindProblems(json, typeof(ClientConfig));

        Assert.Equal(["'customErrorResponseModel.schema.code.descriptionn' is not a setting of this marker file and is ignored"], problems);
    }

    [Fact]
    public void Read_RuleSeverities_KeepsTheKnownRulesWithAKnownSeverity_AndNamesTheOthers()
    {
        const string json = """
            {
              "ruleSeverities": {
                "ATC_API_SCH022": "warning",
                "ATC_API_OPR003": "None",
                "ATC_API_NAM005": "loud",
                "ATC_API_XYZ999": "error"
              }
            }
            """;

        var config = MarkerConfigInspector.Read<ServerConfig>(json, out var problems);

        Assert.Equal(2, config.RuleSeverities!.Count);
        Assert.Equal("warning", config.RuleSeverities["ATC_API_SCH022"]);
        Assert.Equal("None", config.RuleSeverities["ATC_API_OPR003"]);
        Assert.Equal(
            [
                "'ruleSeverities.ATC_API_NAM005' has the unknown value \"loud\" (expected default, none, silent, suggestion, warning or error), so the rule keeps its severity",
                "'ruleSeverities.ATC_API_XYZ999' is not a rule of the generator and is ignored",
            ],
            problems);
    }

    [Fact]
    public void Read_AnUnknownValue_IsTheDefaultOfTheSetting()
    {
        const string json = """
            {
              "namespace": "Contoso.Api.Contracts",
              "validateSpecificationStrategy": "Minimal",
              "subFolderStrategy": "by-tag",
              "useMinimalApiPackage": "yes",
              "healthChecks": { "enabled": true, "security": "bearer" },
              "multiPartConfiguration": { "pathsMergeStrategy": "Merge", "parametersMergeStrategy": "Merge" }
            }
            """;

        var config = MarkerConfigInspector.Read<ServerConfig>(json, out var problems);

        Assert.Equal("Contoso.Api.Contracts", config.Namespace);
        Assert.Equal(ValidateSpecificationStrategy.Strict, config.ValidateSpecificationStrategy);
        Assert.Equal(SubFolderStrategyType.FirstPathSegment, config.SubFolderStrategy);
        Assert.Equal(MinimalApiPackageMode.Auto, config.UseMinimalApiPackage);
        Assert.True(config.HealthChecks!.Enabled);
        Assert.Equal("none", config.HealthChecks.Security);
        Assert.Equal(MergeStrategy.ErrorOnDuplicate, config.MultiPartConfiguration!.PathsMergeStrategy);
        Assert.Equal(MergeStrategy.MergeIfIdentical, config.MultiPartConfiguration.ParametersMergeStrategy);
        Assert.Equal(6, problems.Count);
    }

    [Fact]
    public void Read_AKnownValue_IsKept()
    {
        const string json = """
            {
              "validateSpecificationStrategy": "none",
              "subFolderStrategy": "None",
              "healthChecks": { "security": "apiKey" },
              "multiPartConfiguration": { "parametersMergeStrategy": "LastWins" }
            }
            """;

        var config = MarkerConfigInspector.Read<ServerConfig>(json, out var problems);

        Assert.Empty(problems);
        Assert.Equal(ValidateSpecificationStrategy.None, config.ValidateSpecificationStrategy);
        Assert.Equal(SubFolderStrategyType.None, config.SubFolderStrategy);
        Assert.Equal("apiKey", config.HealthChecks!.Security);
        Assert.Equal(MergeStrategy.LastWins, config.MultiPartConfiguration!.ParametersMergeStrategy);
    }

    [Fact]
    public void Read_AnUnknownStubImplementation_IsTheDefault()
    {
        var config = MarkerConfigInspector.Read<ServerDomainConfig>("""{ "stubImplementation": "error-500" }""", out _);

        Assert.Equal("throw-not-implemented", config.StubImplementation);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("[]")]
    [InlineData("")]
    public void FindProblems_ContentThatIsNotAnObject_IsLeftToTheParseError(
        string json)
    {
        Assert.Empty(MarkerConfigInspector.FindProblems(json, typeof(ServerConfig)));
    }
}