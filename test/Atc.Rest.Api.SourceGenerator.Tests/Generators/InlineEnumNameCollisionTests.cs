namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// An inline enum is named <c>{Model}{Property}</c>; when a component schema already has that name (<c>Thing.type</c>
/// next to a component <c>ThingType</c>) the inline enum is <c>{Model}{Property}Enum</c>. Both were generated under the
/// one name: in the same namespace that did not compile (CS0101), and across a segment's and the shared models
/// namespace a reference to the component was ambiguous (CS0104).
/// </summary>
public sealed class InlineEnumNameCollisionTests
{
    // In one segment: the component and the inline enum land in the same models namespace
    private const string SameSegmentYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              parameters:
                - name: type
                  in: query
                  schema: { $ref: '#/components/schemas/ThingType' }
              responses:
                '204': { description: Listed }
            post:
              operationId: createThing
              requestBody:
                required: true
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Thing' }
              responses:
                '204': { description: Created }
        components:
          schemas:
            ThingType:
              type: string
              enum: [small, large]
            Thing:
              type: object
              properties:
                type:
                  type: string
                  enum: [red, green, blue]
        """;

    // Across segments: the component is shared (two segments use it), the inline enum is the segment's
    private const string CrossSegmentYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              parameters:
                - name: type
                  in: query
                  schema: { $ref: '#/components/schemas/ThingType' }
              responses:
                '204': { description: Listed }
            post:
              operationId: createThing
              requestBody:
                required: true
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Thing' }
              responses:
                '204': { description: Created }
          /others:
            get:
              operationId: listOthers
              parameters:
                - name: type
                  in: query
                  schema: { $ref: '#/components/schemas/ThingType' }
              responses:
                '204': { description: Listed }
        components:
          schemas:
            ThingType:
              type: string
              enum: [small, large]
            Thing:
              type: object
              properties:
                type:
                  type: string
                  enum: [red, green, blue]
        """;

    public static TheoryData<string, string> Cases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var yaml in new[] { nameof(SameSegmentYaml), nameof(CrossSegmentYaml) })
            {
                foreach (var side in new[] { "Server", "TypedClient", "EndpointPerOperation" })
                {
                    data.Add(yaml, side);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AnInlineEnumNamedLikeAComponent_IsRenamed_AndTheCodeCompiles(
        string yamlName,
        string side)
    {
        var yaml = yamlName == nameof(SameSegmentYaml) ? SameSegmentYaml : CrossSegmentYaml;
        var sources = side switch
        {
            "Server" => ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, yaml),
            "TypedClient" => ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, yaml),
            _ => ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, yaml),
        };

        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: side == "Server");

        var types = assembly.GetTypes();
        Assert.Contains(types, t => t.Name == "ThingType");
        Assert.Contains(types, t => t.Name == "ThingTypeEnum");
    }
}