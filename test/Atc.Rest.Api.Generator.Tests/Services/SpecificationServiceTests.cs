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
        Assert.Contains("Pets", file.GetTags());
        Assert.Contains("Users", file.GetTags());
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
}