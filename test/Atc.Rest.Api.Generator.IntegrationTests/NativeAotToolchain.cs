namespace Atc.Rest.Api.Generator.IntegrationTests;

/// <summary>
/// Whether this machine can publish a Native AOT app: the platform linker the ILCompiler calls - clang on Linux and
/// macOS (the GitHub Ubuntu and macOS runners have it), the Visual C++ build tools on Windows.
/// </summary>
internal static class NativeAotToolchain
{
    public static bool IsAvailable(out string skipReason)
    {
        if (OperatingSystem.IsWindows())
        {
            var vswhere = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft Visual Studio",
                "Installer",
                "vswhere.exe");
            if (File.Exists(vswhere) &&
                Run(vswhere, "-products", "*", "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-property", "installationPath").Trim().Length > 0)
            {
                skipReason = string.Empty;
                return true;
            }

            skipReason = "Native AOT needs the Visual C++ build tools (the 'Desktop development with C++' workload); none is installed.";
            return false;
        }

        var onPath = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Combine(directory, "clang")));
        skipReason = onPath ? string.Empty : "Native AOT needs clang on the PATH; none was found.";
        return onPath;
    }

    private static string Run(
        string fileName,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return string.Empty;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}