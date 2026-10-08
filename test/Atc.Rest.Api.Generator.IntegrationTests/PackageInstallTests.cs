namespace Atc.Rest.Api.Generator.IntegrationTests;

/// <summary>
/// The generator as a consumer gets it: packed, restored from a feed and loaded by the compiler - not the project
/// reference every sample and test uses. A dependency left out of <c>analyzers/dotnet/cs</c> shows only here.
/// </summary>
/// <remarks>
/// The consumers are strict: <c>ImplicitUsings</c> off, nullable on, warnings as errors. Clients only - server code
/// needs Atc.Rest.MinimalApi 3.0, which is not on nuget.org yet.
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
        WriteTypedClientLibrary(library);
        WriteReflectionFreeApp(app, library);

        var run = await PackedGeneratorFixture.RunDotnetAsync(app, "run", "-p:UseSharedCompilation=false");
        Assert.True(run.ExitCode == 0, $"The app did not run:\n{run.Output}");

        Assert.Contains("reflection: False", run.Output, StringComparison.Ordinal);
        Assert.Contains("listed: 1 Tom Cat", run.Output, StringComparison.Ordinal);
        Assert.Contains("""sent: {"name":"Rex","kind":"Dog"}""", run.Output, StringComparison.Ordinal);
        Assert.Contains("created: 2 Rex Dog", run.Output, StringComparison.Ordinal);
        Assert.Contains("without the context: failed", run.Output, StringComparison.Ordinal);
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
                 <PackageReference Include="Atc.Rest.Client" Version="2.1.0" />
                 <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.12" />
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

    private void WriteTypedClientLibrary(string library)
    {
        File.WriteAllText(
            Path.Combine(library, "Contoso.yaml"),
            """
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
            """);
        File.WriteAllText(
            Path.Combine(library, ".atc-rest-api-client"),
            """{ "generationMode": "TypedClient", "extensibleEnums": false }""");

        File.WriteAllText(
            Path.Combine(library, "ContosoClientLibrary.csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 {StrictProperties}
                 <IsAotCompatible>true</IsAotCompatible>
               </PropertyGroup>
               <ItemGroup>
                 <PackageReference Include="Atc.Rest.Api.SourceGenerator" Version="{package.Version}" />
                 <PackageReference Include="Atc.Rest.Client" Version="2.1.0" />
                 <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.12" />
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
        string library)
    {
        File.WriteAllText(
            Path.Combine(app, "ContosoApp.csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 {StrictProperties}
                 <OutputType>Exe</OutputType>
                 <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
               </PropertyGroup>
               <ItemGroup>
                 <ProjectReference Include="{Path.Combine(library, "ContosoClientLibrary.csproj")}" />
               </ItemGroup>
             </Project>
             """);

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
            """);

        package.WriteNuGetConfig(app);
    }
}