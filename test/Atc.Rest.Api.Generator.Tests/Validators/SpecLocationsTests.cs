namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A diagnostic found at an operation, a path or a schema is put on that line of the specification - the method's key,
/// the path's, the schema's name - so the IDE takes you there, in YAML and JSON, in the file of a specification of
/// several files that declares it.
/// </summary>
public sealed class SpecLocationsTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Locations
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: OK
          '/things/{id}':
            parameters:
              - name: id
                in: path
                required: true
                schema:
                  type: string
            get:
              operationId: getThing
              responses:
                '200':
                  description: OK
            delete:
              operationId: deleteThing
              responses:
                '204':
                  description: Gone
        components:
          schemas:
            Thing:
              type: object
              properties:
                get:
                  type: string
        """;

    [Theory]
    [InlineData("operation:GET /things", 7, 5)]
    [InlineData("operation:GET /things/{id}", 19, 5)]
    [InlineData("operation:DELETE /things/{id}", 24, 5)]
    [InlineData("path:/things/{id}", 12, 3)]
    [InlineData("schema:Thing", 31, 5)]
    public void Apply_YamlScope_IsTheLineOfItsKey(
        string scope,
        int line,
        int column)
    {
        var diagnostics = new List<DiagnosticMessage> { Diagnostic(scope) };

        SpecLocations.Apply(diagnostics, [("Api.yaml", Yaml)]);

        Assert.Equal(("Api.yaml", line, column), (diagnostics[0].FilePath, diagnostics[0].LineNumber, diagnostics[0].ColumnNumber));
    }

    [Theory]
    [InlineData("operation:GET /things/{id}", 8)]
    [InlineData("schema:Thing", 13)]
    public void Apply_JsonScope_IsTheLineOfItsKey(
        string scope,
        int line)
    {
        const string json = """
            {
              "openapi": "3.0.3",
              "paths": {
                "/things": {
                  "get": { "operationId": "listThings" }
                },
                "/things/{id}": {
                  "get": { "operationId": "getThing" }
                }
              },
              "components": {
                "schemas": {
                  "Thing": { "type": "object" }
                }
              }
            }
            """;
        var diagnostics = new List<DiagnosticMessage> { Diagnostic(scope) };

        SpecLocations.Apply(diagnostics, [("Api.json", json)]);

        Assert.Equal(line, diagnostics[0].LineNumber);
    }

    [Fact]
    public void Apply_InAPartFile_IsThatFilesLine()
    {
        const string part = """
            paths:
              /orders:
                post:
                  operationId: createOrder
            """;
        var diagnostics = new List<DiagnosticMessage> { Diagnostic("operation:POST /orders") };

        SpecLocations.Apply(diagnostics, [("Api.yaml", Yaml), ("Api_Orders.yaml", part)]);

        Assert.Equal(("Api_Orders.yaml", 3), (diagnostics[0].FilePath, diagnostics[0].LineNumber));
    }

    [Theory]
    [InlineData("operation:PUT /things")]
    [InlineData("schema:Missing")]
    [InlineData(null)]
    public void Apply_APlaceThatIsNotFound_KeepsTheDiagnostic(string? scope)
    {
        var diagnostic = Diagnostic(scope);
        var diagnostics = new List<DiagnosticMessage> { diagnostic };

        SpecLocations.Apply(diagnostics, [("Api.yaml", Yaml)]);

        Assert.Equal(diagnostic, diagnostics[0]);
    }

    [Fact]
    public void Validate_AnOperationDiagnostic_IsOnTheOperationsLine()
    {
        // A 404 on a POST: an operation-level rule
        const string yaml = """
            openapi: 3.0.3
            info:
              title: Locations
              version: 1.0.0
            paths:
              /things:
                post:
                  operationId: createThing
                  responses:
                    '201':
                      description: Created
                    '404':
                      description: Not found
            """;
        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], "Api.yaml", yaml);

        var located = diagnostics.Where(d => d.Scope == "operation:POST /things").ToList();
        Assert.NotEmpty(located);
        Assert.All(located, d => Assert.Equal(7, d.LineNumber));
    }

    private static DiagnosticMessage Diagnostic(string? scope)
        => new("ATC_API_TEST", "A test diagnostic.", DiagnosticSeverity.Warning, "Api.yaml", Scope: scope);
}