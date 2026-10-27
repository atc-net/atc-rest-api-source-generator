namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// <c>ProblemDetails.ToDisplayText()</c> of the EndpointPerOperation client: a problem's text for people - the field
/// messages of a validation problem, else <c>detail</c>, else a <c>title</c> that says more than the status. A problem
/// that only names its status (a title that is the status's reason phrase, or none) has no text: the caller words that
/// itself, in its own language.
/// </summary>
public sealed class ProblemDisplayTextTests
{
    private const string ProbeSource = """"
        namespace ErrorBodies.RuntimeTest;

        public static class DisplayTextProbe
        {
            public static string?[] Run()
                =>
                [
                    new ErrorBodies.Generated.ValidationProblemDetails(
                        Title: "One or more validation errors occurred.",
                        Status: 400,
                        Errors: new() { ["name"] = ["Name is required.", "Name is required."], ["age"] = ["Age must be at least 18."] }).ToDisplayText(),
                    new ErrorBodies.Generated.ValidationProblemDetails(Title: "Bad", Status: 400, Detail: "The request is invalid.").ToDisplayText(),
                    new ErrorBodies.Generated.ProblemDetails(Title: "Conflict", Status: 409, Detail: "The camera is in use.").ToDisplayText(),
                    new ErrorBodies.Generated.ProblemDetails(Title: "The camera is offline", Status: 503).ToDisplayText(),
                    new ErrorBodies.Generated.ProblemDetails(Title: "Service Unavailable", Status: 503).ToDisplayText(),
                    ErrorBodies.Generated.ProblemDetailsFactory.Create(System.Net.HttpStatusCode.BadGateway).ToDisplayText(),
                    new ErrorBodies.Generated.ProblemDetails(Status: 500).ToDisplayText(),
                    new ErrorBodies.Generated.ProblemDetails(Detail: "  ").ToDisplayText(),
                ];
        }
        """";

    [Fact]
    public void ToDisplayText_GivesTheProblemsTextForPeople_OrNullForAStatusAlone()
    {
        // Arrange
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"));
        sources.Add(("DisplayTextProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        // Act
        var texts = (string?[])assembly.GetType("ErrorBodies.RuntimeTest.DisplayTextProbe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, null)!;

        // Assert
        Assert.Equal(
            [
                "Name is required." + Environment.NewLine + "Age must be at least 18.",
                "The request is invalid.",
                "The camera is in use.",
                "The camera is offline",
                null,
                null,
                null,
                null,
            ],
            texts);
    }
}