namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptXmlHelperExtractorTests
{
    private const string XmlYaml = """
        openapi: 3.0.3
        info: { title: XmlBodies, version: 1.0.0 }
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                '200':
                  description: OK
                  content:
                    application/xml:
                      schema:
                        type: array
                        items: { $ref: '#/components/schemas/Pet' }
          /notes:
            post:
              operationId: addNote
              requestBody:
                required: true
                content:
                  application/xml:
                    schema: { $ref: '#/components/schemas/Note' }
              responses:
                '201':
                  description: Created
                  content:
                    application/xml:
                      schema: { $ref: '#/components/schemas/Note' }
        components:
          schemas:
            Pet:
              type: object
              required: [id, name]
              properties:
                id:
                  type: integer
                  format: int64
                  xml: { attribute: true }
                name: { type: string }
              xml: { name: pet }
            Note:
              type: object
              required: [text]
              properties:
                text: { type: string }
              xml: { name: note }
            Unrelated:
              type: object
              properties:
                value: { type: string }
        """;

    private const string JsonOnlyYaml = """
        openapi: 3.0.3
        info: { title: Json, version: 1.0.0 }
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Pet' }
        components:
          schemas:
            Pet:
              type: object
              properties:
                name: { type: string }
        """;

    [Fact]
    public void FunctionNames_PrefixesTheSanitizedSchemaName()
    {
        var (read, write, readList, writeList) = TypeScriptXmlHelperExtractor.FunctionNames("Pet-Info");

        Assert.Equal("readPet_Info", read);
        Assert.Equal("writePet_Info", write);
        Assert.Equal("readPet_InfoList", readList);
        Assert.Equal("writePet_InfoList", writeList);
    }

    [Fact]
    public void Generate_NoXmlBody_ReturnsNull()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(JsonOnlyYaml);

        Assert.Null(TypeScriptXmlHelperExtractor.Generate(doc, headerContent: null, TypeScriptNamingStrategy.CamelCase, convertDates: false));
    }

    [Fact]
    public void Generate_XmlBodies_EmitsTheMappingsOfTheReachableModels_AndReExportsTheRuntime()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(XmlYaml);

        var content = TypeScriptXmlHelperExtractor.Generate(doc, "// header\n", TypeScriptNamingStrategy.CamelCase, convertDates: false);
        var runtime = TypeScriptXmlHelperExtractor.GenerateRuntime("// header\n");

        Assert.NotNull(content);
        Assert.StartsWith("// header\n", content, StringComparison.Ordinal);
        Assert.Contains("import type { Note, Pet } from '../models';", content, StringComparison.Ordinal);
        Assert.Matches(@"import \{ [^}]*\bxmlChild\b[^}]* \} from './xmlRuntime';", content);
        Assert.Contains("export * from './xmlRuntime';", content, StringComparison.Ordinal);
        Assert.DoesNotContain("export function setXmlParser(", content, StringComparison.Ordinal);
        Assert.StartsWith("// header\n", runtime, StringComparison.Ordinal);
        Assert.Contains("export function setXmlParser(", runtime, StringComparison.Ordinal);
        Assert.Contains("export function readProblemXml(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("readPet", runtime, StringComparison.Ordinal);
        Assert.Contains("export function readPet(", content, StringComparison.Ordinal);
        Assert.Contains("export function writePet(", content, StringComparison.Ordinal);
        Assert.Contains("export function readPetList(", content, StringComparison.Ordinal);
        Assert.Contains("export function readNote(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Unrelated", content, StringComparison.Ordinal);
        Assert.EndsWith(Environment.NewLine, content, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_NullDocument_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => TypeScriptXmlHelperExtractor.Generate(null!, headerContent: null, TypeScriptNamingStrategy.CamelCase, convertDates: false));

    [Fact]
    public void FetchApiClient_WithXml_ImportsTheHelperAndAddsTheXmlOption()
    {
        var client = TypeScriptFetchApiClientExtractor.Generate(headerContent: null, hasXml: true);

        Assert.Contains("import { isXmlMediaType, parseXml, readProblemXml, withResponse, xmlSuccessArm, type XmlOptions } from '../helpers/xmlRuntime';", client, StringComparison.Ordinal);
        Assert.Contains("  xml?: XmlOptions;", client, StringComparison.Ordinal);
        Assert.Contains("headers.set('Accept', options.xml.accept);", client, StringComparison.Ordinal);
        Assert.Contains("fetchBody = options.xml.write(options.body as never);", client, StringComparison.Ordinal);
        Assert.Contains("options.xml.read(parseXml(", client, StringComparison.Ordinal);
        Assert.Contains("return fitToArms(withResponse(serverResponse, await this.handleResponse<T>(", client, StringComparison.Ordinal);
        Assert.Contains("readProblemXml(parseXml(", client, StringComparison.Ordinal);
    }

    [Fact]
    public void AxiosApiClient_WithXml_WritesXmlBodiesAndSetsAccept()
    {
        var client = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null, hasXml: true);

        Assert.Contains("import { isXmlMediaType, parseXml, readProblemXml, xmlSuccessArm, type XmlOptions } from '../helpers/xmlRuntime';", client, StringComparison.Ordinal);
        Assert.Contains("  xml?: XmlOptions;", client, StringComparison.Ordinal);
        Assert.Contains("data = options.xml.write(options.body as never);", client, StringComparison.Ordinal);
        Assert.Contains("headers['Accept'] = options.xml.accept;", client, StringComparison.Ordinal);
        Assert.Contains("return fitToArms(await this.handleResponse<T>(response, options?.xml), options?.arms);", client, StringComparison.Ordinal);
        Assert.Contains("xml.read(parseXml(response.data))", client, StringComparison.Ordinal);
        Assert.Contains("errorContentType.includes('application/problem+xml')", client, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TypeScriptHttpClient.Fetch)]
    [InlineData(TypeScriptHttpClient.Axios)]
    public void ApiClient_WithoutXml_HasNoXmlSupport(
        TypeScriptHttpClient httpClient)
    {
        var client = httpClient == TypeScriptHttpClient.Axios
            ? TypeScriptAxiosApiClientExtractor.Generate(headerContent: null)
            : TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        Assert.DoesNotContain("helpers/xml", client, StringComparison.Ordinal);
        Assert.DoesNotContain("XmlOptions", client, StringComparison.Ordinal);
        Assert.DoesNotContain("problem+xml", client, StringComparison.Ordinal);
    }
}