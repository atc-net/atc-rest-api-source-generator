namespace Atc.Rest.Api.SourceGenerator.Tests;

/// <summary>
/// Verify configuration for the scenario snapshots.
/// </summary>
/// <remarks>
/// Mirrors the integration-test project's settings, because the snapshot files are the same files -
/// they are now produced by the real Roslyn generators instead of a parallel orchestration.
/// </remarks>
public static partial class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // Write snapshots as UTF-8 without BOM so files are portable across platforms.
        VerifierSettings.UseEncoding(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        VerifierSettings.ScrubLinesContaining(StringComparison.Ordinal, "Version=");

        // The [GeneratedCode] attribute carries the build version, which changes on every build.
        // Pin it so the snapshots stay stable.
        VerifierSettings.AddScrubber(
            (builder, _) =>
            {
                var scrubbed = GeneratedCodeVersionRegex()
                    .Replace(builder.ToString(), "[GeneratedCode(\"${name}\", \"1.0.0\")]");

                builder.Clear();
                builder.Append(scrubbed);
            });
    }

    [GeneratedRegex(@"\[GeneratedCode\(""(?<name>[^""]+)"",\s*""[^""]+""\)\]", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex GeneratedCodeVersionRegex();
}