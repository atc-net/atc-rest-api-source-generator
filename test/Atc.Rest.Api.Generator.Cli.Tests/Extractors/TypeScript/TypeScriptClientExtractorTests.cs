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
        // not a Blob (default for non-JSON responses). Per-op result types replaced the
        // generic ApiResult return type — the 'ok' arm now carries `data: string` directly.
        Assert.Contains("responseType: 'text'", content, StringComparison.Ordinal);
        Assert.Contains("Promise<GetTextReportResult>", content, StringComparison.Ordinal);
        Assert.Contains("{ status: 'ok'; data: string; response: Response }", content, StringComparison.Ordinal);
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
        Assert.Contains("Promise<ExportCsvResult>", content, StringComparison.Ordinal);
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

    [Fact]
    public void Extract_OperationIdIsReservedWord_PrefixesMethodWithUnderscore()
    {
        // operationId: "delete" would produce `async delete(...)` on the client class,
        // which TypeScript accepts as a method but breaks the moment a consumer treats
        // it as `client.delete` (shadows the global `delete` operator at the call site).
        // The sanitizer prefixes `_` to keep both the declaration and the call site safe.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items/{id}:
                                delete:
                                  operationId: delete
                                  parameters:
                                    - name: id
                                      in: path
                                      required: true
                                      schema: { type: string }
                                  responses:
                                    '204': { description: No Content }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(document!, headerContent: null));

        Assert.Contains("async _delete(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("async delete(", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_OperationIdStartsWithDigit_PrefixesMethodWithUnderscore()
    {
        // `1stPage` would camelCase to `1stPage` — invalid TS identifier (leading digit).
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items:
                                get:
                                  operationId: 1stPage
                                  responses:
                                    '200': { description: OK }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(document!, headerContent: null));

        Assert.Contains("async _1stPage(", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_OperationIdWithHyphen_CamelCasesAndStaysSafe()
    {
        // Hyphens are word separators in ToCamelCase, so `list-items` → `listItems`.
        // Regression-guard: the sanitizer must not re-introduce the hyphen.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items:
                                get:
                                  operationId: list-items
                                  responses:
                                    '200': { description: OK }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(document!, headerContent: null));

        Assert.Contains("async listItems(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("async list-items(", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PerOperationResultType_EmitsAliasWithDeclaredArmsAndParseError()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /pets/{petId}:
                                get:
                                  operationId: getPet
                                  parameters:
                                    - name: petId
                                      in: path
                                      required: true
                                      schema: { type: string }
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Pet'
                                    '404':
                                      description: Not found
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id: { type: string }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(document!, headerContent: null));

        // Per-op alias has one arm per declared status + parseError, no extra arms.
        Assert.Contains("export type GetPetResult =", content, StringComparison.Ordinal);
        Assert.Contains("{ status: 'ok'; data: Pet; response: Response }", content, StringComparison.Ordinal);
        Assert.Contains("{ status: 'notFound'; error: ApiError; response: Response }", content, StringComparison.Ordinal);
        Assert.Contains("{ status: 'parseError'; error: Error; response: Response }", content, StringComparison.Ordinal);

        // Method declares the per-op type and casts the request<T> call to it.
        Assert.Contains("async getPet(petId: string): Promise<GetPetResult>", content, StringComparison.Ordinal);
        Assert.Contains("as Promise<GetPetResult>", content, StringComparison.Ordinal);

        // Errors barrel is imported because notFound arm needs ApiError.
        Assert.Contains("from '../errors'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PerOperationResultType_OmitsArmsForStatusesTheOperationDoesNotDeclare()
    {
        // 401/403/409 etc. are mapped by handleResponse globally, but per-op types must
        // stay narrow — only emit arms the spec actually documents. The cast at the call
        // site is sound because handleResponse uses the same discriminator names.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Items'
                            components:
                              schemas:
                                Items:
                                  type: object
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(document!, headerContent: null));

        Assert.Contains("export type ListItemsResult =", content, StringComparison.Ordinal);
        Assert.Contains("status: 'ok'", content, StringComparison.Ordinal);
        Assert.Contains("status: 'parseError'", content, StringComparison.Ordinal);
        Assert.DoesNotContain("status: 'unauthorized'", content, StringComparison.Ordinal);
        Assert.DoesNotContain("status: 'notFound'", content, StringComparison.Ordinal);
        Assert.DoesNotContain("status: 'serverError'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PerOperationResultType_DefaultResponseFansOutToCommonErrorArms()
    {
        // PetStore-style specs use `default:` as a catch-all error response. The per-op
        // type fans that into the standard error arms so consumers can still match
        // common 4xx/5xx without the spec listing every code.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Pets'
                                    default:
                                      description: unexpected error
                            components:
                              schemas:
                                Pets:
                                  type: array
                                  items: { type: string }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(document!, headerContent: null));

        Assert.Contains("status: 'ok'", content, StringComparison.Ordinal);
        Assert.Contains("status: 'badRequest'", content, StringComparison.Ordinal);
        Assert.Contains("status: 'unauthorized'", content, StringComparison.Ordinal);
        Assert.Contains("status: 'notFound'", content, StringComparison.Ordinal);
        Assert.Contains("status: 'serverError'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PerOperationResultType_AcceptedArmFor202WithBody()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /jobs:
                                post:
                                  operationId: enqueueJob
                                  responses:
                                    '202':
                                      description: Accepted
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Job'
                            components:
                              schemas:
                                Job:
                                  type: object
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(document!, headerContent: null));

        // 202 maps to a dedicated 'accepted' discriminator so consumers can distinguish
        // it from 'ok' (200) when the spec uses both.
        Assert.Contains("{ status: 'accepted'; data: Job; response: Response }", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_AxiosVariant_ImportsAxiosResponseForPerOpArms()
    {
        // The per-op result-type arms reference Response (Fetch) or AxiosResponse (Axios).
        // Axios needs an explicit import — Fetch's Response is a DOM global.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  responses:
                                    '200':
                                      description: OK
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(
            document!,
            headerContent: null,
            httpClient: TypeScriptHttpClient.Axios));

        Assert.Contains("import type { AxiosResponse } from 'axios';", content, StringComparison.Ordinal);
        Assert.Contains("response: AxiosResponse", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_RequestBodyReferencesWritableSchema_UsesWritableSuffixInSignature()
    {
        // When the schema has readOnly or writeOnly markers, the body parameter must
        // route through `<Name>Writable`. The response side of the same schema keeps
        // the canonical name. Both names need to be imported.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /users:
                                post:
                                  operationId: createUser
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/User'
                                  responses:
                                    '201':
                                      description: Created
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/User'
                            components:
                              schemas:
                                User:
                                  type: object
                                  properties:
                                    id: { type: string, readOnly: true }
                                    email: { type: string }
                                    password: { type: string, writeOnly: true }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var writableSchemas = TypeScriptModelExtractor.CollectSchemasWithWritableVariant(document!);
        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(
            document!,
            headerContent: null,
            writableSchemas: writableSchemas));

        // Method signature carries the Writable variant on the body…
        Assert.Contains("body: UserWritable", content, StringComparison.Ordinal);

        // …but the per-op result type's `ok`/`created` arms keep the canonical name.
        Assert.Contains("data: User", content, StringComparison.Ordinal);

        // Both names are imported from the models barrel.
        Assert.Contains("import type { User, UserWritable } from '../models'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_RequestBodyReferencesSchemaWithoutMarkers_KeepsCanonicalName()
    {
        // Schemas without readOnly / writeOnly markers must NOT have their body params
        // remapped to a non-existent `<Name>Writable` type.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /addresses:
                                post:
                                  operationId: createAddress
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/Address'
                                  responses:
                                    '201': { description: Created }
                            components:
                              schemas:
                                Address:
                                  type: object
                                  properties:
                                    street: { type: string }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var writableSchemas = TypeScriptModelExtractor.CollectSchemasWithWritableVariant(document!);
        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(
            document!,
            headerContent: null,
            writableSchemas: writableSchemas));

        Assert.Contains("body: Address", content, StringComparison.Ordinal);
        Assert.DoesNotContain("AddressWritable", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_OperationWithSummaryAndDeprecated_EmitsJsDocAboveMethod()
    {
        // Operation-level summary + deprecated flag flow into a /** ... */
        // block above the generated method. Without this, consumers don't see the
        // strikethrough in their IDE and lose the spec author's hint.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: t, version: '1' }
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  summary: List all pets
                                  deprecated: true
                                  responses:
                                    '200': { description: OK }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(document!, headerContent: null));

        // Multi-line block: summary + @deprecated.
        Assert.Contains("* List all pets", content, StringComparison.Ordinal);
        Assert.Contains("* @deprecated", content, StringComparison.Ordinal);

        // The block sits immediately above the method — verify ordering.
        var jsDocIdx = content.IndexOf("@deprecated", StringComparison.Ordinal);
        var methodIdx = content.IndexOf("async listPets", StringComparison.Ordinal);
        Assert.True(jsDocIdx > 0 && methodIdx > jsDocIdx, "JSDoc must precede the method signature.");
    }

    [Fact]
    public void Extract_OperationWithOnlySummary_EmitsSingleLineJsDoc()
    {
        // Summary alone → compact single-line `/** ... */` form. No @deprecated noise.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: t, version: '1' }
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  summary: List all pets
                                  responses:
                                    '200': { description: OK }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(document!, headerContent: null));

        Assert.Contains("/** List all pets */", content, StringComparison.Ordinal);
        Assert.DoesNotContain("@deprecated", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_OperationWithoutSummaryOrDeprecated_DoesNotEmitJsDoc()
    {
        // Regression guard: ops with nothing worth saying must not emit empty JSDoc blocks.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: t, version: '1' }
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200': { description: OK }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(document!, headerContent: null));

        // Per-op result type alias is emitted with the file header — that's the only /**
        // (the file header) — the method should NOT have a leading JSDoc.
        var methodIdx = content.IndexOf("async listPets", StringComparison.Ordinal);
        var before = content[..methodIdx];

        // Header comment `// <auto-generated />` is fine; a `/**` above the method is not.
        // Look for the absence of `/**` in the few lines right above the method.
        var lastNewlineBefore = before.LastIndexOf('\n');
        var prevNewline = before.LastIndexOf('\n', lastNewlineBefore - 1);
        var lineAbove = before[(prevNewline + 1)..lastNewlineBefore];
        Assert.DoesNotContain("/**", lineAbove, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}