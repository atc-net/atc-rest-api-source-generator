namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A broken specification is reported, never a crash: every scenario specification, mutated (a member removed, a value
/// of another kind, an odd string, a component schema removed), goes through the server, handler and client
/// generators, and none reports its "generation failed" diagnostic (ATC_API_GEN001/003/005) or lets an exception out
/// (CS8785). The mutations are drawn from a fixed seed per scenario and mutant, so a failure reproduces; a failure
/// is a generator bug to fix, not a case to skip.
/// </summary>
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Seeded on purpose: a mutant must reproduce from its seed; nothing here is a secret.")]
public sealed class GeneratorRobustnessFuzzTests
{
    private const int MutantsPerScenario = 3;

    private static readonly string[] CrashDiagnostics = ["ATC_API_GEN001", "ATC_API_GEN003", "ATC_API_GEN005", "CS8785"];

    private static readonly (IIncrementalGenerator Generator, string Marker)[] Generators =
    [
        (new ApiServerGenerator(), ".atc-rest-api-server"),
        (new ApiServerDomainGenerator(), ".atc-rest-api-server-handlers"),
        (new ApiClientGenerator(), ".atc-rest-api-client"),
    ];

    public static IEnumerable<object[]> Scenarios()
        => new DirectoryInfo(GeneratorSnapshotHarness.GetScenariosRoot())
            .GetDirectories()
            .Where(d => File.Exists(Path.Combine(d.FullName, d.Name + ".yaml")))
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .Select(d => new object[] { d.Name });

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void MutatedSpecification_IsReportedNotCrashedOn(string scenario)
    {
        var yaml = File.ReadAllText(Path.Combine(GeneratorSnapshotHarness.GetScenariosRoot(), scenario, scenario + ".yaml"));
        var crashes = new List<string>();

        // The handler generator writes its scaffolds next to the marker: a folder of the test's own
        var folder = Path.Combine(Path.GetTempPath(), "atc-fuzz-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            for (var mutant = 0; mutant < MutantsPerScenario; mutant++)
            {
                var (specification, mutation) = Mutate(yaml, StableSeed(scenario) + mutant);
                foreach (var (generator, marker) in Generators)
                {
                    var diagnostics = Run(generator, Path.Combine(folder, marker), Path.Combine(folder, "Contoso.yaml"), specification);
                    foreach (var diagnostic in diagnostics.Where(d => CrashDiagnostics.Contains(d.Id, StringComparer.Ordinal)))
                    {
                        crashes.Add($"mutant {mutant} ({mutation}) {marker}: {diagnostic.Id} {diagnostic.GetMessage(CultureInfo.InvariantCulture)}");
                    }
                }
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }

        Assert.True(crashes.Count == 0, string.Join("\n", crashes));
    }

    /// <summary>
    /// The mutations are not blind: each changes the document, and the same seed gives the same mutant.
    /// </summary>
    [Fact]
    public void Mutate_ChangesTheDocument_TheSameWayForTheSameSeed()
    {
        const string yaml = """
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    '200': { description: OK }
            components:
              schemas:
                Thing: { type: object, properties: { name: { type: string } } }
            """;

        for (var seed = 0; seed < 20; seed++)
        {
            var (first, mutation) = Mutate(yaml, seed);
            var (again, _) = Mutate(yaml, seed);
            Assert.Equal(first, again);
            Assert.NotEqual(Json(yaml), first, StringComparer.Ordinal);
            Assert.False(string.IsNullOrEmpty(mutation));
        }
    }

    private static List<Diagnostic> Run(
        IIncrementalGenerator generator,
        string markerPath,
        string specificationPath,
        string specification)
    {
        var compilation = CSharpCompilation.Create(
            "Contoso.Fuzz",
            references: CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: true),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts([
                new CompilationVerificationHarness.InMemoryAdditionalText(markerPath, "{}"),
                new CompilationVerificationHarness.InMemoryAdditionalText(specificationPath, specification),
            ]);

        // The generators' own diagnostics, and an exception that escaped a generator (CS8785)
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, CancellationToken.None);
        return [.. diagnostics];
    }

    /// <summary>
    /// One mutant of the specification (as JSON, which is YAML too) and what was changed.
    /// </summary>
    private static (string Specification, string Mutation) Mutate(
        string yaml,
        int seed)
    {
        var random = new Random(seed);
        var document = JsonNode.Parse(Json(yaml))!;
        var mutation = (random.Next(4), Nodes(document).ToList()) switch
        {
            (0, var nodes) => RemoveMember(nodes, random),
            (1, var nodes) => ChangeKind(nodes, random),
            (2, var nodes) => OddString(nodes, random),
            (_, var nodes) => RemoveComponentSchema(document, random) ?? RemoveMember(nodes, random),
        };

        return (document.ToJsonString(), mutation);
    }

    private static string RemoveMember(
        List<JsonNode> nodes,
        Random random)
    {
        var members = nodes.OfType<JsonObject>().SelectMany(o => o.Select(m => (Owner: o, m.Key))).ToList();
        var (owner, key) = members[random.Next(members.Count)];
        owner.Remove(key);
        return $"removed {owner.GetPath()}.{key}";
    }

    private static string ChangeKind(
        List<JsonNode> nodes,
        Random random)
    {
        var target = nodes.Where(n => n.Parent is not null).ElementAt(random.Next(nodes.Count(n => n.Parent is not null)));
        JsonNode? replacement = random.Next(5) switch
        {
            0 => JsonValue.Create(42),
            1 => JsonValue.Create("text"),
            2 => new JsonArray(JsonValue.Create(1)),
            3 => new JsonObject { ["x"] = 1 },
            _ => null,
        };

        var path = target.GetPath();
        Replace(target, replacement);
        return $"{path} became {replacement?.ToJsonString() ?? "null"}";
    }

    private static string OddString(
        List<JsonNode> nodes,
        Random random)
    {
        var strings = nodes.OfType<JsonValue>().Where(v => v.GetValueKind() == JsonValueKind.String && v.Parent is not null).ToList();
        string[] odd = [string.Empty, " ", "class", "123abc", "Ünïcødé-名前", "a.b/c{d}", "#/components/schemas/Missing", new('x', 600)];
        var target = strings[random.Next(strings.Count)];
        var value = odd[random.Next(odd.Length)];
        var path = target.GetPath();
        Replace(target, JsonValue.Create(value));
        return $"{path} became \"{(value.Length > 20 ? value[..20] + "..." : value)}\"";
    }

    private static string? RemoveComponentSchema(
        JsonNode document,
        Random random)
    {
        if (document["components"]?["schemas"] is not JsonObject { Count: > 0 } schemas)
        {
            return null;
        }

        var name = schemas.Select(s => s.Key).ToList()[random.Next(schemas.Count)];
        schemas.Remove(name);
        return $"removed the schema {name} (its references dangle)";
    }

    private static void Replace(
        JsonNode target,
        JsonNode? replacement)
    {
        switch (target.Parent)
        {
            case JsonObject owner:
                owner[target.GetPropertyName()] = replacement;
                break;
            case JsonArray array:
                array[target.GetElementIndex()] = replacement;
                break;
        }
    }

    private static IEnumerable<JsonNode> Nodes(JsonNode node)
    {
        yield return node;
        var children = node switch
        {
            JsonObject o => o.Select(m => m.Value),
            JsonArray a => a.AsEnumerable(),
            _ => [],
        };

        foreach (var child in children.OfType<JsonNode>())
        {
            foreach (var descendant in Nodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static string Json(string yaml)
    {
        var stream = new SharpYaml.Serialization.YamlStream();
        stream.Load(new StringReader(yaml));
        return Microsoft.OpenApi.YamlReader.YamlConverter.ToJsonNode(stream.Documents[0]).ToJsonString();
    }

    // string.GetHashCode differs per process: a seed must not
    private static int StableSeed(string text)
        => text.Aggregate(17, (hash, c) => unchecked((hash * 31) + c));
}