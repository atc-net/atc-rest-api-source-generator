namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A deprecated operation or parameter warns the code that calls it through the generated client (CS0618), while the
/// generated code itself compiles without warnings. The server is not marked: it still has to implement a deprecated
/// operation, and an obsolete handler interface would warn on every implementation.
/// </summary>
public sealed class DeprecationObsoleteTests
{
    private const string Scenario = "Deprecations";

    [Fact]
    public void TypedClient_DeprecatedOperationParameterAndProperty_WarnTheCaller()
    {
        const string consumer = """
            internal static class Consumer
            {
                public static async System.Threading.Tasks.Task UseAsync(
                    Deprecations.Generated.IDeprecationsClient client,
                    Deprecations.Generated.DeprecationsClient concrete,
                    Deprecations.Generated.ListPetsParameters parameters,
                    Deprecations.Generated.Models.Pet pet)
                {
                    await client.ListLegacyPetsAsync();
                    await concrete.ListLegacyPetsAsync();
                    _ = parameters.Tag;
                    _ = parameters.Limit;
                    _ = pet.Nickname;
                    _ = pet.Name;
                }
            }
            """;

        var warnings = ConsumerWarnings("Client-Typed", consumer);

        Assert.Equal(
            [
                "'IDeprecationsClient.ListLegacyPetsAsync(CancellationToken)'",
                "'DeprecationsClient.ListLegacyPetsAsync(CancellationToken)'",
                "'ListPetsParameters.Tag'",
                "'Pet.Nickname'",
            ],
            warnings);
    }

    [Fact]
    public void EndpointPerOperation_DeprecatedOperationAndParameter_WarnTheCaller()
    {
        const string consumer = """
            internal static class Consumer
            {
                public static void Use(
                    Deprecations.Generated.Endpoints.Interfaces.IListLegacyPetsEndpoint endpoint,
                    Deprecations.Generated.Endpoints.Interfaces.IListPetsEndpoint current,
                    Deprecations.Generated.Client.ListPetsParameters parameters)
                {
                    _ = parameters.Tag;
                }
            }
            """;

        var warnings = ConsumerWarnings("Client-Operation", consumer);

        Assert.Equal(["'IListLegacyPetsEndpoint'", "'ListPetsParameters.Tag'"], warnings);
    }

    [Fact]
    public void Server_DeprecatedOperationAndParameter_DoNotWarnTheImplementation()
    {
        const string consumer = """
            internal static class Consumer
            {
                public static void Use(
                    Deprecations.Generated.Handlers.IListLegacyPetsHandler handler,
                    Deprecations.Generated.Parameters.ListPetsParameters parameters)
                {
                    _ = parameters.Tag;
                }
            }
            """;

        var warnings = ConsumerWarnings("Server", consumer);

        Assert.Empty(warnings);
    }

    // The obsolete members the consumer is warned about, in source order; any other warning fails the test.
    private static List<string> ConsumerWarnings(
        string masterFolder,
        string consumer)
    {
        var sources = GeneratorSnapshotHarness.RunRaw(Scenario, masterFolder);
        Assert.NotEmpty(sources);
        sources.Add(("Consumer.cs", consumer));

        var warnings = CompilationVerificationHarness.CompileGeneratedSourcesForWarnings(sources);
        var unexpected = warnings.Where(w => !w.StartsWith("Consumer.cs", StringComparison.Ordinal) || !w.Contains("CS0618", StringComparison.Ordinal)).ToList();
        Assert.True(unexpected.Count == 0, string.Join("\n", unexpected));

        return warnings
            .Select(w => w.Substring(w.IndexOf("CS0618: ", StringComparison.Ordinal) + "CS0618: ".Length))
            .Select(w => w.Substring(0, w.IndexOf(" is obsolete", StringComparison.Ordinal)))
            .ToList();
    }
}