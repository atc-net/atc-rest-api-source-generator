namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Specifications of an operation whose request body is a raw file of the given media types.
/// </summary>
internal static class RawFileBodySpecs
{
    public static string Yaml(params string[] mediaTypes)
    {
        var content = string.Join(
            "\n",
            mediaTypes.Select(m => $"          '{m}':\n            schema:\n              type: string\n              format: binary"));
        return $$"""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths:
              /upload:
                post:
                  operationId: uploadFile
                  requestBody:
                    content:
            {{content}}
                  responses:
                    '200':
                      description: OK
            """;
    }
}