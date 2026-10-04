namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// A marker file is read leniently - a misspelled setting is ignored, an unknown value is read as a fallback - so the
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
    [InlineData("""{ "validateSpecificationStrategy": "Minimal" }""", "'validateSpecificationStrategy' has the unknown value \"Minimal\" (expected none, standard or strict), so standard is used")]
    [InlineData("""{ "useMinimalApiPackage": "yes" }""", "'useMinimalApiPackage' has the unknown value \"yes\" (expected auto, enabled, disabled, true or false), so auto is used")]
    [InlineData("""{ "subFolderStrategy": 1 }""", "'subFolderStrategy' has the unknown value 1 (expected None, FirstPathSegment or OpenApiTag), so None is used")]
    [InlineData("""{ "multiPartConfiguration": { "pathsMergeStrategy": "Merge" } }""", "'multiPartConfiguration.pathsMergeStrategy' has the unknown value \"Merge\" (expected ErrorOnDuplicate, MergeIfIdentical, AppendUnique, FirstWins or LastWins), so ErrorOnDuplicate is used")]
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