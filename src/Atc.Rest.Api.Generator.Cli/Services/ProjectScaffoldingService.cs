namespace Atc.Rest.Api.Generator.Cli.Services;

/// <summary>
/// Service for scaffolding repository structure, solution files, and project templates.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class ProjectScaffoldingService
{
    private const string CodingRulesJson = """
        {
          "projectTarget": "DotNet10",
          "useLatestMinorNugetVersion": true,
          "useTemporarySuppressions": false,
          "temporarySuppressionAsExcel": false,
          "analyzerProviderCollectingMode": "LocalCache",
          "mappings": {
            "src": {
              "paths": [
                "src"
              ]
            },
            "test": {
              "paths": [
                "test"
              ]
            }
          }
        }
        """;

    private const string CodingRulesPs1 = """
        Clear-Host
        Write-Host "Updating atc-coding-rules-updater tool to newest version"
        dotnet tool update -g atc-coding-rules-updater

        $currentPath = Get-Location
        Write-Host "Running atc-coding-rules-updater to fetch updated rulesets and configurations"
        atc-coding-rules-updater `
            run `
            -p $currentPath `
            --optionsPath $currentPath'\atc-coding-rules-updater.json' `
            --verbose
        """;

    private const string GitIgnoreContent = """
        # Build results
        [Dd]ebug/
        [Rr]elease/
        x64/
        x86/
        [Ww][Ii][Nn]32/
        [Aa][Rr][Mm]/
        [Aa][Rr][Mm]64/
        bld/
        [Bb]in/
        [Oo]bj/
        [Oo]ut/
        [Ll]og/
        [Ll]ogs/

        # Visual Studio files
        .vs/
        *.suo
        *.user
        *.sln.docstates
        *.userprefs

        # Rider
        .idea/

        # NuGet packages
        packages/
        *.nupkg

        # Test results
        [Tt]est[Rr]esult*/
        [Bb]uild[Ll]og.*

        # NCrunch
        _NCrunch_*
        .*crunch*.local.xml
        nCrunchTemp_*

        # Misc
        *.swp
        *~
        .DS_Store
        Thumbs.db
        """;

    /// <summary>
    /// Creates the base repository structure with src/ and test/ directories.
    /// </summary>
    /// <param name="repoPath">Root path for the repository.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool CreateDirectoryStructure(string repoPath)
    {
        try
        {
            var srcPath = Path.Combine(repoPath, "src");
            var testPath = Path.Combine(repoPath, "test");

            if (!Directory.Exists(srcPath))
            {
                TrackedFile.CreateDirectory(srcPath);
                AnsiConsole.MarkupLine("[green]✓[/] Created directory: src/");
            }

            if (!Directory.Exists(testPath))
            {
                TrackedFile.CreateDirectory(testPath);
                AnsiConsole.MarkupLine("[green]✓[/] Created directory: test/");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error creating directory structure: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generates a .slnx solution file.
    /// </summary>
    /// <param name="repoPath">Root path for the repository.</param>
    /// <param name="projectName">Name of the project (used for solution name).</param>
    /// <param name="srcProjects">List of source project paths relative to the solution file.</param>
    /// <param name="testProjects">List of test project paths relative to the solution file (optional).</param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool GenerateSolutionFile(
        string repoPath,
        string projectName,
        IEnumerable<string> srcProjects,
        IEnumerable<string>? testProjects = null)
    {
        var solutionName = ProjectNameHelper.ExtractSolutionName(projectName);
        var slnxPath = Path.Combine(repoPath, $"{solutionName}.slnx");

        try
        {
            var srcProjectEntries = string.Join(
                Environment.NewLine,
                srcProjects.Select(p => $"    <Project Path=\"{p}\" />"));

            var sb = new StringBuilder();
            sb.AppendLine("<Solution>");
            sb.AppendLine(2, "<Folder Name=\"/src/\">");
            sb.AppendLine(srcProjectEntries);
            sb.AppendLine(2, "</Folder>");

            if (testProjects is not null)
            {
                var arrTestProjects = testProjects.ToArray();
                if (arrTestProjects.Any())
                {
                    var testProjectEntries = string.Join(
                        Environment.NewLine,
                        arrTestProjects.Select(p => $"    <Project Path=\"{p}\" />"));
                    sb.AppendLine(2, "<Folder Name=\"/test/\">");
                    sb.AppendLine(testProjectEntries);
                    sb.AppendLine(2, "</Folder>");
                }
            }

            sb.Append("</Solution>");

            if (File.Exists(slnxPath))
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]✓[/] Solution file already exists: {solutionName}.slnx");
                return true;
            }

            TrackedFile.WriteAllText(slnxPath, sb.ToString());
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Created solution file: {solutionName}.slnx");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error creating solution file: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generates a test project with xUnit references.
    /// </summary>
    /// <param name="testPath">Path to the test directory.</param>
    /// <param name="projectName">Base project name (e.g., "Demo.Api").</param>
    /// <param name="targetFramework">Target framework (e.g., "net10.0").</param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool GenerateTestProject(
        string testPath,
        string projectName,
        string targetFramework)
    {
        var testProjectName = $"{ProjectNameHelper.ExtractSolutionName(projectName)}.Tests";
        var testProjectPath = Path.Combine(testPath, testProjectName);
        var csprojPath = Path.Combine(testProjectPath, $"{testProjectName}.csproj");

        try
        {
            if (!Directory.Exists(testProjectPath))
            {
                TrackedFile.CreateDirectory(testProjectPath);
            }

            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateTestProjectContent(projectName);
                TrackedFile.WriteAllText(csprojPath, csprojContent);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Created test project: {testProjectName}.csproj");

                // Create a sample test file
                var testFilePath = Path.Combine(testProjectPath, "UnitTest1.cs");
                if (!File.Exists(testFilePath))
                {
                    var testContent = GenerateTestFileContent(testProjectName);
                    TrackedFile.WriteAllText(testFilePath, testContent);
                    AnsiConsole.MarkupLine("[green]✓[/] Created sample test: UnitTest1.cs");
                }
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]✓[/] Test project already exists: {testProjectName}.csproj");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error creating test project: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Adds ATC coding rules updater files to the repository.
    /// </summary>
    /// <param name="repoPath">Root path for the repository.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool AddCodingRulesFiles(string repoPath)
    {
        try
        {
            var jsonPath = Path.Combine(repoPath, "atc-coding-rules-updater.json");
            var ps1Path = Path.Combine(repoPath, "atc-coding-rules-updater.ps1");

            if (!File.Exists(jsonPath))
            {
                TrackedFile.WriteAllText(jsonPath, CodingRulesJson);
                AnsiConsole.MarkupLine("[green]✓[/] Created atc-coding-rules-updater.json");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]✓[/] atc-coding-rules-updater.json already exists");
            }

            if (!File.Exists(ps1Path))
            {
                TrackedFile.WriteAllText(ps1Path, CodingRulesPs1);
                AnsiConsole.MarkupLine("[green]✓[/] Created atc-coding-rules-updater.ps1");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]✓[/] atc-coding-rules-updater.ps1 already exists");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error adding coding rules files: {ex.Message}");
            return false;
        }
    }

    // How long the coding-rules updater may take before it is stopped
    private static readonly TimeSpan CodingRulesUpdaterTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Runs the atc-coding-rules-updater tool to download coding rules and Directory.Build.props.
    /// </summary>
    /// <param name="repoPath">Root path for the repository.</param>
    /// <param name="organizationName">Organization name for initial setup (only used if Directory.Build.props doesn't exist).</param>
    /// <param name="repositoryName">Repository name for initial setup (only used if Directory.Build.props doesn't exist).</param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool RunCodingRulesUpdater(
        string repoPath,
        string? organizationName = null,
        string? repositoryName = null)
    {
        var optionsPath = Path.Combine(repoPath, "atc-coding-rules-updater.json");

        if (!File.Exists(optionsPath))
        {
            AnsiConsole.MarkupLine("[yellow]![/] atc-coding-rules-updater.json not found, skipping");
            return true;
        }

        try
        {
            AnsiConsole.MarkupLine("[dim]Running atc-coding-rules-updater...[/]");

            // organizationName and repositoryName are for the initial setup only: a Directory.Build.props is kept
            var includeNames = !File.Exists(Path.Combine(repoPath, "Directory.Build.props")) &&
                               !string.IsNullOrWhiteSpace(organizationName) &&
                               !string.IsNullOrWhiteSpace(repositoryName);

            // The updater downloads the rules: it gets its time, and then the command goes on without it
            var run = ExternalTool.Run(
                ExecutableResolver.Resolve("atc-coding-rules-updater"),
                BuildCodingRulesUpdaterArguments(repoPath, optionsPath, includeNames, organizationName, repositoryName),
                repoPath,
                CodingRulesUpdaterTimeout);
            var error = run.Error;

            if (run.TimedOut)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]![/] atc-coding-rules-updater did not end within {CodingRulesUpdaterTimeout.TotalMinutes:0} minutes and was stopped");
                AnsiConsole.MarkupLine("[dim]Run './atc-coding-rules-updater.ps1' manually to download coding rules.[/]");
                return true; // Don't fail the whole process, just warn
            }

            if (run.ExitCode != 0)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]![/] atc-coding-rules-updater exited with code {run.ExitCode}");
                if (!string.IsNullOrWhiteSpace(error))
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]{error.Trim()}[/]");
                }

                return true; // Don't fail the whole process, just warn
            }

            AnsiConsole.MarkupLine("[green]✓[/] Coding rules updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]![/] Could not run atc-coding-rules-updater: {ex.Message}");
            AnsiConsole.MarkupLine("[dim]Run './atc-coding-rules-updater.ps1' manually to download coding rules.[/]");
            return true; // Don't fail the whole process, just warn
        }
    }

    /// <summary>
    /// Builds the command line for atc-coding-rules-updater.
    /// </summary>
    /// <param name="repoPath">Root path for the repository.</param>
    /// <param name="optionsPath">The path of atc-coding-rules-updater.json.</param>
    /// <param name="includeNames">Whether to pass the organization and repository names (initial setup).</param>
    /// <param name="organizationName">Organization name for initial setup.</param>
    /// <param name="repositoryName">Repository name for initial setup.</param>
    /// <returns>The arguments.</returns>
    public static string BuildCodingRulesUpdaterArguments(
        string repoPath,
        string optionsPath,
        bool includeNames,
        string? organizationName,
        string? repositoryName)
    {
        // A path ending in a backslash would escape the closing quote of its argument
        var arguments = $"run -p \"{repoPath.TrimEnd('\\', '/')}\" --optionsPath \"{optionsPath.TrimEnd('\\', '/')}\" --verbose";
        if (includeNames)
        {
            arguments += $" --organizationName \"{organizationName}\" --repositoryName \"{repositoryName}\"";
        }

        return arguments;
    }

    /// <summary>
    /// Creates a .gitignore file for .NET projects.
    /// </summary>
    /// <param name="repoPath">Root path for the repository.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool CreateGitIgnore(string repoPath)
    {
        var gitignorePath = Path.Combine(repoPath, ".gitignore");

        if (File.Exists(gitignorePath))
        {
            AnsiConsole.MarkupLine("[dim]✓[/] .gitignore already exists");
            return true;
        }

        try
        {
            TrackedFile.WriteAllText(gitignorePath, GitIgnoreContent);
            AnsiConsole.MarkupLine("[green]✓[/] Created .gitignore");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error creating .gitignore: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generates an API host project that references the contracts and domain projects.
    /// </summary>
    /// <param name="srcPath">Path to the src directory.</param>
    /// <param name="hostProjectName">Name of the host project (e.g., "Demo.Api").</param>
    /// <param name="contractsProjectName">Name of the contracts project.</param>
    /// <param name="domainProjectName">Name of the domain project.</param>
    /// <param name="targetFramework">Target framework (e.g., "net10.0").</param>
    /// <param name="hostUi">API documentation UI type.</param>
    /// <param name="hostUiMode">When to enable the UI.</param>
    /// <param name="hostNamespace">The host project's root namespace; the project name when not set.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool GenerateHostProject(
        string srcPath,
        string hostProjectName,
        string contractsProjectName,
        string domainProjectName,
        string targetFramework,
        HostUiType hostUi,
        HostUiModeType hostUiMode,
        string? hostNamespace = null)
    {
        var hostProjectPath = Path.Combine(srcPath, hostProjectName);
        var csprojPath = Path.Combine(hostProjectPath, $"{hostProjectName}.csproj");
        var programPath = Path.Combine(hostProjectPath, "Program.cs");

        try
        {
            // Create project directory
            if (!Directory.Exists(hostProjectPath))
            {
                TrackedFile.CreateDirectory(hostProjectPath);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Created directory: {hostProjectName}/");
            }

            // Create .csproj file
            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateHostProjectContent(
                    contractsProjectName,
                    domainProjectName,
                    targetFramework,
                    hostUi,
                    hostNamespace);
                TrackedFile.WriteAllText(csprojPath, csprojContent);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Created project file: {hostProjectName}.csproj");
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]✓[/] Project file already exists: {hostProjectName}.csproj");
            }

            // Create Program.cs file
            if (!File.Exists(programPath))
            {
                var baseName = ProjectNameHelper.ExtractSolutionName(hostProjectName);
                var programContent = HostProjectFiles.ProgramContent(baseName, hostUi, hostUiMode);
                TrackedFile.WriteAllText(programPath, programContent);
                AnsiConsole.MarkupLine("[green]✓[/] Created Program.cs");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]✓[/] Program.cs already exists");
            }

            HostProjectFiles.WriteLaunchSettings(hostProjectPath, hostProjectName, hostUi);

            // Create GlobalUsings.cs file
            var globalUsingsPath = Path.Combine(hostProjectPath, "GlobalUsings.cs");
            if (!File.Exists(globalUsingsPath))
            {
                var baseName = ProjectNameHelper.ExtractSolutionName(hostProjectName);
                var globalUsingsContent = HostProjectFiles.GlobalUsingsContent(baseName, hostUi);
                TrackedFile.WriteAllText(globalUsingsPath, globalUsingsContent);
                AnsiConsole.MarkupLine("[green]✓[/] Created GlobalUsings.cs");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]✓[/] GlobalUsings.cs already exists");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error creating host project: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generates an Aspire AppHost project that references the API project.
    /// </summary>
    /// <param name="srcPath">Path to the src directory.</param>
    /// <param name="aspireProjectName">Name of the Aspire project (e.g., "Demo.Aspire").</param>
    /// <param name="apiProjectName">Name of the API project to reference.</param>
    /// <param name="targetFramework">Target framework (e.g., "net10.0").</param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool GenerateAspireProject(
        string srcPath,
        string aspireProjectName,
        string apiProjectName,
        string targetFramework)
    {
        var aspireProjectPath = Path.Combine(srcPath, aspireProjectName);
        var csprojPath = Path.Combine(aspireProjectPath, $"{aspireProjectName}.csproj");
        var programPath = Path.Combine(aspireProjectPath, "Program.cs");

        try
        {
            // Create project directory
            if (!Directory.Exists(aspireProjectPath))
            {
                TrackedFile.CreateDirectory(aspireProjectPath);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Created directory: {aspireProjectName}/");
            }

            // Create .csproj file
            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateAspireProjectContent(apiProjectName, targetFramework);
                TrackedFile.WriteAllText(csprojPath, csprojContent);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Created project file: {aspireProjectName}.csproj");
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]✓[/] Project file already exists: {aspireProjectName}.csproj");
            }

            // Create Program.cs file
            if (!File.Exists(programPath))
            {
                var programContent = GenerateAspireProgramContent(apiProjectName);
                TrackedFile.WriteAllText(programPath, programContent);
                AnsiConsole.MarkupLine("[green]✓[/] Created Program.cs");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]✓[/] Program.cs already exists");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error creating Aspire project: {ex.Message}");
            return false;
        }
    }

    private static string GenerateTestProjectContent(
        string contractsProjectName)
        => $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <IsPackable>false</IsPackable>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>

              <ItemGroup>
                <ProjectReference Include="..\..\src\{contractsProjectName}\{contractsProjectName}.csproj" />
              </ItemGroup>

            </Project>
            """;

    private static string GenerateTestFileContent(string testProjectNamespace)
        => $$"""
            namespace {{testProjectNamespace}};

            public class UnitTest1
            {
                [Fact]
                public void Test1()
                {
                    // Arrange
                    var expected = true;

                    // Act
                    var actual = true;

                    // Assert
                    Assert.Equal(expected, actual);
                }
            }
            """;

    private static string GenerateHostProjectContent(
        string contractsProjectName,
        string domainProjectName,
        string targetFramework,
        HostUiType hostUi,
        string? hostNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk.Web\">");
        sb.AppendLine();
        sb.AppendLine(2, "<PropertyGroup>");
        sb.AppendLine(4, $"<TargetFramework>{targetFramework}</TargetFramework>");

        if (!string.IsNullOrWhiteSpace(hostNamespace))
        {
            sb.AppendLine(4, $"<RootNamespace>{hostNamespace}</RootNamespace>");
        }

        sb.AppendLine(2, "</PropertyGroup>");
        sb.AppendLine();
        sb.AppendLine(2, "<ItemGroup>");
        sb.AppendLine(4, $"<ProjectReference Include=\"..\\{contractsProjectName}\\{contractsProjectName}.csproj\" />");
        sb.AppendLine(4, $"<ProjectReference Include=\"..\\{domainProjectName}\\{domainProjectName}.csproj\" />");
        sb.AppendLine(2, "</ItemGroup>");

        if (hostUi != HostUiType.None)
        {
            sb.AppendLine();
            sb.AppendLine(2, "<ItemGroup>");
            sb.AppendLine(4, "<PackageReference Include=\"Microsoft.AspNetCore.OpenApi\" Version=\"10.*\" />");

            // Microsoft.AspNetCore.OpenApi transitively pulls an old, vulnerable Microsoft.OpenApi
            // 2.0.0 (GHSA-v5pm-xwqc-g5wc). Pin the floor to the latest patched 2.x release — not
            // 3.x, which is a breaking change incompatible with Microsoft.AspNetCore.OpenApi's
            // built-in XmlCommentGenerator (IOpenApiMediaType.Example becomes read-only in 3.x).
            sb.AppendLine(4, "<PackageReference Include=\"Microsoft.OpenApi\" Version=\"2.*\" />");

            switch (hostUi)
            {
                case HostUiType.Scalar:
                    sb.AppendLine(4, "<PackageReference Include=\"Scalar.AspNetCore\" Version=\"2.*\" />");
                    break;
                case HostUiType.Swagger:
                    sb.AppendLine(4, "<PackageReference Include=\"Swashbuckle.AspNetCore.SwaggerUI\" Version=\"7.*\" />");
                    break;
            }

            sb.AppendLine(2, "</ItemGroup>");
        }

        sb.AppendLine();
        sb.Append("</Project>");

        return sb.ToString();
    }

    private static string GenerateAspireProjectContent(
        string apiProjectName,
        string targetFramework)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Aspire.AppHost.Sdk/13.3.2\">");
        sb.AppendLine();
        sb.AppendLine(2, "<PropertyGroup>");
        sb.AppendLine(4, "<OutputType>Exe</OutputType>");
        sb.AppendLine(4, $"<TargetFramework>{targetFramework}</TargetFramework>");
        sb.AppendLine(4, "<Nullable>enable</Nullable>");
        sb.AppendLine(4, "<ImplicitUsings>enable</ImplicitUsings>");
        sb.AppendLine(2, "</PropertyGroup>");
        sb.AppendLine();
        sb.AppendLine(2, "<ItemGroup>");
        sb.AppendLine(4, $"<ProjectReference Include=\"..\\{apiProjectName}\\{apiProjectName}.csproj\" />");
        sb.AppendLine(2, "</ItemGroup>");
        sb.AppendLine();
        sb.Append("</Project>");

        return sb.ToString();
    }

    private static string GenerateAspireProgramContent(string apiProjectName)
    {
        // In Aspire Program.cs, dots in project names are replaced with underscores
        var projectsReference = apiProjectName.Replace(".", "_", StringComparison.Ordinal);

        var sb = new StringBuilder();
        sb.AppendLine("var builder = DistributedApplication.CreateBuilder(args);");
        sb.AppendLine();
        sb.AppendLine("// Add the API project");
        sb.AppendLine("var api = builder");
        sb.AppendLine(4, $".AddProject<Projects.{projectsReference}>(\"api\")");
        sb.AppendLine(4, ".WithExternalHttpEndpoints();");
        sb.AppendLine();
        sb.AppendLine("await builder");
        sb.AppendLine(4, ".Build()");
        sb.AppendLine(4, ".RunAsync()");
        sb.Append(4, ".ConfigureAwait(false);");

        return sb.ToString();
    }
}