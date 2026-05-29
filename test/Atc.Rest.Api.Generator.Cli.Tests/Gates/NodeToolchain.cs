namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Thin wrapper around the local Node toolchain (node / npm / npx) used by the
/// TypeScript <c>tsc --noEmit</c> regression gate. Detects availability so the gate
/// can self-skip on machines / CI lanes without Node installed.
/// </summary>
internal static class NodeToolchain
{
    public static bool IsAvailable(out string skipReason)
    {
        try
        {
            var node = Run("node", "--version", Environment.CurrentDirectory, TimeSpan.FromSeconds(30));
            var npm = Run("npm", "--version", Environment.CurrentDirectory, TimeSpan.FromSeconds(60));
            if (node.ExitCode == 0 && npm.ExitCode == 0)
            {
                skipReason = string.Empty;
                return true;
            }

            skipReason = "Node toolchain not runnable (node/npm returned non-zero); skipping tsc gate.";
            return false;
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or InvalidOperationException)
        {
            skipReason = "Node toolchain (node/npm) not found on PATH; skipping tsc gate.";
            return false;
        }
    }

    public static ProcessResult RunNpm(
        string arguments,
        string workingDirectory,
        TimeSpan timeout)
        => Run("npm", arguments, workingDirectory, timeout);

    public static ProcessResult RunNpx(
        string arguments,
        string workingDirectory,
        TimeSpan timeout)
        => Run("npx", arguments, workingDirectory, timeout);

    private static ProcessResult Run(
        string tool,
        string arguments,
        string workingDirectory,
        TimeSpan timeout)
    {
        // On Windows npm / npx are .cmd batch shims that ProcessStartInfo cannot launch
        // by bare name when UseShellExecute is false, so route everything through
        // cmd.exe /c (which honours PATH + PATHEXT). On other platforms invoke directly.
        var (fileName, fullArguments) = OperatingSystem.IsWindows()
            ? ("cmd.exe", "/c " + tool + " " + arguments)
            : (tool, arguments);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = fullArguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var sync = new Lock();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (sync)
                {
                    output.AppendLine(e.Data);
                }
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (sync)
                {
                    output.AppendLine(e.Data);
                }
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited.
            }

            return new ProcessResult(-1, $"Timed out after {timeout.TotalSeconds:N0}s running: {tool} {arguments}");
        }

        // Ensure async output buffers are flushed.
        process.WaitForExit();

        lock (sync)
        {
            return new ProcessResult(process.ExitCode, output.ToString());
        }
    }
}