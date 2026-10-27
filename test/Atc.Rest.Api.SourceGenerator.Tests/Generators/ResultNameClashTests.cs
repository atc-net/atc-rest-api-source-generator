namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// An operation's result is <c>{Operation}Result</c>. The endpoint definitions import
/// <c>Microsoft.AspNetCore.Mvc</c>, which has a result type for many verbs an operation may be named after
/// (<c>SignInResult</c>, <c>RedirectResult</c>, <c>FileResult</c>, ...): the generated code compiles anyway.
/// </summary>
public class ResultNameClashTests
{
    [Theory]
    [InlineData("signIn")]
    [InlineData("signOut")]
    [InlineData("challenge")]
    [InlineData("forbid")]
    [InlineData("redirect")]
    [InlineData("file")]
    [InlineData("content")]
    [InlineData("json")]
    [InlineData("ok")]
    [InlineData("notFound")]
    [InlineData("accepted")]
    [InlineData("created")]
    [InlineData("noContent")]
    [InlineData("badRequest")]
    [InlineData("conflict")]
    [InlineData("unauthorized")]
    [InlineData("statusCode")]
    [InlineData("object")]
    [InlineData("empty")]
    [InlineData("localRedirect")]
    public void Server_AnOperationNamedLikeAnMvcResult_Compiles(
        string operationId)
    {
        // Arrange
        var yaml = $$"""
            openapi: 3.0.0
            info:
              title: Auth API
              version: 1.0.0
            paths:
              /auth/{{operationId}}:
                post:
                  operationId: {{operationId}}
                  responses:
                    '200':
                      description: OK
                    '400':
                      description: Bad request
            """;

        // Act
        var withPackage = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, yaml);
        var withoutPackage = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", "{}", includeMinimalApi: false, yaml);

        // Assert
        var errors = CompilationVerificationHarness.CompileGeneratedSources(withPackage, includeMinimalApi: true)
            .Concat(CompilationVerificationHarness.CompileGeneratedSources(withoutPackage))
            .ToList();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }
}