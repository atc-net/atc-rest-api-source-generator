namespace Atc.Rest.Api.SourceGenerator.Tests.Helpers;

/// <summary>
/// The start every generator makes - the marker read into its configuration, the specification's files found -
/// tested once instead of through each generator.
/// </summary>
public sealed class GeneratorStartupTests
{
    private const string Spec = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths: {}
        """;

    private static readonly EquatableArray<YamlFileInfo> OneSpecification = Files(("Contoso.yaml", Spec));

    [Fact]
    public void ReadMarker_WithoutAMarkerOrASpecification_IsNothing_AndReportsNothing()
    {
        (MarkerFileInfo, ClientConfig)? withoutMarker = null;
        (MarkerFileInfo, ClientConfig)? withoutSpecification = null;

        var diagnostics = Run(context =>
        {
            withoutMarker = GeneratorStartup.ReadMarker<ClientConfig>(context, OneSpecification, Markers());
            withoutSpecification = GeneratorStartup.ReadMarker<ClientConfig>(context, Files(), Markers("{}"));
        });

        Assert.Null(withoutMarker);
        Assert.Null(withoutSpecification);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("{ \"namespace\": \"Contoso.Pets\" }", true)]
    [InlineData("{ \"generate\": false }", false)]
    public void ReadMarker_AMarker_IsItsConfiguration_UnlessItTurnsGenerationOff(
        string content,
        bool expected)
    {
        (MarkerFileInfo Marker, ClientConfig Config)? started = null;

        var diagnostics = Run(context => started = GeneratorStartup.ReadMarker<ClientConfig>(context, OneSpecification, Markers(content)));

        Assert.Empty(diagnostics);
        Assert.Equal(expected, started is not null);
        if (started is { } value)
        {
            Assert.Equal("Contoso.Pets", value.Config.Namespace);
        }
    }

    [Theory]
    [InlineData(RuleIdentifiers.MultipleMarkerFiles, "{}", "{}")]
    [InlineData(RuleIdentifiers.MarkerConfigParseError, "{ not json", null)]
    public void ReadMarker_TwoMarkersOrOneThatCannotBeRead_IsReported(
        string ruleId,
        string marker,
        string? secondMarker)
    {
        string[] markers = secondMarker is null ? [marker] : [marker, secondMarker];
        (MarkerFileInfo, ClientConfig)? started = null;

        var diagnostics = Run(context => started = GeneratorStartup.ReadMarker<ClientConfig>(context, OneSpecification, Markers(markers)));

        Assert.Null(started);
        Assert.Equal(ruleId, Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void FindSpecification_IsTheBaseFileWithItsParts_NotTheOverlays()
    {
        SpecificationFiles? files = null;

        var diagnostics = Run(context => files = GeneratorStartup.FindSpecification(
            context,
            "C:/project/.atc-rest-api-client",
            listedOverlays: null,
            Files(("Contoso.yaml", Spec), ("Contoso_Owners.yaml", "paths: {}"))));

        Assert.Empty(diagnostics);
        Assert.NotNull(files);
        Assert.Equal("Contoso.yaml", files.BaseFile.Path);
        Assert.Equal("Contoso_Owners.yaml", Assert.Single(files.PartFiles).Path);
        Assert.Empty(files.Overlays);
    }

    [Fact]
    public void FindSpecification_TwoBaseFiles_IsReported()
    {
        SpecificationFiles? files = null;

        var diagnostics = Run(context => files = GeneratorStartup.FindSpecification(
            context,
            "C:/project/.atc-rest-api-client",
            listedOverlays: null,
            Files(("Contoso.yaml", Spec), ("Fabrikam.yaml", Spec))));

        Assert.Null(files);
        Assert.Equal(RuleIdentifiers.MultipleSpecifications, Assert.Single(diagnostics).Id);
    }

    private static EquatableArray<YamlFileInfo> Files(
        params (string Path, string Content)[] files)
        => new([.. files.Select(f => new YamlFileInfo(f.Path, f.Content))]);

    private static EquatableArray<MarkerFileInfo> Markers(
        params string[] contents)
        => new([.. contents.Select((c, i) => new MarkerFileInfo($"C:/project{i}/.atc-rest-api-client", c))]);

    private static ImmutableArray<Diagnostic> Run(
        Action<SourceProductionContext> action)
        => SourceProductionContextRunner.Run(action, TestContext.Current.CancellationToken);
}