namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Generated code compiles beside the global usings a host project commonly has: a name the generated code writes
/// unqualified must not be ambiguous with a type of the same name in those namespaces - Microsoft.OpenApi has a
/// <c>DisplayAttribute</c>, FluentValidation and ASP.NET Core MVC have types named like validation helpers.
/// </summary>
public sealed class ConsumerGlobalUsingsTests
{
    // Every validation attribute the server writes, on members whose wire name differs from the C# name (so each gets
    // its [Display(Name)] too): a model's properties and an operation's query and header parameters
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Contoso
          version: 1.0.0
        paths:
          /people:
            post:
              operationId: createPerson
              parameters:
                - name: pageSize
                  in: query
                  required: true
                  schema: { type: integer, format: int32, minimum: 1, maximum: 100 }
                - name: X-Tenant
                  in: header
                  required: true
                  schema: { type: string, minLength: 2, maxLength: 20, pattern: '^[a-z]+$' }
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Person'
              responses:
                '201':
                  description: Created
        components:
          schemas:
            Person:
              title: Person
              type: object
              required: [firstName, emailAddress]
              properties:
                firstName: { type: string, minLength: 1, maxLength: 50 }
                emailAddress: { type: string, format: email }
                homePage: { type: string, format: uri }
                shoeSize: { type: number, minimum: 30, maximum: 50 }
                nickNames: { type: array, minItems: 1, maxItems: 3, items: { type: string } }
                postCode: { type: string, pattern: '^[0-9]{4}$' }
        """;

    private const string HostGlobalUsings = """
        global using FluentValidation;
        global using Microsoft.AspNetCore.Mvc;
        global using Microsoft.OpenApi;
        """;

    [Theory]
    [InlineData(".atc-rest-api-server", "{}")]
    [InlineData(".atc-rest-api-client", """{ "generationMode": "EndpointPerOperation" }""")]
    [InlineData(".atc-rest-api-client", """{ "generationMode": "TypedClient" }""")]
    public void GeneratedCode_CompilesBesideTheHostsCommonGlobalUsings(
        string markerFile,
        string marker)
    {
        IIncrementalGenerator generator = markerFile == ".atc-rest-api-server" ? new ApiServerGenerator() : new ApiClientGenerator();
        var sources = ErrorResponseBodyRuntimeTests.Generate(generator, markerFile, marker, includeMinimalApi: true, Yaml);
        Assert.Contains(sources, s => s.Source.Contains("Display(Name", StringComparison.Ordinal) || markerFile != ".atc-rest-api-server");
        sources.Add(("HostGlobalUsings.g.cs", HostGlobalUsings));

        var errors = CompilationVerificationHarness.CreateCompilation(sources, includeMinimalApi: true)
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Location.SourceTree?.FilePath}: {d.GetMessage(CultureInfo.InvariantCulture)}")
            .ToList();

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }
}