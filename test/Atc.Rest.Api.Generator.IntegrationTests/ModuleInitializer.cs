namespace Atc.Rest.Api.Generator.IntegrationTests;

public static partial class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // Configure Verify to scrub timestamps and other volatile content
        VerifierSettings.ScrubLinesContaining("Version=", StringComparison.Ordinal);

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