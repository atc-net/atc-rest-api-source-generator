namespace Atc.Rest.Api.SourceGenerator.Tests.Helpers;

/// <summary>
/// Overlay files are told from specifications by their content, a marker's <c>overlays</c> are found among them by
/// path (relative to the marker, either slash, any casing), and what goes wrong is reported.
/// </summary>
public class OverlayFileHelperTests
{
    private const string Specification = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths: {}
        """;

    private const string Overlay = """
        overlay: 1.0.0
        info: { title: Contoso fixes, version: 1.0.0 }
        actions:
          - target: $.info
            update: { description: Fixed }
        """;

    private const string JsonOverlay = """{ "overlay": "1.0.0", "info": { "title": "Contoso fixes", "version": "1.0.0" }, "actions": [] }""";

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "contoso-api");

    private static readonly string Marker = Path.Combine(Root, ".atc-rest-api-server");

    [Theory]
    [InlineData("Contoso.yaml", Specification, true)]
    [InlineData("Contoso.yml", Specification, true)]
    [InlineData("Contoso.overlay.yaml", Overlay, true)]
    [InlineData("Contoso.overlay.json", JsonOverlay, true)]
    [InlineData("Contoso.overlay.JSON", JsonOverlay, true)]
    [InlineData(".atc-rest-api-server.json", "{ \"validateSpecificationStrategy\": \"Strict\" }", false)]
    [InlineData("Contoso.JSON", "{ \"openapi\": \"3.0.3\" }", false)]
    [InlineData("Contoso.yaml", "", false)]
    [InlineData("Contoso.overlay.json", "", false)]
    public void IsCandidate_YamlOrAnOverlay_WithContent(
        string path,
        string content,
        bool expected)
        => Assert.Equal(expected, OverlayFileHelper.IsCandidate(new YamlFileInfo(path, content)));

    [Fact]
    public void IsCandidate_NullContent_IsNoCandidate()
        => Assert.False(OverlayFileHelper.IsCandidate(new YamlFileInfo("Contoso.yaml", null!)));

    [Fact]
    public void Split_ByContent_NotByName()
    {
        var files = ImmutableArray.Create(
            new YamlFileInfo("Contoso.yaml", Specification),
            new YamlFileInfo("Contoso.overlay.yaml", Overlay),
            new YamlFileInfo("Contoso_Users.yaml", Specification),
            new YamlFileInfo("Fixes.json", JsonOverlay),
            new YamlFileInfo("Named.overlay.yaml", Specification),
            new YamlFileInfo("Other.json", "{ \"openapi\": \"3.0.3\" }"));

        var (specifications, overlays) = OverlayFileHelper.Split(files);

        Assert.Equal(["Contoso.yaml", "Contoso_Users.yaml", "Named.overlay.yaml"], specifications.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Equal(["Contoso.overlay.yaml", "Fixes.json"], overlays.Select(f => f.Path), StringComparer.Ordinal);
    }

    [Fact]
    public void Split_NoFiles_IsTwoEmptyLists()
    {
        var (specifications, overlays) = OverlayFileHelper.Split(ImmutableArray<YamlFileInfo>.Empty);

        Assert.Empty(specifications);
        Assert.Empty(overlays);
    }

    [Fact]
    public void Resolve_TheListedOverlays_InTheMarkersOrder()
    {
        var first = OverlayFile("first.overlay.yaml");
        var second = OverlayFile("second.overlay.yaml");
        List<OverlayFile>? resolved = null;

        var diagnostics = Run(context => resolved = OverlayFileHelper.Resolve(
            context,
            Marker,
            ["second.overlay.yaml", "first.overlay.yaml"],
            [first, second]));

        Assert.Empty(diagnostics);
        Assert.NotNull(resolved);
        Assert.Equal([second.Path, first.Path], resolved.Select(o => o.FilePath), StringComparer.Ordinal);
        Assert.Equal(Overlay, resolved[0].Content);
    }

    [Fact]
    public void Resolve_NothingListed_NothingApplied_AndEveryOverlayIsANote()
    {
        var overlay = OverlayFile("fixes.overlay.yaml");
        List<OverlayFile>? resolved = null;

        var diagnostics = Run(context => resolved = OverlayFileHelper.Resolve(context, Marker, null, [overlay]));

        Assert.NotNull(resolved);
        Assert.Empty(resolved);
        var note = Assert.Single(diagnostics);
        Assert.Equal(RuleIdentifiers.OverlayNotListed, note.Id);
        Assert.Equal(DiagnosticSeverity.Info, note.Severity);
        Assert.Contains(overlay.Path, note.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_NothingListed_AndNoOverlayFiles_IsQuiet()
    {
        List<OverlayFile>? resolved = null;

        var diagnostics = Run(context => resolved = OverlayFileHelper.Resolve(context, Marker, [], []));

        Assert.Empty(diagnostics);
        Assert.NotNull(resolved);
        Assert.Empty(resolved);
    }

    [Fact]
    public void Resolve_ListedOverlayThatIsNoFile_IsAnError_AndNothingIsApplied()
    {
        var present = OverlayFile("present.overlay.yaml");
        List<OverlayFile>? resolved = [];

        var diagnostics = Run(context => resolved = OverlayFileHelper.Resolve(
            context,
            Marker,
            ["present.overlay.yaml", "missing.overlay.yaml"],
            [present]));

        Assert.Null(resolved);
        var error = Assert.Single(diagnostics);
        Assert.Equal(RuleIdentifiers.OverlayNotFound, error.Id);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("'missing.overlay.yaml'", error.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_ListedAndUnlistedOverlays_TheUnlistedOneIsANote()
    {
        var listed = OverlayFile("listed.overlay.yaml");
        var unlisted = OverlayFile("unlisted.overlay.yaml");
        List<OverlayFile>? resolved = null;

        var diagnostics = Run(context => resolved = OverlayFileHelper.Resolve(context, Marker, ["listed.overlay.yaml"], [listed, unlisted]));

        Assert.Equal([listed.Path], resolved!.Select(o => o.FilePath), StringComparer.Ordinal);
        var note = Assert.Single(diagnostics);
        Assert.Equal(RuleIdentifiers.OverlayNotListed, note.Id);
        Assert.Contains(unlisted.Path, note.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("overlays/fixes.overlay.yaml")]
    [InlineData("./overlays/fixes.overlay.yaml")]
    [InlineData("overlays/../overlays/fixes.overlay.yaml")]
    [InlineData("OVERLAYS/Fixes.Overlay.YAML")]
    public void Resolve_EntryRelativeToTheMarker_ForwardSlashes_AnyCasing(
        string entry)
    {
        var overlay = OverlayFile(Path.Combine("overlays", "fixes.overlay.yaml"));
        List<OverlayFile>? resolved = null;

        var diagnostics = Run(context => resolved = OverlayFileHelper.Resolve(context, Marker, [entry], [overlay]));

        Assert.Empty(diagnostics);
        Assert.Equal([overlay.Path], resolved!.Select(o => o.FilePath), StringComparer.Ordinal);
    }

    [Fact]
    public void Resolve_EntryOutsideTheMarkersFolder_IsFound()
    {
        var overlay = new YamlFileInfo(Path.Combine(Path.GetTempPath(), "shared", "fixes.overlay.yaml"), Overlay);
        List<OverlayFile>? resolved = null;

        var diagnostics = Run(context => resolved = OverlayFileHelper.Resolve(context, Marker, ["../shared/fixes.overlay.yaml"], [overlay]));

        Assert.Empty(diagnostics);
        Assert.Equal([overlay.Path], resolved!.Select(o => o.FilePath), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(@"overlays\fixes.overlay.yaml")]
    [InlineData(@".\overlays\fixes.overlay.yaml")]
    [InlineData(@"C:\contoso-api\overlays\fixes.overlay.yaml")]
    public void Resolve_WindowsPaths_BackslashesAndDriveLetters(string entry)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "A backslash is a separator on Windows only");

        var overlay = new YamlFileInfo(@"C:\contoso-api\overlays\fixes.overlay.yaml", Overlay);
        List<OverlayFile>? resolved = null;

        var diagnostics = Run(context => resolved = OverlayFileHelper.Resolve(context, @"C:\contoso-api\.atc-rest-api-server", [entry], [overlay]));

        Assert.Empty(diagnostics);
        Assert.Equal([overlay.Path], resolved!.Select(o => o.FilePath), StringComparer.Ordinal);
    }

    [Fact]
    public void Resolve_UnixPaths_AbsoluteAndRelative()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "An absolute Unix path has no drive on Windows");

        var overlay = new YamlFileInfo("/srv/contoso-api/overlays/fixes.overlay.yaml", Overlay);
        List<OverlayFile>? resolved = null;

        var diagnostics = Run(context => resolved = OverlayFileHelper.Resolve(
            context,
            "/srv/contoso-api/.atc-rest-api-server",
            ["overlays/fixes.overlay.yaml", "/srv/contoso-api/overlays/fixes.overlay.yaml"],
            [overlay]));

        Assert.Empty(diagnostics);
        Assert.Equal([overlay.Path, overlay.Path], resolved!.Select(o => o.FilePath), StringComparer.Ordinal);
    }

    [Fact]
    public void Resolve_EntryWithCharactersAPathCannotHold_IsNotFound_NotAnException()
    {
        var overlay = OverlayFile("fixes.overlay.yaml");
        List<OverlayFile>? resolved = [];

        var diagnostics = Run(context => resolved = OverlayFileHelper.Resolve(context, Marker, ["fix\0es.overlay.yaml"], [overlay]));

        Assert.Null(resolved);
        Assert.Contains(diagnostics, d => d.Id == RuleIdentifiers.OverlayNotFound);
    }

    [Fact]
    public void ResolveListed_TheFirstMarkerThatListsOverlays_WithoutReportingAnything()
    {
        var first = OverlayFile("first.overlay.yaml");
        var second = OverlayFile("second.overlay.yaml");
        var markers = Markers(
            (Path.Combine(Root, ".atc-rest-api-client"), "{ \"overlays\": [] }"),
            (Marker, "{ \"overlays\": [\"second.overlay.yaml\", \"missing.overlay.yaml\", \"first.overlay.yaml\"] }"));

        var resolved = OverlayFileHelper.ResolveListed(markers, [first, second]);

        // A missing overlay is left out: the marker's own generator reports it
        Assert.Equal([second.Path, first.Path], resolved.Select(o => o.FilePath), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("{ }")]
    [InlineData("{ \"overlays\": null }")]
    [InlineData("{ \"overlays\": \"fixes.overlay.yaml\" }")]
    [InlineData("[ \"fixes.overlay.yaml\" ]")]
    [InlineData("{ not json")]
    [InlineData("")]
    public void ResolveListed_MarkerWithoutAListOfOverlays_IsNoOverlay(
        string marker)
        => Assert.Empty(OverlayFileHelper.ResolveListed(Markers((Marker, marker)), [OverlayFile("fixes.overlay.yaml")]));

    [Fact]
    public void ResolveListed_MarkerWithAnyCasingOfTheSetting_ListsItsOverlays_AsTheGeneratorReadsIt()
    {
        // The generators read a marker ignoring the casing of its settings, so the injected endpoints must see the
        // same overlays
        const string marker = "{ \"Overlays\": [\"fixes.overlay.yaml\"] }";
        var overlay = OverlayFile("fixes.overlay.yaml");

        var config = JsonSerializer.Deserialize<ServerConfig>(marker, JsonHelper.ConfigOptions);

        Assert.Equal(["fixes.overlay.yaml"], config!.Overlays);
        Assert.Equal([overlay.Path], OverlayFileHelper.ResolveListed(Markers((Marker, marker)), [overlay]).Select(o => o.FilePath), StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveListed_SettingThereTwiceInDifferentCasings_IsTheLastOne_AsTheGeneratorReadsIt()
    {
        const string marker = "{ \"overlays\": [\"first.overlay.yaml\"], \"Overlays\": [\"second.overlay.yaml\"] }";
        var first = OverlayFile("first.overlay.yaml");
        var second = OverlayFile("second.overlay.yaml");

        var config = JsonSerializer.Deserialize<ServerConfig>(marker, JsonHelper.ConfigOptions);

        Assert.Equal(["second.overlay.yaml"], config!.Overlays);
        Assert.Equal([second.Path], OverlayFileHelper.ResolveListed(Markers((Marker, marker)), [first, second]).Select(o => o.FilePath), StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveListed_NoMarkers_IsNoOverlay()
        => Assert.Empty(OverlayFileHelper.ResolveListed(Markers(), [OverlayFile("fixes.overlay.yaml")]));

    [Fact]
    public void Report_AnErrorStopsGeneration_AWarningNamesTheActionThatChangedNothing()
    {
        IReadOnlyList<OverlayFileProblem> problems =
        [
            new("broken.overlay.yaml", new OverlayProblem(IsError: true, ActionIndex: null, "'actions' must be an array")),
            new("stale.overlay.yaml", new OverlayProblem(IsError: false, ActionIndex: 2, "$.paths['/old'] selects nothing")),
        ];

        var diagnostics = Run(context => OverlayFileHelper.Report(context, problems));

        Assert.Equal(2, diagnostics.Length);
        Assert.Equal(RuleIdentifiers.OverlayInvalid, diagnostics[0].Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[0].Severity);
        Assert.Equal(
            "The overlay 'broken.overlay.yaml' cannot be applied, so nothing is generated: 'actions' must be an array",
            diagnostics[0].GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal(RuleIdentifiers.OverlayTargetSelectsNothing, diagnostics[1].Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostics[1].Severity);
        Assert.StartsWith("The overlay 'stale.overlay.yaml' (actions[2]) changes nothing", diagnostics[1].GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public void Report_NoProblems_ReportsNothing()
        => Assert.Empty(Run(context => OverlayFileHelper.Report(context, [])));

    private static YamlFileInfo OverlayFile(string relativePath)
        => new(Path.Combine(Root, relativePath), Overlay);

    private static EquatableArray<MarkerFileInfo> Markers(
        params (string Path, string Content)[] markers)
        => new([.. markers.Select(m => new MarkerFileInfo(m.Path, m.Content))]);

    private static ImmutableArray<Diagnostic> Run(
        Action<SourceProductionContext> action)
        => SourceProductionContextRunner.Run(action, TestContext.Current.CancellationToken);
}