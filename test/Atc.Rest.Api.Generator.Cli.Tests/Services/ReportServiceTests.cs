namespace Atc.Rest.Api.Generator.Cli.Tests.Services;

[Collection("AnsiConsoleSerial")]
public class ReportServiceTests
{
    [Fact]
    public void FormatAsMarkdown_ServerStatistics_ListsServerCategoriesAndTotal()
    {
        var stats = new GenerationStatistics
        {
            SpecificationName = "petstore.yaml",
            OpenApiVersion = "3.0.3",
            ApiTitle = "Pet Store",
            ApiVersion = "1.0.0",
            GeneratorVersion = "2.0.0",
            GeneratorType = "Server",
            ProjectStructure = "ThreeProjects",
            ModelsCount = 4,
            EnumsCount = 1,
            HandlersCount = 3,
            EndpointsCount = 3,
            WarningCount = 1,
            WarningRuleIds = ["ATC_W001"],
            ProjectsCreated = ["Demo.Api.Contracts"],
        };

        var markdown = ReportService.FormatAsMarkdown(stats);

        Assert.StartsWith("# ATC REST API Generation Report", markdown, StringComparison.Ordinal);
        Assert.Contains("| File | petstore.yaml |", markdown, StringComparison.Ordinal);
        Assert.Contains("| API Title | Pet Store |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Type | Server |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Project Structure | ThreeProjects |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Handlers | 3 |", markdown, StringComparison.Ordinal);
        Assert.Contains("| **Total** | **11** |", markdown, StringComparison.Ordinal);
        Assert.Contains("- **Warnings:** 1", markdown, StringComparison.Ordinal);
        Assert.Contains("  - `ATC_W001`", markdown, StringComparison.Ordinal);
        Assert.Contains("- `Demo.Api.Contracts`", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("| Parameters |", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Client Methods", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatAsMarkdown_ClientStatistics_ListsClientCategoriesOnly()
    {
        var stats = new GenerationStatistics
        {
            SpecificationName = "petstore.yaml",
            GeneratorType = "Client",
            ModelsCount = 2,
            ClientMethodsCount = 5,
            EndpointClassesCount = 5,
        };

        var markdown = ReportService.FormatAsMarkdown(stats);

        Assert.Contains("| Client Methods | 5 |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Endpoint Classes | 5 |", markdown, StringComparison.Ordinal);
        Assert.Contains("| **Total** | **12** |", markdown, StringComparison.Ordinal);
        Assert.Contains("- **Errors:** 0", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("| Handlers |", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("## Projects Created", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteToFile_WritesTheMarkdownReport()
    {
        var stats = new GenerationStatistics { SpecificationName = "api.yaml", GeneratorType = "Client", ModelsCount = 1 };
        var filePath = Path.Combine(Path.GetTempPath(), "atc-report-" + Guid.NewGuid().ToString("N") + ".md");

        try
        {
            ReportService.WriteToFile(stats, filePath);

            Assert.Equal(ReportService.FormatAsMarkdown(stats), File.ReadAllText(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void WriteToConsole_PrintsSummaryTableAndValidationStatus()
    {
        var stats = new GenerationStatistics
        {
            SpecificationName = "petstore.yaml",
            GeneratorType = "Server",
            ModelsCount = 4,
            EndpointsCount = 3,
            ErrorCount = 2,
            ErrorRuleIds = ["ATC_E010"],
            WarningCount = 1,
        };

        var writer = new StringWriter();
        var original = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });

        try
        {
            ReportService.WriteToConsole(stats);
        }
        finally
        {
            AnsiConsole.Console = original;
        }

        var output = writer.ToString();
        Assert.Contains("Generation Summary (petstore.yaml):", output, StringComparison.Ordinal);
        Assert.Contains("Models", output, StringComparison.Ordinal);
        Assert.Contains("Endpoints", output, StringComparison.Ordinal);
        Assert.Contains("Total", output, StringComparison.Ordinal);
        Assert.Contains("Validation: 2 errors (ATC_E010), 1 warnings", output, StringComparison.Ordinal);
    }
}