namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptClientExtractorTests
{
    [Fact]
    public void Extract_TextPlainOperation_PassesResponseTypeText()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /reports/text:
                                get:
                                  operationId: getTextReport
                                  responses:
                                    '200':
                                      description: Plain text report
                                      content:
                                        text/plain:
                                          schema:
                                            type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null);

        var (_, content) = Assert.Single(clients);

        // Per-operation method must opt into text parsing AND surface the body as a string,
        // not a Blob (default for non-JSON responses).
        Assert.Contains("responseType: 'text'", content, StringComparison.Ordinal);
        Assert.Contains("Promise<ApiResult<string>>", content, StringComparison.Ordinal);
        Assert.Contains("this.api.request<string>('GET', '/reports/text'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_TextCsvOperation_PassesResponseTypeText()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /exports/csv:
                                get:
                                  operationId: exportCsv
                                  responses:
                                    '200':
                                      description: CSV export
                                      content:
                                        text/csv:
                                          schema:
                                            type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null);
        var (_, content) = Assert.Single(clients);

        Assert.Contains("responseType: 'text'", content, StringComparison.Ordinal);
        Assert.Contains("Promise<ApiResult<string>>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_JsonOperation_DoesNotEmitResponseTypeText()
    {
        // Regression: text branch must not leak into ordinary JSON operations.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  responses:
                                    '200':
                                      description: Items
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null);
        var (_, content) = Assert.Single(clients);

        Assert.DoesNotContain("responseType: 'text'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_BinaryFileDownload_DoesNotEmitResponseTypeText()
    {
        // Regression: file downloads keep responseType: 'blob', not 'text'.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /files/{id}:
                                get:
                                  operationId: downloadFile
                                  parameters:
                                    - name: id
                                      in: path
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: Binary
                                      content:
                                        application/octet-stream:
                                          schema:
                                            type: string
                                            format: binary
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null);
        var (_, content) = Assert.Single(clients);

        Assert.Contains("responseType: 'blob'", content, StringComparison.Ordinal);
        Assert.DoesNotContain("responseType: 'text'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_QueryParamReferencesEnum_EmitsEnumImport()
    {
        // When a query parameter $refs a component schema (here an enum), the generator
        // must emit the corresponding import statement. Without it, the inline
        // `query?: { businessLine?: BusinessLine }` signature references an undeclared
        // type and the generated .ts file fails TS2304.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Demo
                              version: 1.0.0
                            paths:
                              /people:
                                get:
                                  operationId: listPeople
                                  parameters:
                                    - name: businessLine
                                      in: query
                                      schema:
                                        $ref: '#/components/schemas/BusinessLine'
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              $ref: '#/components/schemas/PersonSummary'
                            components:
                              schemas:
                                PersonSummary:
                                  type: object
                                  properties:
                                    id: { type: string }
                                BusinessLine:
                                  type: string
                                  enum: [Alpha, Beta]
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var enumNames = new HashSet<string>(StringComparer.Ordinal) { "BusinessLine" };
        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null, enumNames);
        var (_, content) = Assert.Single(clients);

        // The query type must reference BusinessLine, AND BusinessLine must be imported
        // from the enums barrel — both are required for the generated TS to compile.
        Assert.Contains("businessLine?: BusinessLine", content, StringComparison.Ordinal);
        Assert.Contains("import type { BusinessLine } from '../enums';", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PathItemLevelQueryParamReferencesEnum_EmitsEnumImport()
    {
        // Path-item-level parameters (declared once on the pathItem, shared by every
        // operation under that path) must contribute to the import set just like
        // operation-level ones — otherwise refactoring a shared filter from per-op to
        // per-path would silently re-introduce TS2304.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Demo
                              version: 1.0.0
                            paths:
                              /people:
                                parameters:
                                  - name: businessLine
                                    in: query
                                    schema:
                                      $ref: '#/components/schemas/BusinessLine'
                                get:
                                  operationId: listPeople
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              $ref: '#/components/schemas/PersonSummary'
                            components:
                              schemas:
                                PersonSummary:
                                  type: object
                                  properties:
                                    id: { type: string }
                                BusinessLine:
                                  type: string
                                  enum: [Alpha, Beta]
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var enumNames = new HashSet<string>(StringComparer.Ordinal) { "BusinessLine" };
        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null, enumNames);
        var (_, content) = Assert.Single(clients);

        Assert.Contains("businessLine?: BusinessLine", content, StringComparison.Ordinal);
        Assert.Contains("import type { BusinessLine } from '../enums';", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_InlineEnumOnQueryParam_RendersLiteralUnionAndEmitsNoImport()
    {
        // An inline enum (no $ref) carries the allowed values on the parameter schema
        // itself. The generated TS query type must surface those values as a literal
        // union (compile-time-checked) and must NOT emit an enum import, since there
        // is no component schema to import from.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Demo
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: findPetsByStatus
                                  parameters:
                                    - name: status
                                      in: query
                                      schema:
                                        type: string
                                        enum: [available, pending, sold]
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null);
        var (_, content) = Assert.Single(clients);

        Assert.Contains("status?: 'available' | 'pending' | 'sold'", content, StringComparison.Ordinal);
        Assert.DoesNotContain("../enums", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_HeaderParams_AppearInSignatureAndAreForwardedToApiRequest()
    {
        // Header params used to be silently dropped by the TS client writer. They now
        // appear as an inline `headers?: { … }` arg after query, with non-identifier
        // names (X-Correlation-Id) quoted, required headers without `?`, and the values
        // forwarded to the ApiClient via `headers: { … }` in the request options.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  parameters:
                                    - name: limit
                                      in: query
                                      schema:
                                        type: integer
                                    - name: X-Correlation-Id
                                      in: header
                                      required: true
                                      schema:
                                        type: string
                                    - name: X-Continuation
                                      in: header
                                      schema:
                                        type: string
                                  responses:
                                    '200': { description: OK }
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null);
        var (_, content) = Assert.Single(clients);

        // Signature: required header has no `?` on the key, optional one does; non-identifier
        // names quoted. The outer `headers?:` mirrors how `query?:` is always optional —
        // callers may omit the arg entirely (the generator doesn't enforce required-ness
        // at the TS type level, just as it doesn't for required query params).
        Assert.Contains(
            "headers?: { 'X-Correlation-Id': string; 'X-Continuation'?: string }",
            content,
            StringComparison.Ordinal);

        // Forwarded to api.request via a headers block, mirroring the query block — values
        // use optional chaining since the outer headers arg is optional.
        Assert.Contains("'X-Correlation-Id': headers?.['X-Correlation-Id'],", content, StringComparison.Ordinal);
        Assert.Contains("'X-Continuation': headers?.['X-Continuation'],", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_HeaderParamRefEnum_EmitsEnumImportAndSurfacesType()
    {
        // A header param schema that $refs an enum must (a) produce the enum import,
        // and (b) surface the enum type in the inline headers signature.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /things:
                                get:
                                  operationId: listThings
                                  parameters:
                                    - name: X-Tier
                                      in: header
                                      required: true
                                      schema:
                                        $ref: '#/components/schemas/Tier'
                                  responses:
                                    '200': { description: OK }
                            components:
                              schemas:
                                Tier:
                                  type: string
                                  enum: [Free, Pro]
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var enumNames = new HashSet<string>(StringComparer.Ordinal) { "Tier" };
        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null, enumNames);
        var (_, content) = Assert.Single(clients);

        Assert.Contains("'X-Tier': Tier", content, StringComparison.Ordinal); // type in headers signature
        Assert.Contains("'X-Tier': headers?.['X-Tier'],", content, StringComparison.Ordinal); // forwarded
        Assert.Contains("import type { Tier } from '../enums';", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}