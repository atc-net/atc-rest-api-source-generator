namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Runs a tool the CLI depends on (git, atc-coding-rules-updater) so that the tool cannot hold the command up: both
/// output streams are read as they come (a full stderr pipe never blocks the tool), standard input is closed (a tool
/// that asks a question gets no answer instead of waiting for one), and after the time it is given the tool and the
/// processes it started are stopped.
/// </summary>
public static class ExternalTool
{
    /// <summary>
    /// Runs the tool and waits for it, for at most the given time.
    /// </summary>
    /// <param name="fileName">The executable.</param>
    /// <param name="arguments">Its command line.</param>
    /// <param name="workingDirectory">The directory it runs in.</param>
    /// <param name="timeout">How long it may take.</param>
    /// <returns>The exit code and the output, or a result without an exit code when it was stopped.</returns>
    public static ExternalToolResult Run(
        string fileName,
        string arguments,
        string workingDirectory,
        TimeSpan timeout)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var output = new StringBuilder();
        var error = new StringBuilder();
        process.OutputDataReceived += (_, e) => Append(output, e.Data);
        process.ErrorDataReceived += (_, e) => Append(error, e.Data);

        process.Start();
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(timeout))
        {
            Stop(process);
            return new ExternalToolResult(ExitCode: null, Text(output), Text(error));
        }

        // The exit is in; this waits for the readers to see the end of both streams
        process.WaitForExit();
        return new ExternalToolResult(process.ExitCode, Text(output), Text(error));
    }

    private static void Append(
        StringBuilder builder,
        string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (builder)
        {
            builder.AppendLine(line);
        }
    }

    private static string Text(StringBuilder builder)
    {
        lock (builder)
        {
            return builder.ToString();
        }
    }

    private static void Stop(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // It ended on its own in the meantime, or cannot be stopped: nothing more to do about it
        }
    }
}