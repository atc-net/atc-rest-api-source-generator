// ReSharper disable CollectionNeverUpdated.Local
namespace Atc.Rest.Api.Generator.Tests.Services;

public class SpecificationServiceTests
{
    [Fact]
    public void ReadFromContent_ValidYaml_ReturnsSpecificationFileWithDocument()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK

                            """;
        var result = SpecificationService.ReadFromContent(yaml, "test.yaml");

        Assert.NotNull(result);
        Assert.NotNull(result.Document);
        Assert.Equal("test.yaml", result.FileName);
        Assert.Equal(1, result.PathCount);
        Assert.Equal(1, result.OperationCount);
    }

    [Fact]
    public void ReadFromContent_InvalidYaml_ReturnsSpecificationFileWithNullDocument()
    {
        const string yaml = "invalid: yaml: content: here:";

        var result = SpecificationService.ReadFromContent(yaml, "test.yaml");

        Assert.NotNull(result);
        Assert.Null(result.Document);
    }

    [Fact]
    public void ReadFromContent_WithSchemas_CountsSchemasCorrectly()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id:
                                      type: integer
                                Error:
                                  type: object
                                  properties:
                                    message:
                                      type: string

                            """;
        var result = SpecificationService.ReadFromContent(yaml, "test.yaml");

        Assert.NotNull(result);
        Assert.NotNull(result.Document);
        Assert.Equal(2, result.SchemaCount);
    }

    [Fact]
    public void MergeSpecifications_SingleBaseNoPartsReturnsSuccess()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths:
                                  /health:
                                    get:
                                      operationId: health
                                      responses:
                                        '200':
                                          description: OK

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Base.yaml");
        var emptyParts = new List<SpecificationFile>();

        var result = SpecificationService.MergeSpecifications(baseFile, emptyParts);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
        Assert.Equal(1, result.TotalPaths);
        Assert.Equal(1, result.TotalOperations);
    }

    [Fact]
    public void MergeSpecifications_PreservesOpenApiSpecVersion()
    {
        // The merged document is a clone of the base; the spec version stamped at
        // parse time must survive the clone so later phases can gate on it.
        const string baseYaml = """
                                openapi: 3.2.0
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths:
                                  /health:
                                    get:
                                      operationId: health
                                      responses:
                                        '200':
                                          description: OK
                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "SpecVersionBase.yaml");
        Assert.Equal(OpenApiSpecVersion.OpenApi3_2, baseFile.Document.GetOpenApiSpecVersion());

        var result = SpecificationService.MergeSpecifications(baseFile, new List<SpecificationFile>());

        Assert.NotNull(result.Document);
        Assert.Equal(OpenApiSpecVersion.OpenApi3_2, result.Document.GetOpenApiSpecVersion());
    }

    [Fact]
    public void SerializeToYaml_DoesNotLeakSpecVersionMetadataKey()
    {
        // The spec version is stashed in the document's non-serialized Metadata bag
        // (not Extensions, which serialize as x-* fields), so it must never appear
        // in written YAML output.
        const string yaml = """
                            openapi: 3.2.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /health:
                                get:
                                  operationId: health
                                  responses:
                                    '200':
                                      description: OK
                            """;
        var file = SpecificationService.ReadFromContent(yaml, "SpecVersionLeak.yaml");
        Assert.Equal(OpenApiSpecVersion.OpenApi3_2, file.Document.GetOpenApiSpecVersion());

        var serialized = SpecificationService.SerializeToYaml(file.Document);

        Assert.DoesNotContain("x-atc-openapi-spec-version", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeSpecifications_MergesPathsFromPartFiles()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths:
                                  /health:
                                    get:
                                      operationId: health
                                      responses:
                                        '200':
                                          description: OK

                                """;

        // Part files need valid openapi/info sections to parse
        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                paths:
                                  /pets:
                                    get:
                                      operationId: listPets
                                      responses:
                                        '200':
                                          description: OK
                                    post:
                                      operationId: createPet
                                      responses:
                                        '201':
                                          description: Created

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
        Assert.Equal(2, result.TotalPaths);
        Assert.Equal(3, result.TotalOperations);
    }

    [Fact]
    public void MergeSpecifications_MergesSchemasFromPartFiles()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths: {}
                                components:
                                  schemas:
                                    Error:
                                      type: object
                                      properties:
                                        message:
                                          type: string

                                """;

        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                paths: {}
                                components:
                                  schemas:
                                    Pet:
                                      type: object
                                      properties:
                                        id:
                                          type: integer
                                        name:
                                          type: string

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
        Assert.Equal(2, result.TotalSchemas);
    }

    [Fact]
    public void MergeSpecifications_DuplicatePath_ErrorOnDuplicate_ReportsError()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths:
                                  /pets:
                                    get:
                                      operationId: listPets
                                      responses:
                                        '200':
                                          description: OK

                                """;

        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                paths:
                                  /pets:
                                    get:
                                      operationId: listPets2
                                      responses:
                                        '200':
                                          description: OK

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var config = MultiPartConfiguration.Default;

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile },
            config);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.RuleId == RuleIdentifiers.DuplicatePathInPart);
    }

    [Fact]
    public void MergeSpecifications_DuplicateSchema_ErrorOnDuplicate_ReportsError()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths: {}
                                components:
                                  schemas:
                                    Pet:
                                      type: object

                                """;

        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                paths: {}
                                components:
                                  schemas:
                                    Pet:
                                      type: object

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var config = MultiPartConfiguration.Default;

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile },
            config);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.RuleId == RuleIdentifiers.DuplicateSchemaInPart);
    }

    [Fact]
    public void MergeSpecifications_MultiplePartFiles_MergesAllSuccessfully()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Showcase API
                                  version: 1.0.0
                                paths: {}

                                """;

        const string accountsYaml = """

                                    openapi: 3.1.1
                                    info:
                                      title: Accounts Part
                                      version: 1.0.0
                                    paths:
                                      /accounts:
                                        get:
                                          operationId: listAccounts
                                          responses:
                                            '200':
                                              description: OK
                                    components:
                                      schemas:
                                        Account:
                                          type: object

                                    """;

        const string usersYaml = """

                                 openapi: 3.1.1
                                 info:
                                   title: Users Part
                                   version: 1.0.0
                                 paths:
                                   /users:
                                     get:
                                       operationId: listUsers
                                       responses:
                                         '200':
                                           description: OK
                                 components:
                                   schemas:
                                     User:
                                       type: object

                                 """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var accountsPart = SpecificationService.ReadFromContent(accountsYaml, "Showcase_Accounts.yaml");
        var usersPart = SpecificationService.ReadFromContent(usersYaml, "Showcase_Users.yaml");

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { accountsPart, usersPart });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
        Assert.Equal(2, result.TotalPaths);
        Assert.Equal(2, result.TotalOperations);
        Assert.Equal(2, result.TotalSchemas);
        Assert.True(result.IsMultiPart);
        Assert.Equal(2, result.PartFiles.Count);
    }

    [Fact]
    public void MergeSpecifications_MergesTags()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                tags:
                                  - name: Base
                                paths: {}

                                """;

        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                tags:
                                  - name: Pets
                                paths: {}

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
        Assert.NotNull(result.Document.Tags);
        Assert.Equal(2, result.Document.Tags.Count);
    }

    [Fact]
    public void Split_ByTag_CreatesPartFilesPerTag()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  tags:
                                    - Pets
                                  responses:
                                    '200':
                                      description: OK
                              /users:
                                get:
                                  operationId: listUsers
                                  tags:
                                    - Users
                                  responses:
                                    '200':
                                      description: OK

                            """;
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = SpecificationService.Split(doc, "Showcase", SplitStrategy.ByTag, extractCommon: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.PartFiles.Count);
        Assert.Contains(result.PartFiles, p => p.PartName == "Pets");
        Assert.Contains(result.PartFiles, p => p.PartName == "Users");
    }

    [Fact]
    public void Split_ByPathSegment_CreatesPartFilesPerSegment()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                              /pets/{petId}:
                                get:
                                  operationId: getPet
                                  responses:
                                    '200':
                                      description: OK
                              /users:
                                get:
                                  operationId: listUsers
                                  responses:
                                    '200':
                                      description: OK

                            """;
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = SpecificationService.Split(doc, "Showcase", SplitStrategy.ByPathSegment, extractCommon: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.PartFiles.Count);
        Assert.Contains(result.PartFiles, p => p.PartName == "Pets");
        Assert.Contains(result.PartFiles, p => p.PartName == "Users");
    }

    [Fact]
    public void Split_GeneratesBaseFile()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  tags:
                                    - Pets
                                  responses:
                                    '200':
                                      description: OK

                            """;
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = SpecificationService.Split(doc, "Showcase", SplitStrategy.ByTag, extractCommon: false);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.BaseFile);
        Assert.True(result.BaseFile.IsBaseFile);
        Assert.Equal("Showcase.yaml", result.BaseFile.FileName);
    }

    [Fact]
    public void Split_PartFileNamingConvention()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /accounts:
                                get:
                                  operationId: listAccounts
                                  tags:
                                    - Accounts
                                  responses:
                                    '200':
                                      description: OK

                            """;
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = SpecificationService.Split(doc, "Showcase", SplitStrategy.ByTag, extractCommon: false);

        Assert.True(result.IsSuccess);
        Assert.Single(result.PartFiles);
        Assert.Equal("Showcase_Accounts.yaml", result.PartFiles[0].FileName);
    }

    [Fact]
    public void Analyze_CountsOperationsAndSchemas()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                                post:
                                  operationId: createPet
                                  responses:
                                    '201':
                                      description: Created
                            components:
                              schemas:
                                Pet:
                                  type: object
                                Error:
                                  type: object

                            """;
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var analysis = SpecificationService.Analyze(doc, "test.yaml");

        Assert.Equal("test.yaml", analysis.FilePath);
        Assert.Equal(1, analysis.TotalPaths);
        Assert.Equal(2, analysis.TotalOperations);
        Assert.Equal(2, analysis.TotalSchemas);
    }

    [Fact]
    public void Analyze_IdentifiesTagsWithOperationCounts()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  tags:
                                    - Pets
                                  responses:
                                    '200':
                                      description: OK
                                post:
                                  operationId: createPet
                                  tags:
                                    - Pets
                                  responses:
                                    '201':
                                      description: Created
                              /users:
                                get:
                                  operationId: listUsers
                                  tags:
                                    - Users
                                  responses:
                                    '200':
                                      description: OK

                            """;
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var analysis = SpecificationService.Analyze(doc, "test.yaml");

        Assert.Equal(2, analysis.Tags.Count);
        Assert.True(analysis.Tags.ContainsKey("Pets"));
        Assert.True(analysis.Tags.ContainsKey("Users"));
        Assert.Equal(2, analysis.Tags["Pets"].OperationCount);
        Assert.Equal(1, analysis.Tags["Users"].OperationCount);
    }

    [Fact]
    public void Analyze_IdentifiesPathSegments()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                              /pets/{petId}:
                                get:
                                  operationId: getPet
                                  responses:
                                    '200':
                                      description: OK
                              /users:
                                get:
                                  operationId: listUsers
                                  responses:
                                    '200':
                                      description: OK

                            """;
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var analysis = SpecificationService.Analyze(doc, "test.yaml");

        Assert.Equal(2, analysis.PathSegments.Count);
        Assert.True(analysis.PathSegments.ContainsKey("pets"));
        Assert.True(analysis.PathSegments.ContainsKey("users"));
        Assert.Equal(2, analysis.PathSegments["pets"].PathCount);
        Assert.Equal(1, analysis.PathSegments["users"].PathCount);
    }

    [Fact]
    public void Analyze_RecommendsStrategy()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  tags:
                                    - Pets
                                  responses:
                                    '200':
                                      description: OK

                            """;
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var analysis = SpecificationService.Analyze(doc, "test.yaml");

        Assert.False(string.IsNullOrEmpty(analysis.RecommendedStrategyReason));
    }

    [Fact]
    public void Analyze_ShouldSplit_SmallSpec_ReturnsFalse()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /health:
                                get:
                                  operationId: health
                                  responses:
                                    '200':
                                      description: OK

                            """;
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var analysis = SpecificationService.Analyze(doc, "test.yaml");

        Assert.False(analysis.ShouldSplit);
    }

    [Fact]
    public void ValidatePartFile_PartWithServerSection_ReportsWarning()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Part File
                              version: 1.0.0
                            servers:
                              - url: https://api.example.com
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK

                            """;
        var partFile = SpecificationService.ReadFromContent(yaml, "Showcase_Pets.yaml");

        var diagnostics = SpecificationService.ValidatePartFile(partFile);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.PartFileContainsProhibitedSection);
    }

    [Fact]
    public void ValidatePartFile_PartWithSecuritySchemes_ReportsWarning()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Part File
                              version: 1.0.0
                            paths: {}
                            components:
                              securitySchemes:
                                ApiKey:
                                  type: apiKey
                                  in: header
                                  name: X-API-Key

                            """;
        var partFile = SpecificationService.ReadFromContent(yaml, "Showcase_Pets.yaml");

        var diagnostics = SpecificationService.ValidatePartFile(partFile);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.PartFileContainsProhibitedSection);
    }

    [Fact]
    public void ValidatePartFile_ValidPartFile_NoDiagnostics()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Part File
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                Pet:
                                  type: object

                            """;
        var partFile = SpecificationService.ReadFromContent(yaml, "Showcase_Pets.yaml");

        var diagnostics = SpecificationService.ValidatePartFile(partFile);

        // Note: Part files may have info section warning but not errors
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void SpecificationFile_IsPartFile_True_WhenFollowsNamingConvention()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}

                            """;
        var file = SpecificationFile.FromContent("Showcase_Accounts.yaml", yaml, "Showcase");

        Assert.True(file.IsPartFile);
        Assert.False(file.IsBaseFile);
        Assert.Equal("Accounts", file.PartName);
    }

    [Fact]
    public void SpecificationFile_IsBaseFile_True_WhenMatchesBaseName()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}

                            """;
        var file = SpecificationFile.FromContent("Showcase.yaml", yaml, "Showcase");

        Assert.True(file.IsBaseFile);
        Assert.False(file.IsPartFile);
        Assert.Null(file.PartName);
    }

    [Fact]
    public void SpecificationFile_Tags_ReturnsTagNames()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            tags:
                              - name: Pets
                                description: Pet operations
                              - name: Users
                                description: User operations
                            paths: {}

                            """;
        var file = SpecificationFile.FromContent("test.yaml", yaml);

        Assert.Equal(2, file.GetTags().Count);
        Assert.Contains("Pets", file.GetTags(), StringComparer.Ordinal);
        Assert.Contains("Users", file.GetTags(), StringComparer.Ordinal);
    }

    [Fact]
    public void MergeResult_SingleFile_IsMultiPart_False()
    {
        const string yaml = """

                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}

                            """;
        var file = SpecificationService.ReadFromContent(yaml, "test.yaml");
        var result = MergeResult.SingleFile(file);

        Assert.False(result.IsMultiPart);
    }

    [Fact]
    public void MergeResult_AllFiles_IncludesBaseAndParts()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths: {}

                                """;

        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                paths:
                                  /pets:
                                    get:
                                      operationId: listPets
                                      responses:
                                        '200':
                                          description: OK

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile });

        Assert.Equal(2, result.AllFiles.Count);
        Assert.Same(baseFile, result.AllFiles[0]);
        Assert.Same(partFile, result.AllFiles[1]);
    }

    [Fact]
    public void MultiPartConfiguration_Default_HasExpectedValues()
    {
        var config = MultiPartConfiguration.Default;

        Assert.True(config.Enabled);
        Assert.Equal("auto", config.Discovery);
        Assert.Equal(MergeStrategy.ErrorOnDuplicate, config.PathsMergeStrategy);
        Assert.Equal(MergeStrategy.ErrorOnDuplicate, config.SchemasMergeStrategy);
        Assert.Equal(MergeStrategy.MergeIfIdentical, config.ParametersMergeStrategy);
    }

    [Fact]
    public void MergeSpecifications_DuplicateSchema_MergeIfIdentical_IdenticalSkips()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths: {}
                                components:
                                  schemas:
                                    Pet:
                                      type: object
                                      properties:
                                        id:
                                          type: integer

                                """;

        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                paths: {}
                                components:
                                  schemas:
                                    Pet:
                                      type: object
                                      properties:
                                        id:
                                          type: integer

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var config = MultiPartConfiguration.Default with
        {
            SchemasMergeStrategy = MergeStrategy.MergeIfIdentical,
        };

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile },
            config);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Diagnostics, d => d.RuleId == RuleIdentifiers.NonIdenticalMergeConflict);
    }

    [Fact]
    public void MergeSpecifications_DuplicateSchema_MergeIfIdentical_DifferentReportsMPT010()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths: {}
                                components:
                                  schemas:
                                    Pet:
                                      type: object
                                      properties:
                                        id:
                                          type: integer

                                """;

        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                paths: {}
                                components:
                                  schemas:
                                    Pet:
                                      type: object
                                      properties:
                                        name:
                                          type: string

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var config = MultiPartConfiguration.Default with
        {
            SchemasMergeStrategy = MergeStrategy.MergeIfIdentical,
        };

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile },
            config);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.RuleId == RuleIdentifiers.NonIdenticalMergeConflict);
    }

    [Fact]
    public void MergeSpecifications_DuplicateParameter_MergeIfIdentical_IdenticalSkips()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths: {}
                                components:
                                  parameters:
                                    LimitParam:
                                      name: limit
                                      in: query
                                      schema:
                                        type: integer

                                """;

        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                paths: {}
                                components:
                                  parameters:
                                    LimitParam:
                                      name: limit
                                      in: query
                                      schema:
                                        type: integer

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var config = MultiPartConfiguration.Default with
        {
            ParametersMergeStrategy = MergeStrategy.MergeIfIdentical,
        };

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile },
            config);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Diagnostics, d => d.RuleId == RuleIdentifiers.NonIdenticalMergeConflict);
    }

    [Fact]
    public void MergeSpecifications_DuplicateParameter_MergeIfIdentical_DifferentReportsMPT010()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths: {}
                                components:
                                  parameters:
                                    LimitParam:
                                      name: limit
                                      in: query
                                      schema:
                                        type: integer

                                """;

        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                paths: {}
                                components:
                                  parameters:
                                    LimitParam:
                                      name: limit
                                      in: query
                                      schema:
                                        type: string

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var config = MultiPartConfiguration.Default with
        {
            ParametersMergeStrategy = MergeStrategy.MergeIfIdentical,
        };

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile },
            config);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.RuleId == RuleIdentifiers.NonIdenticalMergeConflict);
    }

    [Fact]
    public void MergeSpecifications_DuplicateParameter_ErrorOnDuplicate_UsesMPT009()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths: {}
                                components:
                                  parameters:
                                    LimitParam:
                                      name: limit
                                      in: query
                                      schema:
                                        type: integer

                                """;

        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                paths: {}
                                components:
                                  parameters:
                                    LimitParam:
                                      name: limit
                                      in: query
                                      schema:
                                        type: integer

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var config = MultiPartConfiguration.Default with
        {
            ParametersMergeStrategy = MergeStrategy.ErrorOnDuplicate,
        };

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile },
            config);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.RuleId == RuleIdentifiers.DuplicateParameterInPart);
        Assert.DoesNotContain(result.Diagnostics, d => d.RuleId == RuleIdentifiers.DuplicateSchemaInPart);
    }

    [Fact]
    public void MergeSpecifications_DuplicatePath_MergeIfIdentical_IdenticalSkips()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths:
                                  /pets:
                                    get:
                                      operationId: listPets
                                      responses:
                                        '200':
                                          description: OK

                                """;

        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                paths:
                                  /pets:
                                    get:
                                      operationId: listPets
                                      responses:
                                        '200':
                                          description: OK

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var config = MultiPartConfiguration.Default with
        {
            PathsMergeStrategy = MergeStrategy.MergeIfIdentical,
        };

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile },
            config);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Diagnostics, d => d.RuleId == RuleIdentifiers.NonIdenticalMergeConflict);
    }

    [Fact]
    public void MergeSpecifications_DuplicatePath_MergeIfIdentical_DifferentReportsMPT010()
    {
        const string baseYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths:
                                  /pets:
                                    get:
                                      operationId: listPets
                                      responses:
                                        '200':
                                          description: OK

                                """;

        const string partYaml = """

                                openapi: 3.1.1
                                info:
                                  title: Part File
                                  version: 1.0.0
                                paths:
                                  /pets:
                                    get:
                                      operationId: listPetsV2
                                      responses:
                                        '200':
                                          description: OK

                                """;
        var baseFile = SpecificationService.ReadFromContent(baseYaml, "Showcase.yaml");
        var partFile = SpecificationService.ReadFromContent(partYaml, "Showcase_Pets.yaml");

        var config = MultiPartConfiguration.Default with
        {
            PathsMergeStrategy = MergeStrategy.MergeIfIdentical,
        };

        var result = SpecificationService.MergeSpecifications(
            baseFile,
            new List<SpecificationFile> { partFile },
            config);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.RuleId == RuleIdentifiers.NonIdenticalMergeConflict);
    }

    // ========== $self URI ==========
    [Fact]
    public void ReadFromContent_WithSelfUri_SetsDocumentBaseUri()
    {
        // Arrange — A4: top-level $self declares canonical URI; used as baseUri for $ref resolution
        const string yaml = """
                            $self: "https://api.example.com/openapi.yaml"
                            openapi: "3.2.0"
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            """;

        // Act
        var result = SpecificationService.ReadFromContent(yaml, "test.yaml");

        // Assert
        Assert.NotNull(result.Document);
        Assert.Equal(new Uri("https://api.example.com/openapi.yaml"), result.Document.BaseUri);
    }

    [Fact]
    public void ReadFromContent_WithSelfUriSingleQuotes_SetsDocumentBaseUri()
    {
        // Arrange — A4: $self with single-quoted value
        const string yaml = """
                            $self: 'https://api.example.com/openapi.yaml'
                            openapi: "3.2.0"
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            """;

        // Act
        var result = SpecificationService.ReadFromContent(yaml, "test.yaml");

        // Assert
        Assert.NotNull(result.Document);
        Assert.Equal(new Uri("https://api.example.com/openapi.yaml"), result.Document.BaseUri);
    }

    [Fact]
    public void ReadFromContent_WithSelfUriUnquoted_SetsDocumentBaseUri()
    {
        // Arrange — A4: $self without quotes
        const string yaml = """
                            $self: https://api.example.com/openapi.yaml
                            openapi: "3.2.0"
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            """;

        // Act
        var result = SpecificationService.ReadFromContent(yaml, "test.yaml");

        // Assert
        Assert.NotNull(result.Document);
        Assert.Equal(new Uri("https://api.example.com/openapi.yaml"), result.Document.BaseUri);
    }

    [Fact]
    public void ReadFromContent_WithoutSelfUri_BaseUriUsesFilePath()
    {
        // Arrange — without $self, BaseUri should reflect the provided file path
        const string yaml = """
                            openapi: "3.2.0"
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            """;

        // Act
        var result = SpecificationService.ReadFromContent(yaml, "C:/specs/openapi.yaml");

        // Assert
        Assert.NotNull(result.Document);
        Assert.NotEqual(new Uri("https://api.example.com/openapi.yaml"), result.Document.BaseUri);
    }

    [Fact]
    public void ReadAndMergeResolved_PartReferencesABaseSchema_TheReferenceResolvesInTheMergedDocument()
    {
        // A $ref written in a part file points at the part's own document until the merged document
        // is read again; generators that follow reference targets (TypeScript) need it resolved.
        var directory = Path.Combine(Path.GetTempPath(), "atc-merge-resolved", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var basePath = Path.Combine(directory, "Shop.yaml");
            File.WriteAllText(basePath, """
                openapi: 3.0.3
                info:
                  title: Shop
                  version: 1.0.0
                paths: {}
                components:
                  schemas:
                    Error:
                      type: object
                      properties:
                        message:
                          type: string
                """);
            File.WriteAllText(Path.Combine(directory, "Shop_Orders.yaml"), """
                openapi: 3.0.3
                info:
                  title: Shop - Orders
                  version: 1.0.0
                paths:
                  /orders:
                    get:
                      operationId: listOrders
                      responses:
                        '500':
                          description: Error
                          content:
                            application/json:
                              schema:
                                $ref: '#/components/schemas/Error'
                """);

            var (merge, specification) = SpecificationService.ReadAndMergeResolved(basePath);

            Assert.True(merge.IsMultiPart);
            Assert.NotNull(specification?.Document);
            var schema = specification.Document.Paths["/orders"].Operations![HttpMethod.Get].Responses!["500"].Content!["application/json"].Schema;
            var reference = Assert.IsType<OpenApiSchemaReference>(schema);
            Assert.NotNull(reference.Target);
            Assert.NotNull(reference.Target.Properties?["message"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReadAndMergeResolved_OpenApi32Parts_KeepTheVersionQueryOperationsAndWebhooks()
    {
        // The merged document is read again from YAML: written as 3.0, a 3.2 document lost its query:
        // operations (and 3.1 type arrays became nullable), so the TypeScript client missed them silently.
        var directory = Path.Combine(Path.GetTempPath(), "atc-merge-resolved", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var basePath = Path.Combine(directory, "Shop.yaml");
            File.WriteAllText(basePath, """
                openapi: 3.2.0
                info:
                  title: Shop
                  version: 1.0.0
                paths: {}
                webhooks:
                  orderShipped:
                    post:
                      operationId: onOrderShipped
                      responses:
                        '200':
                          description: OK
                components:
                  schemas:
                    Pet:
                      type: object
                      properties:
                        name:
                          type: [string, 'null']
                """);
            File.WriteAllText(Path.Combine(directory, "Shop_Pets.yaml"), """
                openapi: 3.2.0
                info:
                  title: Shop - Pets
                  version: 1.0.0
                paths:
                  /pets/search:
                    query:
                      operationId: searchPets
                      requestBody:
                        content:
                          application/json:
                            schema:
                              type: object
                      responses:
                        '200':
                          description: OK
                          content:
                            application/json:
                              schema:
                                type: array
                                items:
                                  $ref: '#/components/schemas/Pet'
                """);

            var (merge, specification) = SpecificationService.ReadAndMergeResolved(basePath);

            Assert.True(merge.IsMultiPart);
            var document = specification?.Document;
            Assert.NotNull(document);
            Assert.Equal(OpenApiSpecVersion.OpenApi3_2, document.GetOpenApiSpecVersion());
            Assert.Contains(document.GetAllOperations(), o => o.Operation.OperationId == "searchPets");
            Assert.NotNull(document.Webhooks?["orderShipped"]);
            Assert.True(document.Components!.Schemas!["Pet"].Properties!["name"].Type!.Value.HasFlag(JsonSchemaType.Null));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// <c>spec split</c> followed by the merge the build does gives back the document it split - for every
    /// scenario specification, every strategy, with and without a common file: every path and operation, every
    /// component, webhook, tag, security requirement and extension, in the specification's own OpenAPI version.
    /// </summary>
    [Fact]
    public void Split_ThenMerge_GivesBackTheSameDocument_ForEveryScenario()
    {
        var repositoryRoot = FindRepositoryRoot();
        var specifications = Directory
            .GetDirectories(Path.Combine(repositoryRoot, "test", "Scenarios"))
            .Concat(Directory.GetDirectories(Path.Combine(repositoryRoot, "sample")))
            .SelectMany(d => Directory.GetFiles(d, "*.yaml", SearchOption.AllDirectories))
            .Where(f => !f.Contains("node_modules", StringComparison.Ordinal) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(IsSingleFileSpecification)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(specifications);

        var failures = new List<string>();
        var checkedSpecifications = new List<string>();
        var root = Path.Combine(Path.GetTempPath(), "atc-split-roundtrip", Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var specification in specifications)
            {
                var original = SpecificationService.ReadSingle(specification).Document;
                if (original?.Info is null)
                {
                    // Not an OpenAPI document (e.g. a docker-compose or CI file under sample/).
                    continue;
                }

                checkedSpecifications.Add(Path.GetFileName(specification));

                var expected = NormalizedJson(SpecificationService.SerializeToJson(original));
                var baseName = Path.GetFileNameWithoutExtension(specification);

                foreach (var strategy in (SplitStrategy[])[SplitStrategy.ByTag, SplitStrategy.ByPathSegment, SplitStrategy.ByDomain])
                {
                    foreach (var (extractCommon, minOperations) in ((bool, int)[])[(true, 1), (false, 1), (true, 3)])
                    {
                        var label = $"{baseName} {strategy} extractCommon={extractCommon} minOperations={minOperations}";
                        var result = SpecificationService.Split(SpecificationService.ReadSingle(specification).Document!, baseName, strategy, extractCommon, minOperations);
                        var directory = Path.Combine(root, $"{checkedSpecifications.Count}-{baseName}-{strategy}-{extractCommon}-{minOperations}");
                        Directory.CreateDirectory(directory);
                        foreach (var file in result.AllFiles)
                        {
                            File.WriteAllText(Path.Combine(directory, file.FileName), file.Content);
                        }

                        var merge = SpecificationService.ReadAndMerge(Path.Combine(directory, baseName + ".yaml"));
                        if (!merge.IsSuccess || merge.Document is null)
                        {
                            failures.Add($"{label}: merge failed - {string.Join("; ", merge.Diagnostics.Select(d => d.Message))}");
                            continue;
                        }

                        var unexpected = merge.Diagnostics
                            .Where(d => d.Severity != DiagnosticSeverity.Info)
                            .Select(d => d.Message)
                            .ToList();
                        if (unexpected.Count > 0)
                        {
                            failures.Add($"{label}: merge diagnostics - {string.Join("; ", unexpected)}");
                        }

                        var actual = NormalizedJson(SpecificationService.SerializeToJson(merge.Document));
                        var difference = FirstDifference(expected, actual, "$");
                        if (difference is not null)
                        {
                            failures.Add($"{label}: {difference}");
                        }
                    }
                }
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        Assert.Contains("Showcase.yaml", checkedSpecifications, StringComparer.Ordinal);
        Assert.Contains("PetStoreFull.yaml", checkedSpecifications, StringComparer.Ordinal);
        Assert.True(failures.Count == 0, $"{failures.Count} split round trips differ:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void Split_PathWithOperationsOfSeveralTags_StaysWholeInOnePart()
    {
        const string yaml = """
                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  tags: [Pets]
                                  responses:
                                    '200':
                                      description: OK
                                post:
                                  operationId: createPet
                                  tags: [Admin]
                                  responses:
                                    '201':
                                      description: Created
                                delete:
                                  operationId: deletePets
                                  tags: [Admin]
                                  responses:
                                    '204':
                                      description: Deleted
                            """;
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = SpecificationService.Split(doc, "Shop", SplitStrategy.ByTag, extractCommon: false);

        var part = Assert.Single(result.PartFiles);
        Assert.Equal("Admin", part.PartName);
        Assert.Contains("listPets", part.Content, StringComparison.Ordinal);
        Assert.Contains("createPet", part.Content, StringComparison.Ordinal);
        Assert.Contains("deletePets", part.Content, StringComparison.Ordinal);
        var warning = Assert.Single(result.Diagnostics, d => d.RuleId == RuleIdentifiers.SplitPathSpansGroups);
        Assert.Contains("'/pets'", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Split_MinOperations_PutsSmallGroupsTogetherInOther()
    {
        const string yaml = """
                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                                post:
                                  operationId: createPet
                                  responses:
                                    '201':
                                      description: Created
                              /health:
                                get:
                                  operationId: getHealth
                                  responses:
                                    '200':
                                      description: OK
                              /version:
                                get:
                                  operationId: getVersion
                                  responses:
                                    '200':
                                      description: OK
                            """;
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = SpecificationService.Split(doc, "Shop", SplitStrategy.ByPathSegment, extractCommon: false, minOperations: 2);

        Assert.Equal(["Other", "Pets"], result.PartFiles.Select(p => p.PartName!), StringComparer.Ordinal);
        var other = result.PartFiles.Single(p => p.PartName == "Other");
        Assert.Contains("getHealth", other.Content, StringComparison.Ordinal);
        Assert.Contains("getVersion", other.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadFromFile_ExistingFile_ReadsAndParsesIt()
    {
        var directory = CreateTempDirectory("atc-read-from-file");
        try
        {
            var filePath = Path.Combine(directory, "Shop.yaml");
            File.WriteAllText(filePath, """
                openapi: 3.0.3
                info:
                  title: Shop
                  version: 1.0.0
                paths:
                  /orders:
                    get:
                      operationId: listOrders
                      responses:
                        '200':
                          description: OK
                """);

            var file = SpecificationService.ReadFromFile(filePath);

            Assert.Equal(filePath, file.FilePath);
            Assert.Equal("Shop.yaml", file.FileName);
            Assert.NotNull(file.Document);
            Assert.Equal("Shop", file.Document.Info.Title);
            Assert.Equal(1, file.OperationCount);
            Assert.True(file.IsBaseFile);
            Assert.Contains("operationId: listOrders", file.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReadFromFile_MissingFile_ReturnsAnEmptySpecification()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "atc-read-from-file", Guid.NewGuid().ToString("N"), "Missing.yaml");

        var file = SpecificationService.ReadFromFile(filePath);

        Assert.Equal(filePath, file.FilePath);
        Assert.Null(file.Document);
        Assert.Equal(string.Empty, file.Content);
        Assert.True(file.IsBaseFile);
    }

    [Fact]
    public void DiscoverPartFiles_BaseWithTwoParts_ReturnsThePartsSortedAndNotTheBase()
    {
        var directory = CreateTempDirectory("atc-discover-parts");
        try
        {
            var basePath = Path.Combine(directory, "MyApi.yaml");
            File.WriteAllText(basePath, """
                openapi: 3.0.3
                info:
                  title: My API
                  version: 1.0.0
                paths: {}
                """);
            File.WriteAllText(Path.Combine(directory, "MyApi_Users.yaml"), """
                openapi: 3.0.3
                paths:
                  /users:
                    get:
                      operationId: listUsers
                      responses:
                        '200':
                          description: OK
                """);
            File.WriteAllText(Path.Combine(directory, "MyApi_Orders.yaml"), """
                openapi: 3.0.3
                paths:
                  /orders:
                    get:
                      operationId: listOrders
                      responses:
                        '200':
                          description: OK
                """);

            // A part may be JSON (the build reads .yaml, .yml and .json parts alike); a different base name is no part
            File.WriteAllText(Path.Combine(directory, "OtherApi_Users.yaml"), "openapi: 3.0.3\npaths: {}\n");
            File.WriteAllText(Path.Combine(directory, "MyApi_Legacy.json"), """{ "openapi": "3.0.3", "paths": { "/legacy": { "get": { "operationId": "listLegacy", "responses": { "200": { "description": "OK" } } } } } }""");

            var parts = SpecificationService.DiscoverPartFiles(basePath);

            Assert.Equal(["MyApi_Legacy.json", "MyApi_Orders.yaml", "MyApi_Users.yaml"], parts.Select(p => p.FileName), StringComparer.Ordinal);
            Assert.All(parts, p => Assert.True(p.IsPartFile));
            Assert.All(parts, p => Assert.False(p.IsBaseFile));
            Assert.Equal(["Legacy", "Orders", "Users"], parts.Select(p => p.PartName), StringComparer.Ordinal);
            Assert.All(parts, p => Assert.Equal(1, p.OperationCount));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DiscoverPartFiles_BaseWithoutParts_ReturnsEmpty()
    {
        var directory = CreateTempDirectory("atc-discover-parts");
        try
        {
            var basePath = Path.Combine(directory, "MyApi.yaml");
            File.WriteAllText(basePath, "openapi: 3.0.3\ninfo:\n  title: My API\n  version: 1.0.0\npaths: {}\n");

            var parts = SpecificationService.DiscoverPartFiles(basePath);

            Assert.Empty(parts);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Validate_NoneStrategy_ReportsNothing()
    {
        const string yaml = """
                            openapi: 3.0.3
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  responses:
                                    '200':
                                      description: OK
                            """;
        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = SpecificationService.Validate(document, "test.yaml", ValidateSpecificationStrategy.None, yaml);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Validate_StandardStrategy_WarnsAboutAMissingOperationId()
    {
        const string yaml = """
                            openapi: 3.0.3
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  responses:
                                    '200':
                                      description: OK
                            """;
        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = SpecificationService.Validate(document, "test.yaml");

        var warning = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.OperationIdMissing);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Equal("test.yaml", warning.FilePath);
        Assert.Equal("GET /pets", warning.Context);
    }

    [Fact]
    public void Validate_StrictStrategyWithSourceText_RunsTheSpecVersionRulesOnTheText()
    {
        // ATC_API_VER001 reads the raw text: the parser drops a 'nullable' that has no sibling 'type'.
        const string yaml = """
                            openapi: 3.0.3
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    owner:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/Owner'
                                Owner:
                                  type: object
                            """;
        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var withText = SpecificationService.Validate(document, "test.yaml", ValidateSpecificationStrategy.Strict, yaml);
        var withoutText = SpecificationService.Validate(document, "test.yaml", ValidateSpecificationStrategy.Strict);

        var diagnostic = Assert.Single(withText, d => d.RuleId == RuleIdentifiers.NullableWithoutSiblingType);
        Assert.Equal(12, diagnostic.LineNumber);
        Assert.Equal("test.yaml", diagnostic.FilePath);
        Assert.DoesNotContain(withoutText, d => d.RuleId == RuleIdentifiers.NullableWithoutSiblingType);
    }

    /// <summary>
    /// A specification that is neither a part file (<c>{Base}_{Part}.yaml</c> next to <c>{Base}.yaml</c>) nor the
    /// base of part files.
    /// </summary>
    private static bool IsSingleFileSpecification(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        var name = Path.GetFileNameWithoutExtension(filePath);
        var separator = name.IndexOf('_', StringComparison.Ordinal);
        var isPart = separator > 0 && File.Exists(Path.Combine(directory, name[..separator] + ".yaml"));
        var hasParts = Directory.GetFiles(directory, name + "_*.yaml").Length > 0;
        return !isPart && !hasParts;
    }

    private static string CreateTempDirectory(string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "test", "Scenarios")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("test/Scenarios not found above " + AppContext.BaseDirectory);
    }

    private static JsonNode? NormalizedJson(string json)
        => Sort(JsonNode.Parse(json));

    private static JsonNode? Sort(JsonNode? node)
        => node switch
        {
            JsonObject obj => new JsonObject(obj
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => KeyValuePair.Create(p.Key, Sort(p.Value)))),
            JsonArray array => new JsonArray(array.Select(Sort).ToArray()),
            null => null,
            _ => node.DeepClone(),
        };

    private static string? FirstDifference(
        JsonNode? expected,
        JsonNode? actual,
        string path)
    {
        switch (expected, actual)
        {
            case (JsonObject e, JsonObject a):
                foreach (var key in e.Select(p => p.Key).Union(a.Select(p => p.Key), StringComparer.Ordinal))
                {
                    if (!e.ContainsKey(key))
                    {
                        return $"{path}.{key} was added";
                    }

                    if (!a.ContainsKey(key))
                    {
                        return $"{path}.{key} was lost";
                    }

                    var difference = FirstDifference(e[key], a[key], $"{path}.{key}");
                    if (difference is not null)
                    {
                        return difference;
                    }
                }

                return null;
            case (JsonArray e, JsonArray a):
                if (e.Count != a.Count)
                {
                    return $"{path} has {a.Count} items, expected {e.Count}";
                }

                for (var i = 0; i < e.Count; i++)
                {
                    var difference = FirstDifference(e[i], a[i], $"{path}[{i}]");
                    if (difference is not null)
                    {
                        return difference;
                    }
                }

                return null;
            default:
                return JsonNode.DeepEquals(expected, actual)
                    ? null
                    : $"{path} is {actual?.ToJsonString() ?? "null"}, expected {expected?.ToJsonString() ?? "null"}";
        }
    }
}