// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for a response that is a model or a file (a 200 that declares <c>application/json</c> or an XML model,
/// and <c>application/pdf</c>) in the generated TypeScript clients (Fetch and Axios): the operation's data is
/// <c>Model | Blob</c>, told apart by the answer's Content-Type - the model is read as the model (JSON, or XML by the
/// generated mapping), the file arrives as a Blob with its bytes intact - and the request asks for every declared media
/// type. The Axios stub answers a <c>responseType: 'blob'</c> request with a Blob, as a browser does. The full options
/// (Zod runtime validation, date conversion, branded ids) and an API property name the models rename (<c>page_count</c>)
/// leave the file a Blob and still read the model. The generated code also passes a strict <c>tsc</c>. Self-skips without
/// a Node toolchain, but fails under CI.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptModelOrFileGateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedClients_ModelOrFile_ReadTheModelOrTheFileByContentType(
        bool fullOptions)
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(skipReason + " (running under CI where the model-or-file gate is required)");
            }

            Assert.Skip(skipReason);
        }

        Assert.True(OpenApiDocumentHelper.TryParseYaml(SpecYaml, "ModelOrFile.yaml", out var doc));

        var workDir = Path.Combine(Path.GetTempPath(), "atc-model-or-file-gate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            foreach (var (folder, httpClient) in new[] { ("fetch", TypeScriptHttpClient.Fetch), ("axios", TypeScriptHttpClient.Axios) })
            {
                var output = Path.Combine(workDir, folder);
                Directory.CreateDirectory(output);
                TypeScriptClientGenerationService.Generate(doc!, output, new TypeScriptClientConfig
                {
                    HttpClient = httpClient,
                    HooksStyle = TypeScriptHooksStyle.None,
                    GenerateFileHeaders = true,
                    Scaffold = false,
                    GenerateZodSchemas = fullOptions,
                    ZodRuntimeValidate = fullOptions,
                    ConvertDates = fullOptions,
                    BrandedIds = fullOptions,
                    MutableModels = fullOptions,
                });
            }

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness.Replace("const fullOptions = false;", $"const fullOptions = {(fullOptions ? "true" : "false")};", StringComparison.Ordinal));

            NodeWorkspace.Link(workDir);

            var run = NodeToolchain.RunNpx("tsx harness.mts", workDir, TimeSpan.FromMinutes(3));
            Assert.True(run.ExitCode == 0, $"Model-or-file gate failed (exit {run.ExitCode}):\n{run.Output}");

            var typeCheck = NodeToolchain.RunNpx("tsc -p .", workDir, TimeSpan.FromMinutes(3));
            Assert.True(typeCheck.ExitCode == 0, $"Strict tsc failed (exit {typeCheck.ExitCode}):\n{typeCheck.Output}");
        }
        finally
        {
            try
            {
                NodeWorkspace.Delete(workDir);
            }
            catch (IOException)
            {
                // Best effort: npm may still hold a file handle on Windows.
            }
            catch (UnauthorizedAccessException)
            {
                // Best effort.
            }
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.3
        info:
          title: ModelOrFile
          version: 1.0.0
        paths:
          /reports/{id}:
            get:
              operationId: getReport
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: The report, or its PDF
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Report'
                    application/pdf:
                      schema:
                        type: string
                        format: binary
                '404':
                  description: Not found
          /notes/{id}:
            get:
              operationId: getNote
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: The note as XML, or its PDF
                  content:
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Note'
                    application/pdf:
                      schema:
                        type: string
                        format: binary
          /sheets/{id}:
            get:
              operationId: getSheet
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: The report, or its workbook (a media type that has "xml" in it, and is no XML)
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Report'
                    application/vnd.openxmlformats-officedocument.spreadsheetml.sheet:
                      schema:
                        type: string
                        format: binary
        components:
          schemas:
            Report:
              type: object
              required: [title]
              properties:
                title:
                  type: string
                page_count:
                  type: integer
                createdAt:
                  type: string
                  format: date-time
            Note:
              type: object
              required: [text]
              properties:
                text:
                  type: string
              xml:
                name: note
        """;

    private const string PackageJson = """
        {
          "name": "model-or-file-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "dependencies": {
            "@xmldom/xmldom": "^0.9.0",
            "axios": "^1.7.0",
            "zod": "^3.23.0"
          },
          "devDependencies": {
            "@types/node": "^22.0.0",
            "tsx": "^4.19.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    private const string TsConfig = """
        {
          "compilerOptions": {
            "target": "ES2022",
            "lib": ["ES2022", "DOM"],
            "module": "ESNext",
            "moduleResolution": "bundler",
            "strict": true,
            "noUncheckedIndexedAccess": true,
            "esModuleInterop": true,
            "skipLibCheck": true,
            "noEmit": true,
            "types": ["node"]
          },
          "include": ["fetch/**/*.ts", "axios/**/*.ts", "harness.mts"]
        }
        """;

    private const string Harness = """
        import axios from 'axios';
        import { DOMParser } from '@xmldom/xmldom';
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { ReportsClient as FetchReports } from './fetch/client/ReportsClient';
        import { NotesClient as FetchNotes } from './fetch/client/NotesClient';
        import { SheetsClient as FetchSheets } from './fetch/client/SheetsClient';
        import { setXmlParser as setFetchParser } from './fetch/helpers/xml';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { ReportsClient as AxiosReports } from './axios/client/ReportsClient';
        import { NotesClient as AxiosNotes } from './axios/client/NotesClient';
        import { SheetsClient as AxiosSheets } from './axios/client/SheetsClient';
        import { setXmlParser as setAxiosParser } from './axios/helpers/xml';
        import type { Report } from './fetch/models';

        const fullOptions = false;

        setFetchParser(() => new DOMParser() as never);
        setAxiosParser(() => new DOMParser() as never);

        const failures: string[] = [];
        const check = (name: string, cond: boolean, detail: string) => { if (!cond) failures.push(name + ': ' + detail); };
        const same = (a: unknown, b: unknown) => JSON.stringify(a) === JSON.stringify(b);

        // %PDF-1.7 and a byte that is not valid UTF-8: a body read as text would not survive
        const pdf = new Uint8Array([0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x37, 0xff, 0x00, 0xc3]);
        let accept = '';

        type Answer = { status: number; contentType: string; body: Uint8Array | string };
        function answer(url: string): Answer {
          if (url.endsWith('/reports/1')) return { status: 200, contentType: 'application/json; charset=utf-8', body: '{"title":"Q3","page_count":3,"createdAt":"2026-10-01T08:00:00Z"}' };
          if (url.endsWith('/sheets/2')) return { status: 200, contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', body: pdf };
          if (url.endsWith('/notes/1')) return { status: 200, contentType: 'application/xml; charset=utf-8', body: '<note><text>Hi &amp; bye</text></note>' };
          return { status: 200, contentType: 'application/pdf', body: pdf };
        }

        (globalThis as { fetch: unknown }).fetch = async (input: unknown, init?: RequestInit) => {
          accept = new Headers(init?.headers).get('Accept') ?? '';
          const a = answer(String(input));
          return new Response(a.body as BodyInit, { status: a.status, headers: { 'content-type': a.contentType } });
        };

        // An adapter as Axios' own: responseType 'blob' answers a Blob, 'arraybuffer' the bytes, else text; the response
        // transforms run on what came back
        axios.defaults.adapter = (async (config: { url?: string; responseType?: string; headers?: unknown; transformResponse?: unknown }) => {
          const headers = config.headers as { get?: (name: string) => unknown } & Record<string, unknown>;
          accept = String((typeof headers.get === 'function' ? headers.get('Accept') : headers['Accept']) ?? '');
          const a = answer(String(config.url));
          const bytes = typeof a.body === 'string' ? new TextEncoder().encode(a.body) : a.body;
          const raw: unknown = config.responseType === 'blob'
            ? new Blob([bytes as BlobPart], { type: a.contentType })
            : config.responseType === 'arraybuffer'
              ? bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength)
              : new TextDecoder().decode(bytes);
          const transforms = ([] as unknown[]).concat(config.transformResponse ?? []) as ((this: unknown, data: unknown, headers: unknown) => unknown)[];
          const data = transforms.reduce((d, fn) => fn.call(config, d, { 'content-type': a.contentType }), raw);
          return { data, status: a.status, statusText: String(a.status), headers: { 'content-type': a.contentType }, config, request: {} };
        }) as never;

        async function bytesOf(blob: Blob): Promise<number[]> {
          return Array.from(new Uint8Array(await blob.arrayBuffer()));
        }

        async function run(label: string, reports: { getReport(id: number): Promise<{ status: string; data?: Report | Blob }> }, notes: { getNote(id: number): Promise<{ status: string; data?: unknown }> }, sheets: { getSheet(id: number): Promise<{ status: string; data?: unknown }> }): Promise<void> {
          const model = await reports.getReport(1);
          check(label + ' model', model.status === 'ok' && !(model.data instanceof Blob) && (model.data as Report | undefined)?.title === 'Q3', JSON.stringify(model));
          check(label + ' model-accept', accept === 'application/json, application/pdf, application/problem+json', 'accept ' + accept);
          const report = model.data as Report | undefined;
          check(label + ' model-renamed', (report as { pageCount?: number } | undefined)?.pageCount === 3, JSON.stringify(report));
          check(label + ' model-dates', fullOptions ? (report?.createdAt as unknown) instanceof Date : typeof report?.createdAt === 'string', String(report?.createdAt));

          const file = await reports.getReport(2);
          check(label + ' file', file.status === 'ok' && file.data instanceof Blob, String(file.data));
          if (file.data instanceof Blob) {
            check(label + ' file-bytes', same(await bytesOf(file.data), Array.from(pdf)), JSON.stringify(await bytesOf(file.data)));
            check(label + ' file-type', file.data.type.startsWith('application/pdf'), 'type ' + file.data.type);
          }

          const xml = await notes.getNote(1);
          check(label + ' xml-model', xml.status === 'ok' && same(xml.data, { text: 'Hi & bye' }), JSON.stringify(xml));
          check(label + ' xml-accept', accept === 'application/xml, application/pdf, application/problem+json', 'accept ' + accept);

          const sheet = await sheets.getSheet(2);
          check(label + ' sheet-file', sheet.status === 'ok' && sheet.data instanceof Blob, String(sheet.data));
          if (sheet.data instanceof Blob) {
            check(label + ' sheet-bytes', same(await bytesOf(sheet.data), Array.from(pdf)), JSON.stringify(await bytesOf(sheet.data)));
          }

          const xmlFile = await notes.getNote(2);
          check(label + ' xml-file', xmlFile.status === 'ok' && xmlFile.data instanceof Blob, String(xmlFile.data));
          if (xmlFile.data instanceof Blob) {
            check(label + ' xml-file-bytes', same(await bytesOf(xmlFile.data), Array.from(pdf)), JSON.stringify(await bytesOf(xmlFile.data)));
          }
        }

        const fetchApi = new FetchApiClient('http://stub');
        await run('fetch', new FetchReports(fetchApi), new FetchNotes(fetchApi), new FetchSheets(fetchApi));
        const axiosApi = new AxiosApiClient('http://stub');
        await run('axios', new AxiosReports(axiosApi), new AxiosNotes(axiosApi), new AxiosSheets(axiosApi));

        if (failures.length > 0) {
          console.error(failures.join('\n'));
          process.exitCode = 1;
        } else {
          console.log('model-or-file gate: all checks passed');
        }
        """;
}