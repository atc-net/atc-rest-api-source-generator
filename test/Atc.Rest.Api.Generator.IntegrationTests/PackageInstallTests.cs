namespace Atc.Rest.Api.Generator.IntegrationTests;

/// <summary>
/// The generator as a consumer gets it: packed, restored from a feed and loaded by the compiler - not the project
/// reference every sample and test uses. A dependency left out of <c>analyzers/dotnet/cs</c> shows only here.
/// </summary>
/// <remarks>
/// The consumers are strict: <c>ImplicitUsings</c> off, nullable on, warnings as errors. The clients, and the server on
/// the released Atc.Rest.MinimalApi as well as on the plain host without it.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PackageInstallTests(PackedGeneratorFixture package) : IClassFixture<PackedGeneratorFixture>
{
    private const string StrictProperties = """
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>disable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
        """;

    /// <summary>
    /// The HTTP server the reflection-free apps talk to: a list of one pet, and a created pet for a POST, whose body it
    /// keeps.
    /// </summary>
    private const string StubHandlerSource = """
            internal sealed class StubHandler : HttpMessageHandler
            {
                public static string? LastBody { get; private set; }

                protected override async Task<HttpResponseMessage> SendAsync(
                    HttpRequestMessage request,
                    CancellationToken cancellationToken)
                {
                    if (request.Method == HttpMethod.Post)
                    {
                        LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                        return Json(HttpStatusCode.Created, "{\"id\":2,\"name\":\"Rex\",\"kind\":\"Dog\"}");
                    }

                    return Json(HttpStatusCode.OK, "[{\"id\":1,\"name\":\"Tom\",\"kind\":\"Cat\"}]");
                }

                private static HttpResponseMessage Json(
                    HttpStatusCode statusCode,
                    string json)
                    => new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            }
            """;

    /// <summary>
    /// The pets API every consumer but the first is generated from: a list, and a create with a required name.
    /// </summary>
    private const string ContosoYaml = """
        openapi: 3.0.3
        info:
          title: Contoso
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                '200':
                  description: The pets
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Pet'
            post:
              operationId: createPet
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/NewPet'
              responses:
                '201':
                  description: Created
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
        components:
          schemas:
            Pet:
              title: Pet
              type: object
              required: [id, name, kind]
              properties:
                id: { type: integer, format: int64 }
                name: { type: string }
                kind: { $ref: '#/components/schemas/PetKind' }
            NewPet:
              title: NewPet
              type: object
              required: [name, kind]
              properties:
                name: { type: string }
                kind: { $ref: '#/components/schemas/PetKind' }
            PetKind:
              type: string
              enum: [Cat, Dog]
        """;

    /// <summary>
    /// A server app on the generated code: handlers for the two operations, the generated <c>AddContosoApi()</c> /
    /// <c>MapContosoApi()</c>, and calls to itself on a random port - a list, a create, and a create without the
    /// required name.
    /// </summary>
    private const string ServerProgramSource = """
        using System;
        using System.Linq;
        using System.Net.Http;
        using System.Text;
        using System.Threading;
        using System.Threading.Tasks;
        using Contoso.Generated.Extensions;
        using Contoso.Generated.Handlers;
        using Contoso.Generated.Models;
        using Contoso.Generated.Parameters;
        using Contoso.Generated.Results;
        using Microsoft.AspNetCore.Builder;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Logging;

        var builder = WebApplication.CreateSlimBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services.AddScoped<IListPetsHandler, ListPetsHandler>();
        builder.Services.AddScoped<ICreatePetHandler, CreatePetHandler>();
        builder.Services.AddContosoApi();

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapContosoApi();
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        Console.WriteLine("listed: " + await client.GetStringAsync(new Uri("/pets", UriKind.Relative)));

        using var created = await client.PostAsync(new Uri("/pets", UriKind.Relative), new StringContent("{\"name\":\"Rex\",\"kind\":\"Dog\"}", Encoding.UTF8, "application/json"));
        Console.WriteLine($"created: {(int)created.StatusCode} {await created.Content.ReadAsStringAsync()}");

        using var invalid = await client.PostAsync(new Uri("/pets", UriKind.Relative), new StringContent("{\"kind\":\"Dog\"}", Encoding.UTF8, "application/json"));
        Console.WriteLine($"invalid: {(int)invalid.StatusCode} {invalid.Content.Headers.ContentType?.MediaType}");

        await app.StopAsync();

        internal sealed class ListPetsHandler : IListPetsHandler
        {
            public Task<ListPetsResult> ExecuteAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(ListPetsResult.Ok([new Pet(1, "Tom", PetKind.Cat)]));
        }

        internal sealed class CreatePetHandler : ICreatePetHandler
        {
            public Task<CreatePetResult> ExecuteAsync(
                CreatePetParameters parameters,
                CancellationToken cancellationToken = default)
                => Task.FromResult(CreatePetResult.Created(new Pet(2, parameters.Request.Name, parameters.Request.Kind)));
        }
        """;

    /// <summary>
    /// The trim- and Native AOT-ready Atc.Rest.Client the generated per-operation client needs.
    /// </summary>
    private const string AtcRestClientReference = """<PackageReference Include="Atc.Rest.Client" Version="2.6.0" />""";

    [Fact]
    public async Task APackedGenerator_GeneratesAClientThatCompiles_InAStrictConsumer()
    {
        var consumer = package.NewDirectory("consumer");
        WriteConsumer(consumer);

        var build = await PackedGeneratorFixture.RunDotnetAsync(consumer, "build", "-p:UseSharedCompilation=false", "-p:EmitCompilerGeneratedFiles=true");
        Assert.True(build.ExitCode == 0, $"The consumer did not build:\n{build.Output}");

        // Validation ran from the package too: the sample specification's style warnings are reported
        Assert.Contains("ATC_API_SCH003", build.Output, StringComparison.Ordinal);

        // The generator ran from the package: its files are there, and the consumer's own code used them
        var generated = Directory.GetFiles(Path.Combine(consumer, "obj"), "*.g.cs", SearchOption.AllDirectories);
        Assert.Contains(generated, f => f.Contains("Atc.Rest.Api.SourceGenerator", StringComparison.Ordinal));
    }

    /// <summary>
    /// A server from the packed generator in a strict consumer, on the released Atc.Rest.MinimalApi (its validation
    /// filter and error handling) and on the plain host without it (the generated host wiring alone): it builds, starts,
    /// answers the list and the create, and answers a create without the required name with a 400 problem.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APackedGenerator_GeneratesAServerThatRuns_InAStrictConsumer(
        bool withMinimalApi)
    {
        var server = package.NewDirectory(withMinimalApi ? "server-minimalapi" : "server-plain");
        WriteServer(server, withMinimalApi);

        var run = await PackedGeneratorFixture.RunDotnetAsync(server, "run", "-p:UseSharedCompilation=false");
        Assert.True(run.ExitCode == 0, $"The server did not run:\n{run.Output}");

        Assert.Contains("""listed: [{"id":1,"name":"Tom","kind":"Cat"}]""", run.Output, StringComparison.Ordinal);
        Assert.Contains("""created: 201 {"id":2,"name":"Rex","kind":"Dog"}""", run.Output, StringComparison.Ordinal);
        Assert.Contains("invalid: 400 application/problem+json", run.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A typed client in an app with reflection-based serialization off (<c>JsonSerializerIsReflectionEnabledByDefault</c>,
    /// as Native AOT and trimmed apps have it): the app's <c>JsonSerializerContext</c>, added through the client's
    /// <c>configureJsonSerializerOptions</c>, is all the client needs - and without it the client fails rather than
    /// falling back to reflection. The client is generated in a library the app references, as the context's
    /// generator only sees types that are compiled already.
    /// </summary>
    [Fact]
    public async Task ATypedClient_WorksWithReflectionOff_FromTheAppsJsonSerializerContext()
    {
        var library = package.NewDirectory("aot-library");
        var app = package.NewDirectory("aot-app");
        WriteClientLibrary(library, """{ "generationMode": "TypedClient", "extensibleEnums": false }""", isAotCompatible: true);
        WriteReflectionFreeApp(app, library);

        var run = await PackedGeneratorFixture.RunDotnetAsync(app, "run", "-p:UseSharedCompilation=false");
        Assert.True(run.ExitCode == 0, $"The app did not run:\n{run.Output}");

        Assert.Contains("reflection: False", run.Output, StringComparison.Ordinal);
        Assert.Contains("listed: 1 Tom Cat", run.Output, StringComparison.Ordinal);
        Assert.Contains("""sent: {"name":"Rex","kind":"Dog"}""", run.Output, StringComparison.Ordinal);
        Assert.Contains("created: 2 Rex Dog", run.Output, StringComparison.Ordinal);
        Assert.Contains("without the context: failed", run.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A per-operation client in an app with reflection-based serialization off: the app's
    /// <c>JsonSerializerContext</c>, added through the endpoints' <c>configureJsonSerializerOptions</c>, joins
    /// Atc.Rest.Client's default options next to the generated enum converter - and without it the client fails
    /// rather than falling back to reflection.
    /// </summary>
    [Fact]
    public async Task APerOperationClient_WorksWithReflectionOff_FromTheAppsJsonSerializerContext()
    {
        var library = package.NewDirectory("epo-aot-library");
        var app = package.NewDirectory("epo-aot-app");
        WriteClientLibrary(library, """{ "generationMode": "EndpointPerOperation", "extensibleEnums": false }""", isAotCompatible: false);
        WritePerOperationReflectionFreeApp(app, library);

        var run = await PackedGeneratorFixture.RunDotnetAsync(app, "run", "-p:UseSharedCompilation=false");
        Assert.True(run.ExitCode == 0, $"The app did not run:\n{run.Output}");

        Assert.Contains("reflection: False", run.Output, StringComparison.Ordinal);
        Assert.Contains("listed: 1 Tom Cat", run.Output, StringComparison.Ordinal);
        Assert.Contains("""sent: {"name":"Rex","kind":"Dog"}""", run.Output, StringComparison.Ordinal);
        Assert.Contains("created: 2 Rex Dog", run.Output, StringComparison.Ordinal);
        Assert.Contains("without the context: failed", run.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reflection-free typed-client app as a Native AOT binary: the ILCompiler reports no trim or AOT warning for the
    /// generated client, and the native app does what the JIT-compiled one does. Skipped where no native toolchain is
    /// installed.
    /// </summary>
    [Fact]
    public Task ATypedClient_RunsAsANativeAotBinary()
    {
        var library = package.NewDirectory("native-library");
        var app = package.NewDirectory("native-app");
        WriteClientLibrary(library, """{ "generationMode": "TypedClient", "extensibleEnums": false }""", isAotCompatible: true);
        WriteReflectionFreeApp(app, library, publishAot: true);

        return AssertRunsAsNativeAotBinaryAsync(app);
    }

    /// <summary>
    /// The reflection-free per-operation app as a Native AOT binary, as <see cref="ATypedClient_RunsAsANativeAotBinary"/>.
    /// </summary>
    [Fact]
    public Task APerOperationClient_RunsAsANativeAotBinary()
    {
        var library = package.NewDirectory("epo-native-library");
        var app = package.NewDirectory("epo-native-app");
        WriteClientLibrary(library, """{ "generationMode": "EndpointPerOperation", "extensibleEnums": false }""", isAotCompatible: true);
        WritePerOperationReflectionFreeApp(app, library, publishAot: true);

        return AssertRunsAsNativeAotBinaryAsync(app);
    }

    private static async Task AssertRunsAsNativeAotBinaryAsync(string app)
    {
        if (!NativeAotToolchain.IsAvailable(out var skipReason))
        {
            Assert.Skip(skipReason);
        }

        var output = Path.Combine(app, "native");
        var publish = await PackedGeneratorFixture.RunDotnetAsync(app, "publish", "-c", "Release", "-p:UseSharedCompilation=false", "-o", output);
        Assert.True(publish.ExitCode == 0, $"The app did not publish:\n{publish.Output}");

        // The ILCompiler's own analysis of the whole app - the generated client, Atc.Rest.Client and the framework
        var aotWarnings = publish.Output
            .Split('\n')
            .Where(line => line.Contains("warning IL", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.True(aotWarnings.Count == 0, "Trim or AOT warnings:\n" + string.Join("\n", aotWarnings));

        var executable = Path.Combine(output, OperatingSystem.IsWindows() ? "ContosoApp.exe" : "ContosoApp");
        var run = await PackedGeneratorFixture.RunAsync(executable, app);
        Assert.True(run.ExitCode == 0, $"The native app did not run:\n{run.Output}");

        Assert.Contains("reflection: False", run.Output, StringComparison.Ordinal);
        Assert.Contains("listed: 1 Tom Cat", run.Output, StringComparison.Ordinal);
        Assert.Contains("""sent: {"name":"Rex","kind":"Dog"}""", run.Output, StringComparison.Ordinal);
        Assert.Contains("created: 2 Rex Dog", run.Output, StringComparison.Ordinal);
        Assert.Contains("without the context: failed", run.Output, StringComparison.Ordinal);
    }

    private void WriteServer(
        string server,
        bool withMinimalApi)
    {
        File.WriteAllText(Path.Combine(server, "Contoso.yaml"), ContosoYaml);
        File.WriteAllText(Path.Combine(server, ".atc-rest-api-server"), "{}");
        File.WriteAllText(Path.Combine(server, "Program.cs"), ServerProgramSource);
        File.WriteAllText(
            Path.Combine(server, "ContosoServer.csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk.Web">
               <PropertyGroup>
                 {StrictProperties}
                 <OutputType>Exe</OutputType>
               </PropertyGroup>
               <ItemGroup>
                 <PackageReference Include="Atc.Rest.Api.SourceGenerator" Version="{package.Version}" />
                 {(withMinimalApi ? """<PackageReference Include="Atc.Rest.MinimalApi" Version="3.0.0" />""" : string.Empty)}
               </ItemGroup>
               <ItemGroup>
                 <AdditionalFiles Include="Contoso.yaml" />
                 <AdditionalFiles Include=".atc-rest-api-server" />
               </ItemGroup>
             </Project>
             """);
        package.WriteNuGetConfig(server);
    }

    private void WriteConsumer(string consumer)
    {
        File.Copy(
            Path.Combine(package.RepositoryRoot, "sample", "PetStoreSimple", "PetStoreSimple.yaml"),
            Path.Combine(consumer, "PetStoreSimple.yaml"));
        File.WriteAllText(
            Path.Combine(consumer, ".atc-rest-api-client"),
            """{ "generationMode": "EndpointPerOperation", "httpClientName": "PetStoreSimple-ApiClient" }""");

        File.WriteAllText(
            Path.Combine(consumer, "Consumer.csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 {StrictProperties}
                 <!-- The sample specification's style warnings: reported, not the subject here -->
                 <WarningsNotAsErrors>ATC_API_SCH001;ATC_API_SCH003;ATC_API_OPR003;ATC_API_OPR014</WarningsNotAsErrors>
               </PropertyGroup>
               <ItemGroup>
                 <PackageReference Include="Atc.Rest.Api.SourceGenerator" Version="{package.Version}" />
                 {AtcRestClientReference}
                 <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.13" />
               </ItemGroup>
               <ItemGroup>
                 <AdditionalFiles Include="PetStoreSimple.yaml" />
                 <AdditionalFiles Include=".atc-rest-api-client" />
               </ItemGroup>
             </Project>
             """);

        // The consumer's own code uses the generated client, so the build fails if nothing was generated
        File.WriteAllText(
            Path.Combine(consumer, "UsesTheClient.cs"),
            """
            namespace Consumer;

            public static class UsesTheClient
            {
                public static System.Type Model => typeof(PetStoreSimple.Generated.Models.Pet);
            }
            """);

        package.WriteNuGetConfig(consumer);
    }

    private void WriteClientLibrary(
        string library,
        string marker,
        bool isAotCompatible)
    {
        File.WriteAllText(Path.Combine(library, "Contoso.yaml"), ContosoYaml);
        File.WriteAllText(Path.Combine(library, ".atc-rest-api-client"), marker);

        File.WriteAllText(
            Path.Combine(library, "ContosoClientLibrary.csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 {StrictProperties}
                 <IsAotCompatible>{(isAotCompatible ? "true" : "false")}</IsAotCompatible>
               </PropertyGroup>
               <ItemGroup>
                 <PackageReference Include="Atc.Rest.Api.SourceGenerator" Version="{package.Version}" />
                 {AtcRestClientReference}
                 <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.13" />
               </ItemGroup>
               <ItemGroup>
                 <AdditionalFiles Include="Contoso.yaml" />
                 <AdditionalFiles Include=".atc-rest-api-client" />
               </ItemGroup>
             </Project>
             """);

        package.WriteNuGetConfig(library);
    }

    private void WriteReflectionFreeApp(
        string app,
        string library,
        bool publishAot = false)
    {
        WriteReflectionFreeAppProject(app, library, publishAot);

        File.WriteAllText(
            Path.Combine(app, "Program.cs"),
            """
            using System;
            using System.Collections.Generic;
            using System.Net;
            using System.Net.Http;
            using System.Text;
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using System.Threading;
            using System.Threading.Tasks;
            using Contoso.Generated;
            using Contoso.Generated.Models;
            using Microsoft.Extensions.DependencyInjection;

            Console.WriteLine($"reflection: {JsonSerializer.IsReflectionEnabledByDefault}");

            var services = new ServiceCollection();
            services
                .AddContosoClient(
                    client => client.BaseAddress = new Uri("http://contoso.test/"),
                    options => options.TypeInfoResolverChain.Insert(0, AppJsonContext.Default))
                .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
            var client = services.BuildServiceProvider().GetRequiredService<IContosoClient>();

            var pets = await client.ListPetsAsync();
            Console.WriteLine($"listed: {pets.Count} {pets[0].Name} {pets[0].Kind}");

            var created = await client.CreatePetAsync(new CreatePetParameters(new NewPet("Rex", PetKind.Dog)));
            Console.WriteLine($"sent: {StubHandler.LastBody}");
            Console.WriteLine($"created: {created.Id} {created.Name} {created.Kind}");

            try
            {
                using var http = new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://contoso.test/") };
                await new ContosoClient(http).ListPetsAsync();
                Console.WriteLine("without the context: read");
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
            {
                Console.WriteLine("without the context: failed");
            }

            [JsonSerializable(typeof(List<Pet>))]
            [JsonSerializable(typeof(Pet))]
            [JsonSerializable(typeof(NewPet))]
            internal sealed partial class AppJsonContext : JsonSerializerContext;

            """ + StubHandlerSource);

        package.WriteNuGetConfig(app);
    }

    // PublishAot in the app's project, not on the command line: a global property reaches every referenced project,
    // and a netstandard2.0 build of one cannot be compiled ahead of time (NETSDK1207)
    private static void WriteReflectionFreeAppProject(
        string app,
        string library,
        bool publishAot)
        => File.WriteAllText(
            Path.Combine(app, "ContosoApp.csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 {StrictProperties}
                 <OutputType>Exe</OutputType>
                 <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
                 <PublishAot>{(publishAot ? "true" : "false")}</PublishAot>
               </PropertyGroup>
               <ItemGroup>
                 <ProjectReference Include="{Path.Combine(library, "ContosoClientLibrary.csproj")}" />
               </ItemGroup>
             </Project>
             """);

    private void WritePerOperationReflectionFreeApp(
        string app,
        string library,
        bool publishAot = false)
    {
        WriteReflectionFreeAppProject(app, library, publishAot);

        File.WriteAllText(
            Path.Combine(app, "Program.cs"),
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Net;
            using System.Net.Http;
            using System.Text;
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using System.Threading;
            using System.Threading.Tasks;
            using Contoso.Generated;
            using Contoso.Generated.Client;
            using Contoso.Generated.Endpoints.Interfaces;
            using Contoso.Generated.Models;
            using Microsoft.Extensions.DependencyInjection;

            Console.WriteLine($"reflection: {JsonSerializer.IsReflectionEnabledByDefault}");

            var services = new ServiceCollection();
            services.AddContosoEndpoints(options => options.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));
            services
                .AddHttpClient(Constants.HttpClientName, client => client.BaseAddress = new Uri("http://contoso.test/"))
                .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
            var provider = services.BuildServiceProvider();

            var listed = await provider.GetRequiredService<IListPetsEndpoint>().ExecuteAsync();
            var pets = listed.OkContent.ToList();
            Console.WriteLine($"listed: {pets.Count} {pets[0].Name} {pets[0].Kind}");

            var created = await provider.GetRequiredService<ICreatePetEndpoint>().ExecuteAsync(new CreatePetParameters(new NewPet("Rex", PetKind.Dog)));
            Console.WriteLine($"sent: {string.Concat(StubHandler.LastBody!.Where(c => !char.IsWhiteSpace(c)))}");
            Console.WriteLine($"created: {created.CreatedContent.Id} {created.CreatedContent.Name} {created.CreatedContent.Kind}");

            try
            {
                var bare = new ServiceCollection();
                bare.AddContosoEndpoints();
                bare
                    .AddHttpClient(Constants.HttpClientName, client => client.BaseAddress = new Uri("http://contoso.test/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                await bare.BuildServiceProvider().GetRequiredService<IListPetsEndpoint>().ExecuteAsync();
                Console.WriteLine("without the context: read");
            }
            catch (Exception ex) when (ex.InnerException is NotSupportedException)
            {
                // Atc.Rest.Client wraps it in its RestClientDeserializationException
                Console.WriteLine("without the context: failed");
            }

            [JsonSerializable(typeof(IEnumerable<Pet>))]
            [JsonSerializable(typeof(List<Pet>))]
            [JsonSerializable(typeof(Pet))]
            [JsonSerializable(typeof(NewPet))]
            internal sealed partial class AppJsonContext : JsonSerializerContext;

            """ + StubHandlerSource);

        package.WriteNuGetConfig(app);
    }
}