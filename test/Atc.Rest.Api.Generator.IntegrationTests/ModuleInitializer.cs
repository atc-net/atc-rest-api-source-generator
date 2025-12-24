namespace Atc.Rest.Api.Generator.IntegrationTests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // Configure Verify to scrub timestamps and other volatile content
        VerifierSettings.ScrubLinesContaining("Version=", StringComparison.Ordinal);
    }
}