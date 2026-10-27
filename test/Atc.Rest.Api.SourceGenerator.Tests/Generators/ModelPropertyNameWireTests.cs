namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The property names a generated model is written with are the names the specification declares, whatever their
/// casing: a minimal API writes with ASP.NET Core's default <c>JsonOptions</c> (camelCase naming policy), so a
/// spec property whose name already is the C# name (<c>UserId</c>) needs its <c>[JsonPropertyName]</c> too.
/// </summary>
public class ModelPropertyNameWireTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /people:
            get:
              operationId: listPeople
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Person'
        components:
          schemas:
            Person:
              type: object
              properties:
                user_name:
                  type: string
                UserId:
                  type: string
                displayName:
                  type: string
                HTTPStatus:
                  type: integer
        """;

    [Fact]
    public void ServerModel_IsWrittenWithTheSpecificationsPropertyNames()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(
            new ApiServerGenerator(),
            ".atc-rest-api-server",
            ErrorResponseBodyRuntimeTests.ServerMarker,
            includeMinimalApi: true,
            Yaml);
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        var personType = assembly.GetTypes().Single(t => t.Name == "Person");

        // Every property set, whatever the model's shape (record or class).
        var person = CreateWithValues(personType);

        // The options a minimal API writes with.
        var options = new Microsoft.AspNetCore.Http.Json.JsonOptions().SerializerOptions;
        var json = JsonSerializer.Serialize(person, personType, options);
        var keys = JsonNode.Parse(json)!.AsObject().Select(p => p.Key).ToList();

        Assert.Equal(["user_name", "UserId", "displayName", "HTTPStatus"], keys);
    }

    private static object CreateWithValues(Type type)
    {
        var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var arguments = constructor.GetParameters().Select(p => SampleValue(p.ParameterType)).ToArray();
        var instance = constructor.Invoke(arguments);
        foreach (var property in type.GetProperties().Where(p => p.CanWrite && p.GetSetMethod() is not null))
        {
            property.SetValue(instance, SampleValue(property.PropertyType));
        }

        return instance;

        static object? SampleValue(Type type)
            => (Nullable.GetUnderlyingType(type) ?? type) == typeof(int) ? 1 : "x";
    }
}