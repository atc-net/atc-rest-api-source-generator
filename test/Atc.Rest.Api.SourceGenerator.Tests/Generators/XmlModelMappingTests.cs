namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The generated XML mapping of a model (<c>{Model}Xml</c>): the element and attribute names, namespaces and array
/// wrapping the OpenAPI <c>xml</c> keyword describes - the golden documents are the XML Object examples of the
/// OpenAPI 3.0.3 specification - and the reading side (round trip, foreign prefixes, whitespace, missing and invalid
/// values, no DTD). Server, EndpointPerOperation client and TypedClient get the same mapping.
/// </summary>
public sealed class XmlModelMappingTests
{
    internal const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /xml/plain:
            get:
              operationId: getPlain
              responses:
                '200': { description: OK, content: { application/xml: { schema: { $ref: '#/components/schemas/PlainPerson' } } } }
          /xml/renamed:
            get:
              operationId: getRenamed
              responses:
                '200': { description: OK, content: { application/xml: { schema: { $ref: '#/components/schemas/RenamedPerson' } } } }
          /xml/person:
            get:
              operationId: getPerson
              responses:
                '200': { description: OK, content: { application/xml: { schema: { $ref: '#/components/schemas/Person' } } } }
          /xml/zoos:
            get:
              operationId: getZoos
              responses:
                '200':
                  description: OK
                  content:
                    application/xml:
                      schema:
                        type: object
                        properties:
                          z1: { $ref: '#/components/schemas/Zoo1' }
                          z2: { $ref: '#/components/schemas/Zoo2' }
                          z3: { $ref: '#/components/schemas/Zoo3' }
                          z4: { $ref: '#/components/schemas/Zoo4' }
                          z5: { $ref: '#/components/schemas/Zoo5' }
                          z6: { $ref: '#/components/schemas/Zoo6' }
                          z7: { $ref: '#/components/schemas/Zoo7' }
          /xml/pet:
            get:
              operationId: getPet
              responses:
                '200': { description: OK, content: { application/xml: { schema: { $ref: '#/components/schemas/Pet' } } } }
          /xml/doc:
            get:
              operationId: getDoc
              responses:
                '200': { description: OK, content: { text/xml: { schema: { $ref: '#/components/schemas/Doc' } } } }
          /xml/legacy:
            get:
              operationId: getLegacy
              responses:
                '200': { description: OK, content: { application/xml: { schema: { $ref: '#/components/schemas/Legacy' } } } }
          /xml/task:
            get:
              operationId: getTask
              responses:
                '200': { description: OK, content: { application/xml: { schema: { $ref: '#/components/schemas/Task' } } } }
          /xml/sample:
            get:
              operationId: getSample
              responses:
                '200': { description: OK, content: { application/vnd.acme+xml: { schema: { $ref: '#/components/schemas/Sample' } } } }
          /json/only:
            get:
              operationId: getJsonOnly
              responses:
                '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/JsonOnly' } } } }
        components:
          schemas:
            PlainPerson:
              type: object
              properties:
                name: { type: string }
            RenamedPerson:
              type: object
              properties:
                name: { type: string, xml: { name: surname } }
            Person:
              type: object
              properties:
                id: { type: integer, format: int32, xml: { attribute: true } }
                name: { type: string, xml: { namespace: 'http://example.com/schema/sample', prefix: sample } }
            Zoo1:
              type: object
              properties:
                animals: { type: array, items: { type: string } }
            Zoo2:
              type: object
              properties:
                animals: { type: array, items: { type: string, xml: { name: animal } } }
            Zoo3:
              type: object
              properties:
                animals: { type: array, items: { type: string, xml: { name: animal } }, xml: { name: aliens } }
            Zoo4:
              type: object
              properties:
                animals: { type: array, items: { type: string }, xml: { wrapped: true } }
            Zoo5:
              type: object
              properties:
                animals: { type: array, items: { type: string, xml: { name: animal } }, xml: { wrapped: true } }
            Zoo6:
              type: object
              properties:
                animals: { type: array, items: { type: string, xml: { name: animal } }, xml: { name: aliens, wrapped: true } }
            Zoo7:
              type: object
              properties:
                animals: { type: array, items: { type: string }, xml: { name: aliens, wrapped: true } }
            Category:
              type: object
              properties:
                id: { type: integer, format: int64 }
                name: { type: string }
              xml: { name: category }
            Tag:
              type: object
              properties:
                id: { type: integer, format: int64 }
                name: { type: string }
              xml: { name: tag }
            Pet:
              type: object
              required: [name, photoUrls]
              properties:
                id: { type: integer, format: int64 }
                name: { type: string }
                category: { $ref: '#/components/schemas/Category' }
                photoUrls:
                  type: array
                  xml: { wrapped: true }
                  items: { type: string, xml: { name: photoUrl } }
                tags:
                  type: array
                  xml: { wrapped: true }
                  items: { $ref: '#/components/schemas/Tag' }
                status: { type: string, enum: [available, pending, sold] }
              xml: { name: pet }
            Doc:
              type: object
              properties:
                title: { type: string }
              xml: { namespace: 'http://example.com/doc' }
            Legacy:
              type: object
              properties:
                code: { type: string }
              xml: { name: '##default' }
            Task:
              type: object
              properties:
                title: { type: string }
            Sample:
              type: object
              required: [flag, count]
              properties:
                flag: { type: boolean }
                count: { type: integer, format: int64 }
                ratio: { type: number, format: double }
                price: { type: number }
                at: { type: string, format: date-time }
                day: { type: string, format: date }
                key: { type: string, format: uuid }
                link: { type: string, format: uri }
                blob: { type: string, format: byte }
                labels: { type: object, additionalProperties: { type: string } }
                level: { $ref: '#/components/schemas/Level' }
            Level:
              type: string
              enum: [low, very-high]
            JsonOnly:
              type: object
              properties:
                name: { type: string }
        """;

    private const string ProbeSource = """"
        using System.Xml;
        using ErrorBodies.Generated.Xml.Models;

        namespace ErrorBodies.RuntimeTest;

        public static class XmlProbe
        {
            public static List<string> Run()
            {
                var pet = new Pet(Name: "doggie", PhotoUrls: ["a.png"], Id: 10, Category: new Category(Id: 1, Name: "Dogs"), Tags: [new Tag(Id: 2, Name: "t")], Status: PetStatus.Available);
                var sample = new Sample(
                    Flag: true,
                    Count: 42,
                    Ratio: 1.5,
                    Price: 9.99,
                    At: new DateTimeOffset(2026, 9, 30, 8, 15, 0, TimeSpan.FromHours(2)),
                    Day: new DateOnly(2026, 9, 30),
                    Key: Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"),
                    Link: new Uri("https://example.com/a?b=c&d=e"),
                    Blob: [1, 2, 3],
                    Labels: new Dictionary<string, string> { ["color"] = "red", ["size s"] = "m" },
                    Level: Level.VeryHigh);

                var rows = new List<string>
                {
                    PlainPersonXml.ToXml(new PlainPerson(Name: "Fred")),
                    RenamedPersonXml.ToXml(new RenamedPerson(Name: "Fred")),
                    PersonXml.ToXml(new Person(Id: 123, Name: "example")),
                    Zoo1Xml.ToXml(new Zoo1(Animals: ["cat", "dog"])),
                    Zoo2Xml.ToXml(new Zoo2(Animals: ["cat", "dog"])),
                    Zoo3Xml.ToXml(new Zoo3(Animals: ["cat", "dog"])),
                    Zoo4Xml.ToXml(new Zoo4(Animals: ["cat", "dog"])),
                    Zoo5Xml.ToXml(new Zoo5(Animals: ["cat", "dog"])),
                    Zoo6Xml.ToXml(new Zoo6(Animals: ["cat", "dog"])),
                    Zoo7Xml.ToXml(new Zoo7(Animals: ["cat", "dog"])),
                    PetXml.ToXml(pet),
                    DocXml.ToXml(new Doc(Title: "x")),
                    LegacyXml.ToXml(new Legacy(Code: "c")),
                    TaskXml.ToXml(new ErrorBodies.Generated.Xml.Models.Task(Title: "t")),
                    SampleXml.ToXml(sample),
                    PlainPersonXml.ToXml(new PlainPerson()),
                };

                // Every document reads back into the same model.
                rows.Add("round trip: " + string.Join(" ", rows.Select(RoundTrip)));

                // Another producer's document: prefixes, indentation, an unknown element, an empty wrapped list.
                rows.Add(PetXml.ToXml(PetXml.FromXml("""
                    <?xml version="1.0" encoding="utf-8"?>
                    <p:pet xmlns:p="urn:other">
                      <p:unknown>ignored</p:unknown>
                      <p:name>rex</p:name>
                      <p:photoUrls />
                      <p:status>SOLD</p:status>
                    </p:pet>
                    """)));
                rows.Add(SampleXml.ToXml(SampleXml.FromXml("<Sample><flag>1</flag><count> 7 </count><day>2026-09-30</day><labels><size_x0020_s>l</size_x0020_s></labels></Sample>")));
                rows.Add(DocXml.ToXml(DocXml.FromXml("<Doc><title>no namespace</title></Doc>")));

                rows.Add(Failure(() => PetXml.FromXml("<pet><id>1</id></pet>")));
                rows.Add(Failure(() => SampleXml.FromXml("<Sample><flag>yes</flag><count>1</count></Sample>")));
                rows.Add(Failure(() => SampleXml.FromXml("<Sample><flag>true</flag><count>1</count><level>extreme</level></Sample>")));
                rows.Add(Failure(() => PetXml.FromXml("<!DOCTYPE pet [<!ENTITY x SYSTEM \"file:///c:/windows/win.ini\">]><pet><name>&x;</name><photoUrls /></pet>")));
                rows.Add(Failure(() => PetXml.FromXml("<pet><name>unclosed</pet>")));
                return rows;
            }

            private static string RoundTrip(string xml, int index)
                => index switch
                {
                    0 => Same(xml, PlainPersonXml.ToXml(PlainPersonXml.FromXml(xml))),
                    1 => Same(xml, RenamedPersonXml.ToXml(RenamedPersonXml.FromXml(xml))),
                    2 => Same(xml, PersonXml.ToXml(PersonXml.FromXml(xml))),
                    3 => Same(xml, Zoo1Xml.ToXml(Zoo1Xml.FromXml(xml))),
                    4 => Same(xml, Zoo2Xml.ToXml(Zoo2Xml.FromXml(xml))),
                    5 => Same(xml, Zoo3Xml.ToXml(Zoo3Xml.FromXml(xml))),
                    6 => Same(xml, Zoo4Xml.ToXml(Zoo4Xml.FromXml(xml))),
                    7 => Same(xml, Zoo5Xml.ToXml(Zoo5Xml.FromXml(xml))),
                    8 => Same(xml, Zoo6Xml.ToXml(Zoo6Xml.FromXml(xml))),
                    9 => Same(xml, Zoo7Xml.ToXml(Zoo7Xml.FromXml(xml))),
                    10 => Same(xml, PetXml.ToXml(PetXml.FromXml(xml))),
                    11 => Same(xml, DocXml.ToXml(DocXml.FromXml(xml))),
                    12 => Same(xml, LegacyXml.ToXml(LegacyXml.FromXml(xml))),
                    13 => Same(xml, TaskXml.ToXml(TaskXml.FromXml(xml))),
                    14 => Same(xml, SampleXml.ToXml(SampleXml.FromXml(xml))),
                    15 => Same(xml, PlainPersonXml.ToXml(PlainPersonXml.FromXml(xml))),
                    _ => "?",
                };

            private static string Same(string expected, string actual)
                => expected == actual ? "ok" : $"DIFF[{actual}]";

            private static string Failure(Func<object> read)
            {
                try
                {
                    read();
                    return "no exception";
                }
                catch (Exception ex)
                {
                    return $"{ex.GetType().Name}: {ex.Message.Split('\n')[0].Trim()}";
                }
            }
        }
        """";

    private static readonly string[] Expected =
    [
        "<PlainPerson><name>Fred</name></PlainPerson>",
        "<RenamedPerson><surname>Fred</surname></RenamedPerson>",
        "<Person id=\"123\"><sample:name xmlns:sample=\"http://example.com/schema/sample\">example</sample:name></Person>",
        "<Zoo1><animals>cat</animals><animals>dog</animals></Zoo1>",
        "<Zoo2><animal>cat</animal><animal>dog</animal></Zoo2>",
        "<Zoo3><animal>cat</animal><animal>dog</animal></Zoo3>",
        "<Zoo4><animals><animals>cat</animals><animals>dog</animals></animals></Zoo4>",
        "<Zoo5><animals><animal>cat</animal><animal>dog</animal></animals></Zoo5>",
        "<Zoo6><aliens><animal>cat</animal><animal>dog</animal></aliens></Zoo6>",
        "<Zoo7><aliens><aliens>cat</aliens><aliens>dog</aliens></aliens></Zoo7>",
        "<pet><id>10</id><name>doggie</name><category><id>1</id><name>Dogs</name></category><photoUrls><photoUrl>a.png</photoUrl></photoUrls><tags><tag><id>2</id><name>t</name></tag></tags><status>available</status></pet>",
        "<Doc xmlns=\"http://example.com/doc\"><title>x</title></Doc>",
        "<Legacy><code>c</code></Legacy>",
        "<Task><title>t</title></Task>",
        "<Sample><flag>true</flag><count>42</count><ratio>1.5</ratio><price>9.99</price><at>2026-09-30T08:15:00+02:00</at><day>2026-09-30</day><key>0f8fad5b-d9cb-469f-a165-70867728950e</key><link>https://example.com/a?b=c&amp;d=e</link><blob>AQID</blob><labels><color>red</color><size_x0020_s>m</size_x0020_s></labels><level>very-high</level></Sample>",
        "<PlainPerson />",
        "round trip: ok ok ok ok ok ok ok ok ok ok ok ok ok ok ok ok",
        "<pet><name>rex</name><photoUrls /><status>sold</status></pet>",
        "<Sample><flag>true</flag><count>7</count><day>2026-09-30</day><labels><size_x0020_s>l</size_x0020_s></labels></Sample>",
        "<Doc xmlns=\"http://example.com/doc\"><title>no namespace</title></Doc>",
        "FormatException: The XML element 'name' of 'pet' is required but missing.",
        "FormatException: The XML element 'flag' of 'Sample' has an invalid value: 'yes'.",
        "FormatException: The XML element 'level' of 'Sample' has an invalid value: 'extreme'.",
        "XmlException: For security reasons DTD is prohibited in this XML document. To enable DTD processing set the DtdProcessing property on XmlReaderSettings to Parse and pass the settings into XmlReader.Create method.",
        "XmlException: The 'name' start tag on line 1 position 7 does not match the end tag of 'pet'. Line 1, position 22.",
    ];

    [Theory]
    [InlineData("server")]
    [InlineData("EndpointPerOperation")]
    [InlineData("TypedClient")]
    public void Models_MapToTheXmlTheSpecificationDescribes(string side)
    {
        var sources = Generate(side);
        sources.Add(("XmlProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: side == "server");
        var rows = (List<string>)assembly.GetType("ErrorBodies.RuntimeTest.XmlProbe", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [])!;

        Assert.Equal(Expected, rows, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("server")]
    [InlineData("EndpointPerOperation")]
    [InlineData("TypedClient")]
    public void OnlyModelsReachableFromAnXmlBody_GetAMapping(string side)
    {
        var hints = Generate(side).Select(s => Path.GetFileName(s.HintName)).ToList();

        Assert.Contains(hints, h => h.EndsWith(".PetXml.g.cs", StringComparison.Ordinal));
        Assert.Contains(hints, h => h.EndsWith(".CategoryXml.g.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(hints, h => h.EndsWith(".JsonOnlyXml.g.cs", StringComparison.Ordinal));
        Assert.Single(hints, h => h.EndsWith(".XmlModelSupport.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void ASpecificationWithoutXml_GetsNoMapping()
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, BodyUploadGenerationTests.Yaml);

        Assert.DoesNotContain(sources, s => Path.GetFileName(s.HintName).Contains("Xml", StringComparison.Ordinal));
    }

    private static List<(string HintName, string Source)> Generate(string side)
        => side switch
        {
            "server" => ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml),
            "EndpointPerOperation" => ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml),
            _ => ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml),
        };
}