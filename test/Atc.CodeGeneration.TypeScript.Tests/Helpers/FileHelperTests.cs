namespace Atc.CodeGeneration.TypeScript.Tests.Helpers;

public class FileHelperTests
{
    [Fact]
    public void WriteTsFile_MissingDirectory_CreatesItAndWritesTrimmedUtf8WithoutBom()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "atc-ts-" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
        var filePath = Path.Combine(root, "models", "pet.ts");

        try
        {
            // Act
            FileHelper.WriteTsFile(filePath, "export interface Pet { name: string; }   \n\n");

            // Assert
            Assert.True(File.Exists(filePath));
            Assert.Equal("export interface Pet { name: string; }", File.ReadAllText(filePath));

            var bytes = File.ReadAllBytes(filePath);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void WriteTsFile_EmptyPath_Throws()
        => Assert.Throws<ArgumentNullException>(() => FileHelper.WriteTsFile(string.Empty, "x"));
}