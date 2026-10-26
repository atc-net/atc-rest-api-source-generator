namespace Atc.Rest.Api.Generator.Tests.Models;

public class SpecificationFileTests
{
    private const string BaseYaml = """
        openapi: 3.0.3
        info:
          title: Shop
          version: 1.0.0
        tags:
          - name: orders
        paths:
          /orders:
            get:
              operationId: listOrders
              responses:
                '200':
                  description: OK
            post:
              operationId: createOrder
              responses:
                '201':
                  description: Created
        components:
          schemas:
            Order:
              type: object
        """;

    private const string PartYaml = """
        openapi: 3.0.3
        paths:
          /users:
            get:
              operationId: listUsers
              responses:
                '200':
                  description: OK
        """;

    [Fact]
    public void FromFile_ExistingYamlFile_ReadsContentAndParsesTheDocument()
    {
        var directory = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(directory, "Shop.yaml");
            File.WriteAllText(filePath, BaseYaml);

            var file = SpecificationFile.FromFile(filePath);

            Assert.Equal(filePath, file.FilePath);
            Assert.Equal("Shop.yaml", file.FileName);
            Assert.Equal(BaseYaml, file.Content);
            Assert.NotNull(file.Document);
            Assert.True(file.IsBaseFile);
            Assert.False(file.IsPartFile);
            Assert.Null(file.PartName);
            Assert.Equal(1, file.PathCount);
            Assert.Equal(2, file.OperationCount);
            Assert.Equal(1, file.SchemaCount);
            Assert.Equal(["orders"], file.GetTags(), StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FromFile_WithBaseName_MarksAPartFileAndExtractsThePartName()
    {
        var directory = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(directory, "Shop_Users.yaml");
            File.WriteAllText(filePath, PartYaml);

            var file = SpecificationFile.FromFile(filePath, "Shop");

            Assert.False(file.IsBaseFile);
            Assert.True(file.IsPartFile);
            Assert.Equal("Users", file.PartName);
            Assert.NotNull(file.Document);
            Assert.Equal(1, file.PathCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FromFile_UnparsableYaml_KeepsTheContentWithANullDocument()
    {
        var directory = CreateTempDirectory();
        try
        {
            var filePath = Path.Combine(directory, "Broken.yaml");
            File.WriteAllText(filePath, "openapi: [3.0.3\ninfo: {");

            var file = SpecificationFile.FromFile(filePath);

            Assert.Null(file.Document);
            Assert.Equal("openapi: [3.0.3\ninfo: {", file.Content);
            Assert.Equal(0, file.PathCount);
            Assert.Empty(file.GetTags());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FromDocument_ParsedDocument_IsABaseFileCarryingThatDocument()
    {
        var document = OpenApiDocumentHelper.ParseYaml(BaseYaml);

        var path = Path.Combine(Path.GetTempPath(), "specs", "Shop.yaml");

        var file = SpecificationFile.FromDocument(path, BaseYaml, document);

        Assert.Equal(path, file.FilePath);
        Assert.Equal("Shop.yaml", file.FileName);
        Assert.Equal(BaseYaml, file.Content);
        Assert.Same(document, file.Document);
        Assert.True(file.IsBaseFile);
        Assert.False(file.IsPartFile);
        Assert.Null(file.PartName);
        Assert.Equal(2, file.OperationCount);
        Assert.Equal(1, file.SchemaCount);
    }

    [Fact]
    public void FromDocument_PartLikeFileName_IsStillABaseFile()
    {
        // The merged document stands for the whole specification, whatever path it is given.
        var document = OpenApiDocumentHelper.ParseYaml(BaseYaml);

        var file = SpecificationFile.FromDocument("Shop_Users.yaml", BaseYaml, document);

        Assert.True(file.IsBaseFile);
        Assert.False(file.IsPartFile);
        Assert.Null(file.PartName);
    }

    [Fact]
    public void Empty_FilePath_HasNoContentAndNoDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), "specs", "Missing.yaml");

        var file = SpecificationFile.Empty(path);

        Assert.Equal(path, file.FilePath);
        Assert.Equal("Missing.yaml", file.FileName);
        Assert.Equal(string.Empty, file.Content);
        Assert.Null(file.Document);
        Assert.True(file.IsBaseFile);
        Assert.False(file.IsPartFile);
        Assert.Null(file.PartName);
        Assert.Equal(0, file.PathCount);
        Assert.Equal(0, file.SchemaCount);
        Assert.Equal(0, file.OperationCount);
        Assert.Empty(file.GetTags());
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "atc-specification-file", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}