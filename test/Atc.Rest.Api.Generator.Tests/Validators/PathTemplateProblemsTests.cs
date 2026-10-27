namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// The problems of a path template (ATC_API_PTH001), checked over plain strings: none for a well-formed template, and
/// for a broken one the problems in the order they are checked, the last one ending the checks.
/// </summary>
public sealed class PathTemplateProblemsTests
{
    [Theory]
    [InlineData("/pets")]
    [InlineData("/pets/{petId}")]
    [InlineData("/owners/{ownerId}/pets/{petId}")]
    public void PathTemplateProblems_AWellFormedTemplate_HasNone(string path)
        => Assert.Empty(OpenApiDocumentValidator.PathTemplateProblems(path));

    [Theory]
    [InlineData("/pets/{petId", "unbalanced braces")]
    [InlineData("pets", "does not start with '/'")]
    [InlineData("/pets//toys", "has an empty segment ('//')")]
    [InlineData("/pets/{id}/toys/{id}", "has the placeholder '{id}' more than once")]
    [InlineData("/pets/a b", "contains ' '")]
    public void PathTemplateProblems_ABrokenTemplate_EndsTheChecksWithItsProblem(
        string path,
        string expected)
    {
        var problems = OpenApiDocumentValidator.PathTemplateProblems(path);

        var last = problems[^1];
        Assert.True(last.EndsChecks);
        Assert.Contains(expected, last.Message, StringComparison.Ordinal);
        Assert.StartsWith($"Path '{path}' ", last.Message, StringComparison.Ordinal);
        Assert.NotEmpty(last.Suggestions);
    }

    [Fact]
    public void PathTemplateProblems_AnEmptyPlaceholder_IsReported_BeforeTheMalformedParameter()
    {
        var problems = OpenApiDocumentValidator.PathTemplateProblems("/pets/{}");

        Assert.Contains("empty parameter placeholder", problems[0].Message, StringComparison.Ordinal);
        Assert.False(problems[0].EndsChecks);
    }
}