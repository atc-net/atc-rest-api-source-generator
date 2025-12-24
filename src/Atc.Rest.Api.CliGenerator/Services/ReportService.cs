namespace Atc.Rest.Api.CliGenerator.Services;

/// <summary>
/// Service for generating generation reports to console and file.
/// </summary>
public static class ReportService
{
    /// <summary>
    /// Writes a compact generation report to the console using Spectre.Console.
    /// </summary>
    /// <param name="stats">The generation statistics.</param>
    public static void WriteToConsole(GenerationStatistics stats)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[blue]Generation Summary[/] ({Markup.Escape(stats.SpecificationName)}):");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Category")
            .AddColumn(new TableColumn("Count").RightAligned());

        if (stats.GeneratorType == "Server")
        {
            AddServerRows(table, stats);
        }
        else
        {
            AddClientRows(table, stats);
        }

        table.AddEmptyRow();
        table.AddRow(
            new Markup("[bold]Total[/]"),
            new Markup($"[bold]{stats.TotalTypesGenerated.ToString(CultureInfo.InvariantCulture)}[/]"));

        AnsiConsole.Write(table);

        WriteValidationStatus(stats);

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Writes a detailed generation report to a markdown file.
    /// </summary>
    /// <param name="stats">The generation statistics.</param>
    /// <param name="filePath">The path to write the report file.</param>
    public static void WriteToFile(
        GenerationStatistics stats,
        string filePath)
    {
        var markdown = FormatAsMarkdown(stats);
        File.WriteAllText(filePath, markdown);
    }

    /// <summary>
    /// Formats the generation statistics as a markdown string.
    /// </summary>
    /// <param name="stats">The generation statistics.</param>
    /// <returns>A markdown-formatted report string.</returns>
    public static string FormatAsMarkdown(GenerationStatistics stats)
    {
        var sb = new StringBuilder();

        AppendHeader(sb, stats);
        AppendSpecificationSection(sb, stats);
        AppendGeneratorSection(sb, stats);
        AppendGeneratedTypesSection(sb, stats);
        AppendValidationSection(sb, stats);
        AppendProjectsSection(sb, stats);
        AppendFooter(sb);

        return sb.ToString();
    }

    private static void AddServerRows(
        Table table,
        GenerationStatistics stats)
    {
        AddRowIfPositive(table, "Models", stats.ModelsCount);
        AddRowIfPositive(table, "Enums", stats.EnumsCount);
        AddRowIfPositive(table, "Parameters", stats.ParametersCount);
        AddRowIfPositive(table, "Results", stats.ResultsCount);
        AddRowIfPositive(table, "Handlers", stats.HandlersCount);
        AddRowIfPositive(table, "Endpoints", stats.EndpointsCount);
    }

    private static void AddClientRows(
        Table table,
        GenerationStatistics stats)
    {
        AddRowIfPositive(table, "Models", stats.ModelsCount);
        AddRowIfPositive(table, "Enums", stats.EnumsCount);
        AddRowIfPositive(table, "Client Methods", stats.ClientMethodsCount);
        AddRowIfPositive(table, "Endpoint Classes", stats.EndpointClassesCount);
    }

    private static void WriteValidationStatus(GenerationStatistics stats)
    {
        if (stats.ErrorCount == 0 && stats.WarningCount == 0)
        {
            AnsiConsole.MarkupLine("[green]\u2713[/] Validation: No issues");
        }
        else if (stats.ErrorCount == 0)
        {
            var warningIds = stats.WarningRuleIds.Count > 0
                ? $" ({string.Join(", ", stats.WarningRuleIds)})"
                : string.Empty;
            AnsiConsole.MarkupLine($"[green]\u2713[/] Validation: 0 errors, [yellow]{stats.WarningCount.ToString(CultureInfo.InvariantCulture)} warnings{warningIds}[/]");
        }
        else
        {
            var errorIds = stats.ErrorRuleIds.Count > 0
                ? $" ({string.Join(", ", stats.ErrorRuleIds)})"
                : string.Empty;
            AnsiConsole.MarkupLine($"[red]\u2717[/] Validation: [red]{stats.ErrorCount.ToString(CultureInfo.InvariantCulture)} errors{errorIds}[/], [yellow]{stats.WarningCount.ToString(CultureInfo.InvariantCulture)} warnings[/]");
        }
    }

    private static void AppendHeader(
        StringBuilder sb,
        GenerationStatistics stats)
    {
        sb.AppendLine("# ATC REST API Generation Report");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Generated:** {stats.GeneratedAt:O}");
        if (stats.Duration > TimeSpan.Zero)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Duration:** {stats.Duration.TotalSeconds:F2}s");
        }

        sb.AppendLine();
    }

    private static void AppendSpecificationSection(
        StringBuilder sb,
        GenerationStatistics stats)
    {
        sb.AppendLine("## Specification");
        sb.AppendLine();
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| File | {stats.SpecificationName} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| OpenAPI Version | {stats.OpenApiVersion} |");
        if (!string.IsNullOrEmpty(stats.ApiTitle))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"| API Title | {stats.ApiTitle} |");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"| API Version | {stats.SpecificationVersion} |");
        sb.AppendLine();
    }

    private static void AppendGeneratorSection(
        StringBuilder sb,
        GenerationStatistics stats)
    {
        sb.AppendLine("## Generator");
        sb.AppendLine();
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine("| Generator | Atc.Rest.Api.SourceGenerator |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Version | {stats.GeneratorVersion} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Type | {stats.GeneratorType} |");
        if (!string.IsNullOrEmpty(stats.ProjectStructure))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"| Project Structure | {stats.ProjectStructure} |");
        }

        sb.AppendLine();
    }

    private static void AppendGeneratedTypesSection(
        StringBuilder sb,
        GenerationStatistics stats)
    {
        sb.AppendLine("## Generated Types");
        sb.AppendLine();
        sb.AppendLine("| Category | Count |");
        sb.AppendLine("|----------|------:|");

        if (stats.GeneratorType == "Server")
        {
            AppendRowIfPositive(sb, "Models", stats.ModelsCount);
            AppendRowIfPositive(sb, "Enums", stats.EnumsCount);
            AppendRowIfPositive(sb, "Parameters", stats.ParametersCount);
            AppendRowIfPositive(sb, "Results", stats.ResultsCount);
            AppendRowIfPositive(sb, "Handlers", stats.HandlersCount);
            AppendRowIfPositive(sb, "Endpoints", stats.EndpointsCount);
        }
        else
        {
            AppendRowIfPositive(sb, "Models", stats.ModelsCount);
            AppendRowIfPositive(sb, "Enums", stats.EnumsCount);
            AppendRowIfPositive(sb, "Client Methods", stats.ClientMethodsCount);
            AppendRowIfPositive(sb, "Endpoint Classes", stats.EndpointClassesCount);
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"| **Total** | **{stats.TotalTypesGenerated}** |");
        sb.AppendLine();
    }

    private static void AppendValidationSection(
        StringBuilder sb,
        GenerationStatistics stats)
    {
        sb.AppendLine("## Validation Summary");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Errors:** {stats.ErrorCount}");
        if (stats.ErrorRuleIds.Count > 0)
        {
            foreach (var ruleId in stats.ErrorRuleIds)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  - `{ruleId}`");
            }
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Warnings:** {stats.WarningCount}");
        if (stats.WarningRuleIds.Count > 0)
        {
            foreach (var ruleId in stats.WarningRuleIds)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  - `{ruleId}`");
            }
        }

        sb.AppendLine();
    }

    private static void AppendProjectsSection(
        StringBuilder sb,
        GenerationStatistics stats)
    {
        if (stats.ProjectsCreated.Count > 0)
        {
            sb.AppendLine("## Projects Created");
            sb.AppendLine();
            foreach (var project in stats.ProjectsCreated)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- `{project}`");
            }

            sb.AppendLine();
        }
    }

    private static void AppendFooter(StringBuilder sb)
    {
        sb.AppendLine("---");
        sb.AppendLine("*Generated by [ATC REST API Source Generator](https://github.com/atc-net/atc-rest-api-generator)*");
    }

    private static void AddRowIfPositive(
        Table table,
        string category,
        int count)
    {
        if (count > 0)
        {
            table.AddRow(category, count.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AppendRowIfPositive(
        StringBuilder sb,
        string category,
        int count)
    {
        if (count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {category} | {count} |");
        }
    }
}