namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// What the generated server makes of a polymorphic body, on the wire. A <c>oneOf</c> without a discriminator is the
/// variant the body fits - its required members present, no member it does not have - not the first variant that
/// deserializes leniently (<c>{"sideLength":2}</c> used to be <c>Circle(Radius=0)</c>), and a body that fits no
/// variant is a 400. A discriminator of the wrong kind, an unknown value or a body that is no object is a 400, never
/// a 500; a discriminator that is not the first property is read all the same.
/// </summary>
[Collection(nameof(PolymorphismWireTests))]
[CollectionDefinition(nameof(PolymorphismWireTests), DisableParallelization = true)]
public sealed class PolymorphismWireTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /shapes:
            post:
              operationId: createShape
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Shape'
              responses:
                '200':
                  description: The same shape
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Shape'
          /pets:
            post:
              operationId: createPet
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Pet'
              responses:
                '200':
                  description: The same pet
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
          /vehicles:
            post:
              operationId: createVehicle
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Vehicle'
              responses:
                '200':
                  description: The same vehicle
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Vehicle'
        components:
          schemas:
            # oneOf without a discriminator: the body decides
            Shape:
              oneOf:
                - $ref: '#/components/schemas/Circle'
                - $ref: '#/components/schemas/Square'
            Circle:
              type: object
              required: [radius]
              properties:
                radius: { type: number, format: double }
            Square:
              type: object
              required: [sideLength]
              properties:
                sideLength: { type: number, format: double }
            # a discriminator the variants declare as a property of their own: the generated converter
            Pet:
              oneOf:
                - $ref: '#/components/schemas/Cat'
                - $ref: '#/components/schemas/Dog'
              discriminator:
                propertyName: kind
                mapping:
                  cat: '#/components/schemas/Cat'
                  dog: '#/components/schemas/Dog'
            Cat:
              type: object
              required: [kind, lives]
              properties:
                kind: { type: string }
                lives: { type: integer }
            Dog:
              type: object
              required: [kind, barks]
              properties:
                kind: { type: string }
                barks: { type: boolean }
            # a discriminator the variants require but do not declare as a property: System.Text.Json's own polymorphism
            Vehicle:
              oneOf:
                - $ref: '#/components/schemas/Car'
                - $ref: '#/components/schemas/Bike'
              discriminator:
                propertyName: type
                mapping:
                  car: '#/components/schemas/Car'
                  bike: '#/components/schemas/Bike'
            Car:
              type: object
              required: [type, doors]
              properties:
                doors: { type: integer }
            Bike:
              type: object
              required: [type, gears]
              properties:
                gears: { type: integer }
        """;

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class CreateShapeHandlerImpl : ICreateShapeHandler
        {
            public System.Threading.Tasks.Task<CreateShapeResult> ExecuteAsync(CreateShapeParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(CreateShapeResult.Ok(parameters.Request));
        }

        public sealed class CreatePetHandlerImpl : ICreatePetHandler
        {
            public System.Threading.Tasks.Task<CreatePetResult> ExecuteAsync(CreatePetParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(CreatePetResult.Ok(parameters.Request));
        }

        public sealed class CreateVehicleHandlerImpl : ICreateVehicleHandler
        {
            public System.Threading.Tasks.Task<CreateVehicleResult> ExecuteAsync(CreateVehicleParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(CreateVehicleResult.Ok(parameters.Request));
        }

        public static class PolymorphismRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<ICreateShapeHandler, CreateShapeHandlerImpl>();
                services.AddScoped<ICreatePetHandler, CreatePetHandlerImpl>();
                services.AddScoped<ICreateVehicleHandler, CreateVehicleHandlerImpl>();
            }
        }
        """";

    [Theory]
    [InlineData("""{"radius":1.5}""", """{"radius":1.5}""")]
    [InlineData("""{"sideLength":2}""", """{"sideLength":2}""")]
    public async Task OneOfWithoutDiscriminator_IsTheVariantTheBodyFits(
        string body,
        string expected)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.PolymorphismRegistration");

        var (status, _, response) = await host.SendRawAsync(HttpMethod.Post, "/shapes", accept: "application/json", body: body, contentType: "application/json");

        Assert.Equal(200, status);
        Assert.Equal(expected, response);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"radius":1,"sideLength":2}""")]
    [InlineData("""{"size":2}""")]
    [InlineData("""[1]""")]
    public async Task OneOfWithoutDiscriminator_ABodyThatFitsNoVariant_IsA400(
        string body)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.PolymorphismRegistration");

        var (status, _, _) = await host.SendRawAsync(HttpMethod.Post, "/shapes", accept: "application/json", body: body, contentType: "application/json");

        Assert.Equal(400, status);
    }

    [Theory]
    [InlineData("/pets", """{"kind":"cat","lives":9}""", """{"kind":"cat","lives":9}""")]
    [InlineData("/pets", """{"lives":9,"kind":"cat"}""", """{"kind":"cat","lives":9}""")]
    [InlineData("/vehicles", """{"type":"car","doors":4}""", """{"type":"car","doors":4}""")]
    [InlineData("/vehicles", """{"doors":4,"type":"car"}""", """{"type":"car","doors":4}""")]
    public async Task Discriminator_IsReadWhereverItStands(
        string path,
        string body,
        string expected)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.PolymorphismRegistration");

        var (status, _, response) = await host.SendRawAsync(HttpMethod.Post, path, accept: "application/json", body: body, contentType: "application/json");

        Assert.Equal(200, status);
        Assert.Equal(expected, response);
    }

    [Theory]
    [InlineData("/pets", """{"kind":1,"lives":9}""")]
    [InlineData("/pets", """{"kind":"hamster","lives":9}""")]
    [InlineData("/pets", """{"lives":9}""")]
    [InlineData("/pets", """[1]""")]
    [InlineData("/pets", """"cat"""")]
    [InlineData("/vehicles", """{"type":1,"doors":4}""")]
    [InlineData("/vehicles", """{"type":"boat","doors":4}""")]
    [InlineData("/vehicles", """[1]""")]
    public async Task Discriminator_OfTheWrongKindUnknownOrMissing_IsA400NotA500(
        string path,
        string body)
    {
        await using var host = await XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.PolymorphismRegistration");

        var (status, _, response) = await host.SendRawAsync(HttpMethod.Post, path, accept: "application/json", body: body, contentType: "application/json");

        Assert.True(status == 400, $"{status}: {response}");
    }
}