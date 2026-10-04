namespace Atc.Rest.Api.Generator.Cli.Tests.Services.Migration;

/// <summary>
/// The marker files a migration writes hold only values the generators know: an unknown value is read as a fallback
/// without a word, so the migrated project would not get the setting the old configuration asked for.
/// </summary>
public sealed class MarkerFileGeneratorTests : IDisposable
{
    private readonly string root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "atc-migrate-markers-" + Guid.NewGuid().ToString("N"))).FullName;

    [Theory]
    [InlineData(null, "Standard")]
    [InlineData(false, "Standard")]
    [InlineData(true, "Strict")]
    public void EveryMarker_ValidateSpecificationStrategy_IsAKnownValue(
        bool? strictMode,
        string expected)
    {
        var options = new GeneratorOptionsResult { StrictMode = strictMode };

        string[] markers =
        [
            MarkerFileGenerator.GenerateServerMarker(root, options, "Contoso"),
            MarkerFileGenerator.GenerateClientMarker(root, options, "Contoso"),
            MarkerFileGenerator.GenerateHandlerMarker(root, options, "Contoso"),
        ];

        foreach (var marker in markers)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(marker));
            var value = document.RootElement.GetProperty("validateSpecificationStrategy");

            Assert.True(new ValidateSpecificationStrategyTypeConverter().Accepts(value), $"{Path.GetFileName(marker)}: {value}");
            Assert.Equal(expected, value.GetString());
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}