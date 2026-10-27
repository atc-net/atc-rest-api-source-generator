namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Checks git status for uncommitted changes before migration.
/// </summary>
internal static class GitStatusChecker
{
    /// <summary>
    /// Checks if there are uncommitted changes in the repository.
    /// </summary>
    /// <param name="rootDirectory">The root directory of the repository.</param>
    /// <returns>A result containing the list of uncommitted files.</returns>
    public static GitStatusResult Check(string rootDirectory)
    {
        var result = new GitStatusResult();

        try
        {
            // Check if this is a git repository
            var gitDir = Path.Combine(rootDirectory, ".git");
            if (!Directory.Exists(gitDir))
            {
                result.IsGitRepository = false;
                return result;
            }

            result.IsGitRepository = true;

            // Run git status --porcelain
            var run = ExternalTool.Run(ExecutableResolver.Resolve("git"), "status --porcelain", rootDirectory, TimeSpan.FromSeconds(60));
            if (run.TimedOut)
            {
                result.GitCommandFailed = true;
                result.ErrorMessage = "git status did not end within 60 seconds and was stopped.";
                return result;
            }

            var output = run.Output;
            if (run.ExitCode != 0)
            {
                result.GitCommandFailed = true;
                result.ErrorMessage = run.Error;
                return result;
            }

            // Parse the output
            if (!string.IsNullOrWhiteSpace(output))
            {
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Length < 3)
                    {
                        continue;
                    }

                    var status = line[..2];
                    var file = line[3..].Trim();

                    result.UncommittedFiles.Add(new GitFileStatus
                    {
                        Status = status,
                        FilePath = file,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            result.GitCommandFailed = true;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Displays the git status warning to the user.
    /// </summary>
    /// <param name="result">The git status result.</param>
    /// <returns>True if the user wants to proceed, false otherwise.</returns>
    public static bool DisplayWarningAndConfirm(GitStatusResult result)
    {
        if (!result.HasUncommittedChanges)
        {
            return true;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]⚠ Warning: Uncommitted changes detected[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("The following files have uncommitted changes:");

        foreach (var file in result.UncommittedFiles.Take(10))
        {
            var (statusColor, statusText) = file.Status switch
            {
                "M " => (Color.Yellow, "M"),
                " M" => (Color.Yellow, "M"),
                "MM" => (Color.Yellow, "M"),
                "A " => (Color.Green, "+"),
                " A" => (Color.Green, "+"),
                "D " => (Color.Red, "-"),
                " D" => (Color.Red, "-"),
                "??" => (Color.Grey, "?"),
                _ => (Color.Grey, file.Status),
            };

            // Text never parses markup, so the status and the path are printed as they are.
            AnsiConsole.Write(new Text("  "));
            AnsiConsole.Write(new Text(statusText, new Style(statusColor)));
            AnsiConsole.Write(new Text($" {file.FilePath}{Environment.NewLine}"));
        }

        if (result.UncommittedFiles.Count > 10)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]... and {result.UncommittedFiles.Count - 10} more files[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Migration will modify and delete files. Ensure all changes are committed");
        AnsiConsole.MarkupLine("to git before proceeding so you can easily revert if needed.");
        AnsiConsole.WriteLine();

        return AnsiConsole.Confirm("Do you want to proceed anyway?", defaultValue: false);
    }
}