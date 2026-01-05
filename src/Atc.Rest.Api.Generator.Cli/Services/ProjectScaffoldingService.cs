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
                Directory.CreateDirectory(srcPath);
                AnsiConsole.MarkupLine($"[green]✓[/] Created directory: src/");
            }

            if (!Directory.Exists(testPath))
            {
                Directory.CreateDirectory(testPath);
                AnsiConsole.MarkupLine($"[green]✓[/] Created directory: test/");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error creating directory structure: {ex.Message}");
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
        var solutionName = ExtractSolutionName(projectName);
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

            if (testProjects is not null && testProjects.Any())
            {
                var testProjectEntries = string.Join(
                    Environment.NewLine,
                    testProjects.Select(p => $"    <Project Path=\"{p}\" />"));
                sb.AppendLine(2, "<Folder Name=\"/test/\">");
                sb.AppendLine(testProjectEntries);
                sb.AppendLine(2, "</Folder>");
            }

            sb.Append("</Solution>");

            if (File.Exists(slnxPath))
            {
                AnsiConsole.MarkupLine($"[dim]✓[/] Solution file already exists: {solutionName}.slnx");
                return true;
            }

            File.WriteAllText(slnxPath, sb.ToString());
            AnsiConsole.MarkupLine($"[green]✓[/] Created solution file: {solutionName}.slnx");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error creating solution file: {ex.Message}");
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
        string targetFramework = "net10.0")
    {
        var testProjectName = $"{ExtractSolutionName(projectName)}.Tests";
        var testProjectPath = Path.Combine(testPath, testProjectName);
        var csprojPath = Path.Combine(testProjectPath, $"{testProjectName}.csproj");

        try
        {
            if (!Directory.Exists(testProjectPath))
            {
                Directory.CreateDirectory(testProjectPath);
            }

            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateTestProjectContent(
                    targetFramework,
                    projectName);
                File.WriteAllText(csprojPath, csprojContent);
                AnsiConsole.MarkupLine($"[green]✓[/] Created test project: {testProjectName}.csproj");

                // Create a sample test file
                var testFilePath = Path.Combine(testProjectPath, "UnitTest1.cs");
                if (!File.Exists(testFilePath))
                {
                    var testContent = GenerateTestFileContent(testProjectName);
                    File.WriteAllText(testFilePath, testContent);
                    AnsiConsole.MarkupLine($"[green]✓[/] Created sample test: UnitTest1.cs");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]✓[/] Test project already exists: {testProjectName}.csproj");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error creating test project: {ex.Message}");
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
                File.WriteAllText(jsonPath, CodingRulesJson);
                AnsiConsole.MarkupLine("[green]✓[/] Created atc-coding-rules-updater.json");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]✓[/] atc-coding-rules-updater.json already exists");
            }

            if (!File.Exists(ps1Path))
            {
                File.WriteAllText(ps1Path, CodingRulesPs1);
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
            AnsiConsole.MarkupLine($"[red]✗[/] Error adding coding rules files: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Runs the atc-coding-rules-updater.ps1 script to download coding rules and Directory.Build.props.
    /// </summary>
    /// <param name="repoPath">Root path for the repository.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool RunCodingRulesUpdater(string repoPath)
    {
        var ps1Path = Path.Combine(repoPath, "atc-coding-rules-updater.ps1");

        if (!File.Exists(ps1Path))
        {
            AnsiConsole.MarkupLine("[yellow]![/] atc-coding-rules-updater.ps1 not found, skipping");
            return true;
        }

        try
        {
            AnsiConsole.MarkupLine("[dim]Running atc-coding-rules-updater...[/]");

            var startInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"-ExecutionPolicy Bypass -File \"{ps1Path}\"",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            // Fall back to powershell.exe if pwsh is not available
            using var process = new Process { StartInfo = startInfo };

            try
            {
                process.Start();
            }
            catch (Win32Exception)
            {
                // pwsh not found, try powershell.exe
                startInfo.FileName = "powershell";
                process.StartInfo = startInfo;
                process.Start();
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                AnsiConsole.MarkupLine($"[yellow]![/] atc-coding-rules-updater exited with code {process.ExitCode}");
                if (!string.IsNullOrWhiteSpace(error))
                {
                    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(error.Trim())}[/]");
                }

                return true; // Don't fail the whole process, just warn
            }

            AnsiConsole.MarkupLine("[green]✓[/] Coding rules updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]![/] Could not run atc-coding-rules-updater: {ex.Message}");
            AnsiConsole.MarkupLine("[dim]Run './atc-coding-rules-updater.ps1' manually to download coding rules.[/]");
            return true; // Don't fail the whole process, just warn
        }
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
            File.WriteAllText(gitignorePath, GitIgnoreContent);
            AnsiConsole.MarkupLine("[green]✓[/] Created .gitignore");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error creating .gitignore: {ex.Message}");
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
    /// <returns>True if successful, false otherwise.</returns>
    public bool GenerateHostProject(
        string srcPath,
        string hostProjectName,
        string contractsProjectName,
        string domainProjectName,
        string targetFramework,
        HostUiType hostUi,
        HostUiModeType hostUiMode)
    {
        var hostProjectPath = Path.Combine(srcPath, hostProjectName);
        var csprojPath = Path.Combine(hostProjectPath, $"{hostProjectName}.csproj");
        var programPath = Path.Combine(hostProjectPath, "Program.cs");

        try
        {
            // Create project directory
            if (!Directory.Exists(hostProjectPath))
            {
                Directory.CreateDirectory(hostProjectPath);
                AnsiConsole.MarkupLine($"[green]✓[/] Created directory: {hostProjectName}/");
            }

            // Create .csproj file
            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateHostProjectContent(
                    contractsProjectName,
                    domainProjectName,
                    targetFramework,
                    hostUi);
                File.WriteAllText(csprojPath, csprojContent);
                AnsiConsole.MarkupLine($"[green]✓[/] Created project file: {hostProjectName}.csproj");
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]✓[/] Project file already exists: {hostProjectName}.csproj");
            }

            // Create Program.cs file
            if (!File.Exists(programPath))
            {
                var baseName = ExtractSolutionName(hostProjectName);
                var programContent = GenerateProgramContent(baseName, hostUi, hostUiMode);
                File.WriteAllText(programPath, programContent);
                AnsiConsole.MarkupLine("[green]✓[/] Created Program.cs");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]✓[/] Program.cs already exists");
            }

            // Create GlobalUsings.cs file
            var globalUsingsPath = Path.Combine(hostProjectPath, "GlobalUsings.cs");
            if (!File.Exists(globalUsingsPath))
            {
                var baseName = ExtractSolutionName(hostProjectName);
                var globalUsingsContent = GenerateHostGlobalUsingsContent(baseName, hostUi);
                File.WriteAllText(globalUsingsPath, globalUsingsContent);
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
            AnsiConsole.MarkupLine($"[red]✗[/] Error creating host project: {ex.Message}");
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
                Directory.CreateDirectory(aspireProjectPath);
                AnsiConsole.MarkupLine($"[green]✓[/] Created directory: {aspireProjectName}/");
            }

            // Create .csproj file
            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateAspireProjectContent(apiProjectName, targetFramework);
                File.WriteAllText(csprojPath, csprojContent);
                AnsiConsole.MarkupLine($"[green]✓[/] Created project file: {aspireProjectName}.csproj");
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]✓[/] Project file already exists: {aspireProjectName}.csproj");
            }

            // Create Program.cs file
            if (!File.Exists(programPath))
            {
                var programContent = GenerateAspireProgramContent(apiProjectName);
                File.WriteAllText(programPath, programContent);
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
            AnsiConsole.MarkupLine($"[red]✗[/] Error creating Aspire project: {ex.Message}");
            return false;
        }
    }

    private static string ExtractSolutionName(string projectName)
    {
        // Remove common suffixes to get base name
        var name = projectName;
        var suffixes = new[] { ".Api.Contracts", ".Api.Domain", ".Api", ".Contracts", ".Domain" };

        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }

        return name;
    }

    private static string GenerateTestProjectContent(
        string targetFramework,
        string contractsProjectName)
        => $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{targetFramework}</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <IsPackable>false</IsPackable>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="coverlet.collector" Version="6.*">
                  <PrivateAssets>all</PrivateAssets>
                  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
                </PackageReference>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
                <PackageReference Include="xunit" Version="2.*" />
                <PackageReference Include="xunit.runner.visualstudio" Version="3.*">
                  <PrivateAssets>all</PrivateAssets>
                  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
                </PackageReference>
              </ItemGroup>

              <ItemGroup>
                <ProjectReference Include="..\..\src\{contractsProjectName}\{contractsProjectName}.csproj" />
              </ItemGroup>

            </Project>
            """;

    private static string GenerateTestFileContent(string testProjectNamespace)
        => $$"""
            using Xunit;

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
        HostUiType hostUi)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk.Web\">");
        sb.AppendLine();
        sb.AppendLine(2, "<PropertyGroup>");
        sb.AppendLine(4, $"<TargetFramework>{targetFramework}</TargetFramework>");
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

    private static string GenerateProgramContent(
        string baseName,
        HostUiType hostUi,
        HostUiModeType hostUiMode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();

        // Add OpenAPI services if UI is enabled
        if (hostUi != HostUiType.None)
        {
            sb.AppendLine("// Configure OpenAPI document generation");
            sb.AppendLine("builder.Services.AddOpenApi();");
            sb.AppendLine();
        }

        sb.AppendLine("// Register handler implementations from Domain project");
        sb.AppendLine("builder.Services.AddApiHandlersFromDomain();");
        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();

        // Generate UI mapping based on configuration
        if (hostUi != HostUiType.None)
        {
            if (hostUiMode == HostUiModeType.DevelopmentOnly)
            {
                sb.AppendLine("if (app.Environment.IsDevelopment())");
                sb.AppendLine("{");
                sb.AppendLine(4, "app.MapOpenApi();");
                AppendUiMapping(sb, hostUi, 4);
                sb.AppendLine("}");
            }
            else
            {
                sb.AppendLine("app.MapOpenApi();");
                AppendUiMapping(sb, hostUi, 0);
            }

            sb.AppendLine();

            // Add root redirect to UI
            var redirectPath = hostUi == HostUiType.Swagger ? "/swagger" : "/scalar/v1";
            sb.AppendLine("// Redirect root to API documentation");
            sb.Append("app.MapGet(\"/\", () => Results.Redirect(\"");
            sb.Append(redirectPath);
            sb.AppendLine("\")).ExcludeFromDescription();");
            sb.AppendLine();
        }

        sb.AppendLine("// Configure middleware and map all endpoints");
        sb.AppendLine("app.MapEndpoints();");
        sb.AppendLine();
        sb.Append("app.Run();");

        return sb.ToString();
    }

    private static void AppendUiMapping(
        StringBuilder sb,
        HostUiType hostUi,
        int indent)
    {
        switch (hostUi)
        {
            case HostUiType.Scalar:
                sb.AppendLine(indent, "app.MapScalarApiReference();");
                break;
            case HostUiType.Swagger:
                sb.AppendLine(indent, "app.UseSwaggerUI(options =>");
                sb.AppendLine(indent, "{");
                sb.AppendLine(indent + 4, "options.SwaggerEndpoint(\"/openapi/v1.json\", \"v1\");");
                sb.AppendLine(indent, "});");
                break;
        }
    }

    private static string GenerateHostGlobalUsingsContent(
        string baseName,
        HostUiType hostUi)
    {
        var sb = new StringBuilder();

        // Root namespace for extension methods (from Domain project DI registration)
        sb.Append("global using ");
        sb.Append(baseName);
        sb.AppendLine(";");

        // Generated endpoints namespace
        sb.Append("global using ");
        sb.Append(baseName);
        sb.AppendLine(".Generated.Endpoints;");

        // UI-specific namespaces
        if (hostUi == HostUiType.Scalar)
        {
            sb.AppendLine("global using Scalar.AspNetCore;");
        }

        return sb.ToString();
    }

    private static string GenerateAspireProjectContent(
        string apiProjectName,
        string targetFramework)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Aspire.AppHost.Sdk/13.0.0\">");
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