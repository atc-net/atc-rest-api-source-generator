namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A rule about a path or an operation judges only the operations the marker's <c>includePaths</c> /
/// <c>excludePaths</c> keep: an operation left out is never generated, so what is wrong with it changes nothing in the
/// generated code - and an error there must not stop the generation of the rest. Each fixture keeps <c>/things</c>
/// (clean for the rule) and trips the rule on <c>/admin/things</c>.
/// </summary>
public class PathFilteredValidationTests
{
    private const string FilePath = "things.yaml";

    // A parameter whose value no server can bind (ATC_API_OPR037: an error to a server)
    private const string UnbindableAdminPath = """
          /admin/things:
            get:
              operationId: listAdminThings
              parameters:
                - name: filter
                  in: query
                  schema: { type: object, properties: { name: { type: string } } }
              responses:
                '200': { description: OK }
        """;

    private static readonly string[] AdminExcluded = ["/admin/**"];
    private static readonly string[] AdminIncluded = ["/admin/**"];

    public static TheoryData<string, ValidateSpecificationStrategy, string, string> Cases
        => new()
    {
        {
            "ATC_API_OPR037", ValidateSpecificationStrategy.None, "3.1.1", UnbindableAdminPath
        },
        {
            "ATC_API_OPR032", ValidateSpecificationStrategy.None, "3.1.1",
            """
              /admin/things:
                get:
                  operationId: listThings
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_OPR001", ValidateSpecificationStrategy.Standard, "3.1.1",
            """
              /admin/things:
                get:
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_SCH013", ValidateSpecificationStrategy.None, "3.1.1",
            """
              /admin/things:
                get:
                  operationId: listAdminThings
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/Missing' }
            """
        },
        {
            "ATC_API_OPR012", ValidateSpecificationStrategy.None, "3.1.1",
            """
              /admin/things/{thingId}:
                get:
                  operationId: getAdminThing
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_OPR034", ValidateSpecificationStrategy.None, "3.1.1",
            """
              /admin/things:
                get:
                  operationId: listAdminThings
                  responses:
                    '2XX': { description: OK }
            """
        },
        {
            "ATC_API_OPR039", ValidateSpecificationStrategy.None, "3.1.1",
            """
              /admin/things:
                head:
                  operationId: headAdminThings
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { type: string }
            """
        },
        {
            "ATC_API_OPR040", ValidateSpecificationStrategy.None, "3.1.1",
            """
              /admin/things:
                get:
                  operationId: listAdminThings
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { type: string }
                        application/json; charset=utf-8:
                          schema: { type: integer }
            """
        },
        {
            "ATC_API_OPR036", ValidateSpecificationStrategy.None, "3.2.0",
            """
              /admin/things:
                get:
                  operationId: listAdminThings
                  parameters:
                    - { name: filter, in: querystring, content: { application/x-www-form-urlencoded: { schema: { type: string } } } }
                    - { name: q, in: query, schema: { type: string } }
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_SEC012", ValidateSpecificationStrategy.None, "3.1.1",
            """
              /admin/things:
                get:
                  operationId: listAdminThings
                  security:
                    - missing: []
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_OPR033", ValidateSpecificationStrategy.None, "3.1.1",
            """
              /admin/things:
                get:
                  operationId: listAdminThings
                  parameters:
                    - name: q
                      in: query
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_SCH025", ValidateSpecificationStrategy.None, "3.1.1",
            """
              /admin/things:
                get:
                  operationId: listAdminThings
                  parameters:
                    - name: q
                      in: query
                      schema: { type: string, minLength: 5, maxLength: 1 }
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_PTH001", ValidateSpecificationStrategy.None, "3.1.1",
            """
              admin/things:
                get:
                  operationId: listAdminThings
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_STREAM002", ValidateSpecificationStrategy.Standard, "3.1.1",
            """
              /admin/things:
                post:
                  operationId: createAdminThings
                  requestBody:
                    content:
                      text/event-stream:
                        schema: { type: string }
                  responses:
                    '204': { description: No Content }
            """
        },
        {
            "ATC_API_RL003", ValidateSpecificationStrategy.Standard, "3.1.1",
            """
              /admin/things:
                get:
                  operationId: listAdminThings
                  x-ratelimit-policy: shared
                  x-ratelimit-permit-limit: 20
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_RL005", ValidateSpecificationStrategy.Standard, "3.1.1",
            """
              /admin/things:
                get:
                  operationId: listAdminThings
                  x-ratelimit-policy: admin
                  x-ratelimit-permit-limit: -5
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_RTY001", ValidateSpecificationStrategy.Standard, "3.1.1",
            """
              /admin/things:
                get:
                  operationId: listAdminThings
                  x-retry-max-attempts: 5
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_NAM001", ValidateSpecificationStrategy.Strict, "3.1.1",
            """
              /admin/things:
                get:
                  operationId: ListAdminThings
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_OPR014", ValidateSpecificationStrategy.Strict, "3.1.1",
            """
              /admin/things/{thingId}:
                get:
                  operationId: getAdminThing
                  parameters:
                    - { name: thingId, in: path, required: true, schema: { type: string } }
                  responses:
                    '200': { description: OK }
            """
        },
        {
            "ATC_API_CACHE001", ValidateSpecificationStrategy.Strict, "3.1.1",
            """
              /admin/things:
                post:
                  operationId: createAdminThing
                  x-cache-policy: AdminPolicy
                  x-cache-expiration-seconds: 60
                  responses:
                    '204': { description: No Content }
            """
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ARuleAboutAnOperation_JudgesOnlyTheOperationsThePathFilterKeeps(
        string ruleId,
        ValidateSpecificationStrategy strategy,
        string version,
        string adminPath)
    {
        var yaml = Document(version, adminPath);

        var unfiltered = SpecificationPipeline.ParseAndValidate(FilePath, yaml, strategy);
        var excluded = SpecificationPipeline.ParseAndValidate(FilePath, yaml, strategy, excludePaths: AdminExcluded);

        Assert.Contains(unfiltered.Diagnostics, d => d.RuleId == ruleId);
        Assert.DoesNotContain(excluded.Diagnostics, d => d.RuleId == ruleId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnUnbindableParameter_OnAnExcludedPath_DoesNotStopAServer_OnAKeptPathItStillDoes(
        bool kept)
    {
        var yaml = Document("3.1.1", UnbindableAdminPath);

        var result = kept
            ? SpecificationPipeline.ParseAndValidate(FilePath, yaml, ValidateSpecificationStrategy.Standard, parametersMustBind: true, includePaths: AdminIncluded)
            : SpecificationPipeline.ParseAndValidate(FilePath, yaml, ValidateSpecificationStrategy.Standard, parametersMustBind: true, excludePaths: AdminExcluded);

        if (kept)
        {
            Assert.Equal(DiagnosticSeverity.Error, Assert.Single(result.Diagnostics, d => d.RuleId == RuleIdentifiers.ParameterValueNotBindable).Severity);
            Assert.False(result.IsValid);
        }
        else
        {
            Assert.DoesNotContain(result.Diagnostics, d => d.RuleId == RuleIdentifiers.ParameterValueNotBindable);
            Assert.True(result.IsValid);
        }
    }

    /// <summary>
    /// The rules about the document - its components, servers, version - judge the whole document: the models are
    /// generated from every component schema whatever the filter keeps.
    /// </summary>
    [Fact]
    public void ARuleAboutAComponentSchema_JudgesItWhateverThePathFilterKeeps()
    {
        var yaml = Document(
            "3.1.1",
            """
              /admin/things:
                post:
                  operationId: createAdminThing
                  requestBody:
                    content:
                      application/json:
                        schema: { $ref: '#/components/schemas/AdminThing' }
                  responses:
                    '204': { description: No Content }
            """,
            """
                AdminThing:
                  type: object
                  properties:
                    name: { type: string }
            """);

        var excluded = SpecificationPipeline.ParseAndValidate(FilePath, yaml, ValidateSpecificationStrategy.Standard, excludePaths: AdminExcluded);

        var warning = Assert.Single(excluded.Diagnostics, d => d.RuleId == RuleIdentifiers.SchemaWithoutRequiredProperties);
        Assert.Contains("'AdminThing'", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The filtered document is read as the version it was parsed as: the rules that differ by version judge it alike.
    /// </summary>
    [Fact]
    public void TheFilteredDocument_KeepsTheSpecVersion()
    {
        Assert.True(OpenApiDocumentHelper.TryParseYaml(Document("3.2.0", UnbindableAdminPath), FilePath, out var document));

        var filtered = PathFilterHelper.WithFilteredPaths(document!, includePaths: null, excludePaths: AdminExcluded);

        Assert.Equal(OpenApiSpecVersion.OpenApi3_2, document!.SpecVersion);
        Assert.Equal(document.SpecVersion, filtered.SpecVersion);
        Assert.False(filtered.Paths.ContainsKey("/admin/things"));
    }

    /// <summary>
    /// A filter that keeps no operation does not make the document one without operations (ATC_API_PTH002 is about
    /// the document).
    /// </summary>
    [Fact]
    public void AFilterThatKeepsNothing_DoesNotReportADocumentWithoutOperations()
    {
        var yaml = Document("3.1.1", string.Empty);

        var result = SpecificationPipeline.ParseAndValidate(FilePath, yaml, ValidateSpecificationStrategy.Standard, excludePaths: ["/things"]);

        Assert.DoesNotContain(result.Diagnostics, d => d.RuleId == RuleIdentifiers.NoOperations);
    }

    private static string Document(
        string version,
        string adminPath,
        string extraSchemas = "")
        => $$"""
           openapi: {{version}}
           info:
             title: Things
             version: 1.0.0
           paths:
             /things:
               get:
                 operationId: listThings
                 x-ratelimit-policy: shared
                 x-ratelimit-permit-limit: 10
                 responses:
                   '200':
                     description: OK
                     content:
                       application/json:
                         schema:
                           type: array
                           items: { $ref: '#/components/schemas/Thing' }
           {{adminPath}}
           components:
             schemas:
               Thing:
                 type: object
                 required: [name]
                 properties:
                   name: { type: string }
           {{extraSchemas}}
           """;
}