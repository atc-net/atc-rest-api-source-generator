namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A <c>type: string</c> is a JSON string on the wire whatever its <c>format</c>: an integer format (<c>int32</c>,
/// <c>int64</c>, <c>long</c>) is the integer in the generated model, read from and written as the JSON string - a number
/// on the wire was what a server that sends the string could not read - and a format the generator has no type for
/// (<c>double</c>, an unknown one) is a string. The items of an array and the values of a map are read and written the
/// same way; a length or a pattern, which is the string's, is not validated on the integer.
/// </summary>
public sealed class StringFormatGenerationTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Formats
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: The things
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
        components:
          schemas:
            Thing:
              type: object
              required: [a, b, c, d, e, f, g, h, i, j, k]
              properties:
                a: { type: string, format: long }
                b: { type: string, format: int64 }
                c: { type: string, format: int32 }
                d: { type: string, format: double }
                e: { type: string, format: whatever }
                f: { type: string, format: email }
                g: { type: string, format: uuid }
                h: { type: integer, format: int64 }
                i: { type: array, items: { type: string, format: int64 } }
                j: { type: object, additionalProperties: { type: string, format: int32 } }
                k: { type: string, format: int64, maxLength: 19, pattern: '^[0-9]+$' }
        """;

    private const string ProbeSource = """"
        using System.Text.Json;

        namespace Formats.RuntimeTest;

        public static class RoundTripProbe
        {
            public static string Run(string json)
            {
                var thingType = typeof(RoundTripProbe).Assembly.GetTypes().Single(t => t.Name == "Thing");
                var thing = JsonSerializer.Deserialize(json, thingType, JsonSerializerOptions.Web)!;
                return JsonSerializer.Serialize(thing, thingType, JsonSerializerOptions.Web);
            }
        }
        """";

    private const string Json = """{"a":"9007199254740993","b":"-5","c":"7","d":"1.5","e":"x","f":"a@b.dk","g":"6f9619ff-8b86-d011-b42d-00c04fc964ff","h":12,"i":["1","2"],"j":{"x":"3"},"k":"42"}""";

    [Fact]
    public void AnIntegerFormatOnAString_IsTheIntegerReadAndWrittenAsTheString()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml);

        var model = sources.Single(s => s.HintName.EndsWith(".Models.Thing.g.cs", StringComparison.Ordinal)).Source;
        const string asString = @"global::System\.Text\.Json\.Serialization\.JsonNumberHandling\(global::System\.Text\.Json\.Serialization\.JsonNumberHandling\.AllowReadingFromString \| global::System\.Text\.Json\.Serialization\.JsonNumberHandling\.WriteAsString\)\] ";
        foreach (var (type, name) in new[] { ("long", "A"), ("long", "B"), ("int", "C"), ("List<long>", "I"), ("Dictionary<string, int>", "J"), ("long", "K") })
        {
            Assert.True(Regex.IsMatch(model, $@"{asString}{type} {name}\b", RegexOptions.None, TimeSpan.FromSeconds(1)), model);
        }

        // A format with no type of its own is a string; one that names a string type keeps it; an integer is a number
        foreach (var name in new[] { "D", "E", "F" })
        {
            Assert.True(Regex.IsMatch(model, $@"\bstring {name}\b", RegexOptions.None, TimeSpan.FromSeconds(1)), model);
        }

        Assert.Matches(new Regex(@"\bGuid G\b", RegexOptions.None, TimeSpan.FromSeconds(1)), model);
        Assert.Equal(6, Regex.Matches(model, "JsonNumberHandling.WriteAsString", RegexOptions.None, TimeSpan.FromSeconds(1)).Count);

        // A length or a pattern is the string's: on the integer it would throw when it validates
        Assert.DoesNotMatch(new Regex(@"(MaxLength|RegularExpression)[^
]*long K", RegexOptions.None, TimeSpan.FromSeconds(1)), model);
    }

    [Fact]
    public void AnIntegerFormatOnAString_RoundTripsTheString()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml);
        sources.Add(("RoundTripProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var actual = (string)assembly.GetType("Formats.RuntimeTest.RoundTripProbe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [Json])!;

        Assert.Equal(Json, actual);
    }
}
