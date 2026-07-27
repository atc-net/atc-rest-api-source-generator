namespace Atc.Rest.Api.Generator.IntegrationTests;

public static partial class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // Write snapshots as UTF-8 without BOM so files are portable across platforms.
        VerifierSettings.UseEncoding(new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // Configure Verify to scrub timestamps and other volatile content
        VerifierSettings.ScrubLinesContaining(StringComparison.Ordinal, "Version=");

        // Scrub the version in GeneratedCode attribute (e.g., [GeneratedCode("Atc.Rest.Api.SourceGenerator", "x.y.z")])
        // to ensure verify files remain stable across different builds
        VerifierSettings.AddScrubber(
            (builder, _) =>
            {
                var content = builder.ToString();
                var scrubbed = GeneratedCodeVersionRegex().Replace(content, "[GeneratedCode(\"${name}\", \"1.0.0\")]");
                builder.Clear();
                builder.Append(scrubbed);
            });
    }

    [GeneratedRegex(@"\[GeneratedCode\(""(?<name>[^""]+)"",\s*""[^""]+""\)\]", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex GeneratedCodeVersionRegex();
}