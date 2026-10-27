namespace Atc.Rest.Api.Generator.Tests.Models;

public class MergeResultTests
{
    private const string BaseYaml = """
        openapi: 3.0.3
        info:
          title: Shop
          version: 1.0.0
        paths:
          /health:
            get:
              operationId: getHealth
              responses:
                '200':
                  description: OK
        components:
          schemas:
            Error:
              type: object
        """;

    private const string PartYaml = """
        openapi: 3.0.3
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

    [Fact]
    public void Success_BaseAndOnePart_IsAMultiPartSuccessWithTheMergedDocument()
    {
        var baseFile = SpecificationFile.FromContent("Shop.yaml", BaseYaml, "Shop");
        var partFile = SpecificationFile.FromContent("Shop_Orders.yaml", PartYaml, "Shop");
        var merged = SpecificationService.MergeSpecifications(baseFile, [partFile]).Document!;
        var info = DiagnosticMessage.Info(RuleIdentifiers.MultiPartMergeSuccessful, "merged");

        var result = MergeResult.Success(merged, baseFile, [partFile], [info]);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsMultiPart);
        Assert.Same(merged, result.Document);
        Assert.Same(baseFile, result.BaseFile);
        Assert.Equal([partFile], result.PartFiles);
        Assert.Equal([baseFile, partFile], result.AllFiles);
        Assert.Equal([info], result.Diagnostics);
        Assert.Equal(2, result.TotalPaths);
        Assert.Equal(3, result.TotalOperations);
        Assert.Equal(2, result.TotalSchemas);
    }

    [Fact]
    public void Success_NoParts_IsNotMultiPartAndAllFilesIsJustTheBase()
    {
        var baseFile = SpecificationFile.FromContent("Shop.yaml", BaseYaml);

        var result = MergeResult.Success(baseFile.Document!, baseFile, [], []);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsMultiPart);
        Assert.Equal([baseFile], result.AllFiles);
        Assert.Equal(1, result.TotalPaths);
        Assert.Equal(1, result.TotalOperations);
        Assert.Equal(1, result.TotalSchemas);
    }

    [Fact]
    public void Success_WithAnErrorDiagnostic_IsNotSuccessful()
    {
        // IsSuccess is about the diagnostics, not the factory used: an error among them means failure.
        var baseFile = SpecificationFile.FromContent("Shop.yaml", BaseYaml);
        var error = DiagnosticMessage.Error(RuleIdentifiers.DuplicatePathInPart, "Duplicate path '/orders'");

        var result = MergeResult.Success(baseFile.Document!, baseFile, [], [error]);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Document);
    }

    [Fact]
    public void Failed_Diagnostics_HasNoDocumentNoFilesAndKeepsTheDiagnostics()
    {
        var error = DiagnosticMessage.Error(RuleIdentifiers.ServerParsingError, "Failed to parse part file: Shop_Orders.yaml", "Shop_Orders.yaml");
        var warning = DiagnosticMessage.Warning(RuleIdentifiers.PartFileInfoIgnored, "info ignored");

        var result = MergeResult.Failed([error, warning]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Document);
        Assert.Null(result.BaseFile);
        Assert.Empty(result.PartFiles);
        Assert.Empty(result.AllFiles);
        Assert.False(result.IsMultiPart);
        Assert.Equal([error, warning], result.Diagnostics);
        Assert.Equal(0, result.TotalPaths);
        Assert.Equal(0, result.TotalOperations);
        Assert.Equal(0, result.TotalSchemas);
    }

    [Fact]
    public void Failed_RuleIdMessageAndFilePath_WrapsOneErrorDiagnostic()
    {
        var result = MergeResult.Failed(RuleIdentifiers.BaseFileNotFound, "Base file not found: Shop.yaml", "Shop.yaml");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Document);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(RuleIdentifiers.BaseFileNotFound, diagnostic.RuleId);
        Assert.Equal("Base file not found: Shop.yaml", diagnostic.Message);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Shop.yaml", diagnostic.FilePath);
        Assert.Null(diagnostic.LineNumber);
    }

    [Fact]
    public void Failed_RuleIdAndMessageOnly_LeavesTheFilePathNull()
    {
        var result = MergeResult.Failed(RuleIdentifiers.ServerParsingError, "Base file has no valid document");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(RuleIdentifiers.ServerParsingError, diagnostic.RuleId);
        Assert.Null(diagnostic.FilePath);
    }
}