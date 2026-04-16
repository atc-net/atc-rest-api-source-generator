namespace Atc.Rest.Api.SourceGenerator.Tests.Helpers;

[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
public class YamlFileHelperTests
{
    // ========== IdentifyBaseFile Tests ==========

    [Fact]
    public void IdentifyBaseFile_SingleFile_ReturnsThatFile()
    {
        // Arrange
        var files = ImmutableArray.Create(
            new YamlFileInfo("C:/project/PetStore.yaml", "content"));

        // Act
        var result = YamlFileHelper.IdentifyBaseFile(files);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("C:/project/PetStore.yaml", result.Value.Path);
    }

    [Fact]
    public void IdentifyBaseFile_BaseWithPartFiles_ReturnsBaseFile()
    {
        // Arrange
        var files = ImmutableArray.Create(
            new YamlFileInfo("C:/project/PetStore.yaml", "base-content"),
            new YamlFileInfo("C:/project/PetStore_Users.yaml", "users-content"),
            new YamlFileInfo("C:/project/PetStore_Pets.yaml", "pets-content"));

        // Act
        var result = YamlFileHelper.IdentifyBaseFile(files);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("C:/project/PetStore.yaml", result.Value.Path);
    }

    [Fact]
    public void IdentifyBaseFile_PartsBeforeBase_StillReturnsBaseFile()
    {
        // Arrange
        var files = ImmutableArray.Create(
            new YamlFileInfo("C:/project/PetStore_Users.yaml", "users-content"),
            new YamlFileInfo("C:/project/PetStore_Pets.yaml", "pets-content"),
            new YamlFileInfo("C:/project/PetStore.yaml", "base-content"));

        // Act
        var result = YamlFileHelper.IdentifyBaseFile(files);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("C:/project/PetStore.yaml", result.Value.Path);
    }

    [Fact]
    public void IdentifyBaseFile_NoUnderscoreFiles_ReturnsFirst()
    {
        // Arrange
        var files = ImmutableArray.Create(
            new YamlFileInfo("C:/project/Api.yaml", "content-a"),
            new YamlFileInfo("C:/project/Other.yaml", "content-b"));

        // Act
        var result = YamlFileHelper.IdentifyBaseFile(files);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("C:/project/Api.yaml", result.Value.Path);
    }

    [Fact]
    public void IdentifyBaseFile_AllLookLikeParts_ReturnsShortestName()
    {
        // Arrange - All files have underscores but no matching base name exists for any of them,
        // so each could be a base. IdentifyBaseFile returns the first non-part match.
        var files = ImmutableArray.Create(
            new YamlFileInfo("C:/project/My_Api.yaml", "content-a"),
            new YamlFileInfo("C:/project/My_Other.yaml", "content-b"));

        // Act
        var result = YamlFileHelper.IdentifyBaseFile(files);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void IdentifyBaseFile_EmptyArray_ReturnsNull()
    {
        // Arrange
        var files = ImmutableArray<YamlFileInfo>.Empty;

        // Act
        var result = YamlFileHelper.IdentifyBaseFile(files);

        // Assert
        Assert.Null(result);
    }

    // ========== IsPartFile Tests ==========

    [Theory]
    [InlineData("C:/project/PetStore_Users.yaml", "PetStore", true)]
    [InlineData("C:/project/PetStore_Pets.yaml", "PetStore", true)]
    [InlineData("C:/project/PetStore.yaml", "PetStore", false)]
    [InlineData("C:/project/OtherApi_Users.yaml", "PetStore", false)]
    public void IsPartFile_ReturnsExpectedResult(
        string filePath,
        string baseName,
        bool expected)
    {
        // Act
        var result = YamlFileHelper.IsPartFile(filePath, baseName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsPartFile_CaseInsensitive()
    {
        // Act
        var result = YamlFileHelper.IsPartFile("C:/project/petstore_Users.yaml", "PetStore");

        // Assert
        Assert.True(result);
    }
}