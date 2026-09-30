// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for XML bodies in the generated TypeScript clients (Fetch and Axios): an XML-only response is read
/// into the model by the generated <c>helpers/xml.ts</c> mapping (attributes, wrapped arrays, namespaces, escaping),
/// a JSON+XML response asks for JSON and reads whatever comes back, an XML request body is sent as the model's XML,
/// and an <c>application/problem+xml</c> error reaches the error arms like a problem+json one. The parser is
/// injectable (<c>setXmlParser</c>): browsers have <c>DOMParser</c>, Node takes <c>@xmldom/xmldom</c>. The generated
/// code also passes a strict <c>tsc</c>. Self-skips without a Node toolchain, but fails under CI.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptXmlGateTests
{
    [Fact]
    public void GeneratedClients_XmlBodies_AreReadAndWrittenByTheModelMapping()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(skipReason + " (running under CI where the XML gate is required)");
            }

            Assert.Skip(skipReason);
        }

        Assert.True(OpenApiDocumentHelper.TryParseYaml(SpecYaml, "XmlBodies.yaml", out var doc));

        var workDir = Path.Combine(Path.GetTempPath(), "atc-xml-gate", Guid.NewGuid().ToString("N"));
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
                });
            }

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            var install = NodeToolchain.RunNpm("install --no-audit --no-fund --loglevel=error", workDir, TimeSpan.FromMinutes(5));
            Assert.True(install.ExitCode == 0, $"npm install failed (exit {install.ExitCode}):\n{install.Output}");

            var run = NodeToolchain.RunNpx("tsx harness.mts", workDir, TimeSpan.FromMinutes(3));
            Assert.True(run.ExitCode == 0, $"XML gate failed (exit {run.ExitCode}):\n{run.Output}");

            var typeCheck = NodeToolchain.RunNpx("tsc -p .", workDir, TimeSpan.FromMinutes(3));
            Assert.True(typeCheck.ExitCode == 0, $"Strict tsc failed (exit {typeCheck.ExitCode}):\n{typeCheck.Output}");
        }
        finally
        {
            try
            {
                Directory.Delete(workDir, recursive: true);
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
          title: XmlBodies
          version: 1.0.0
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
                        items:
                          $ref: '#/components/schemas/Pet'
          /pets/{id}:
            get:
              operationId: getPet
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Pet'
                '404':
                  description: Not found
          /notes:
            post:
              operationId: addNote
              requestBody:
                required: true
                content:
                  application/xml:
                    schema:
                      $ref: '#/components/schemas/Note'
              responses:
                '201':
                  description: Created
                  content:
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Note'
                '400':
                  description: Invalid
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
                  description: OK
                  content:
                    application/vnd.acme.report+xml:
                      schema:
                        $ref: '#/components/schemas/Report'
        components:
          schemas:
            Pet:
              type: object
              required: [id, name]
              properties:
                id:
                  type: integer
                  format: int64
                  xml:
                    attribute: true
                name:
                  type: string
                tags:
                  type: array
                  xml:
                    wrapped: true
                  items:
                    type: string
                    xml:
                      name: tag
              xml:
                name: pet
            Note:
              type: object
              required: [text]
              properties:
                text:
                  type: string
              xml:
                name: note
            Report:
              type: object
              required: [title]
              properties:
                title:
                  type: string
                pages:
                  type: integer
                available:
                  type: boolean
              xml:
                name: report
                namespace: 'urn:acme:report'
        """;

    private const string PackageJson = """
        {
          "name": "xml-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "dependencies": {
            "@xmldom/xmldom": "^0.9.0",
            "axios": "^1.7.0"
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
            "esModuleInterop": true,
            "skipLibCheck": true,
            "noEmit": true,
            "types": ["node"]
          },
          "include": ["fetch/**/*.ts", "axios/**/*.ts"]
        }
        """;

    private const string Harness = """
        import axios from 'axios';
        import { DOMParser } from '@xmldom/xmldom';
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { PetsClient as FetchPets } from './fetch/client/PetsClient';
        import { NotesClient as FetchNotes } from './fetch/client/NotesClient';
        import { ReportsClient as FetchReports } from './fetch/client/ReportsClient';
        import { setXmlParser as setFetchParser } from './fetch/helpers/xml';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { PetsClient as AxiosPets } from './axios/client/PetsClient';
        import { NotesClient as AxiosNotes } from './axios/client/NotesClient';
        import { ReportsClient as AxiosReports } from './axios/client/ReportsClient';
        import { setXmlParser as setAxiosParser } from './axios/helpers/xml';

        // Node has no DOMParser: the generated helper takes any DOMParser-compatible parser.
        setFetchParser(() => new DOMParser() as any);
        setAxiosParser(() => new DOMParser() as any);

        const failures: string[] = [];
        const check = (name: string, cond: boolean, detail: string) => { if (!cond) failures.push(name + ': ' + detail); };
        const same = (a: unknown, b: unknown) => JSON.stringify(a) === JSON.stringify(b);

        type Answer = { status: number; contentType: string; body: string };
        let sent: { accept: string; contentType: string; body: string } = { accept: '', contentType: '', body: '' };

        // One answer table for both clients: route -> what the server writes.
        function answer(method: string, url: string, body: string): Answer {
          if (url.endsWith('/pets')) {
            return { status: 200, contentType: 'application/xml; charset=utf-8', body: '<ArrayOfPet><pet id="1"><name>Rex</name><tags><tag>good</tag><tag>boy</tag></tags></pet><pet id="2"><name>Fido</name></pet></ArrayOfPet>' };
          }
          if (url.endsWith('/pets/1')) {
            return { status: 200, contentType: 'application/xml; charset=utf-8', body: '<pet id="1"><name>Rex &amp; co</name></pet>' };
          }
          if (url.endsWith('/pets/2')) {
            return { status: 200, contentType: 'application/json; charset=utf-8', body: '{"id":2,"name":"Fido"}' };
          }
          if (url.endsWith('/pets/404')) {
            return { status: 404, contentType: 'application/problem+xml; charset=utf-8', body: '<problem xmlns="urn:ietf:rfc:7807"><title>Not Found</title><status>404</status><detail>No pet 404</detail></problem>' };
          }
          if (url.endsWith('/notes') && body.includes('bad')) {
            return { status: 400, contentType: 'application/problem+xml; charset=utf-8', body: '<problem xmlns="urn:ietf:rfc:7807"><title>One or more validation errors occurred.</title><status>400</status><errors><text><i>Too long</i></text></errors></problem>' };
          }
          if (url.endsWith('/notes')) {
            return { status: 201, contentType: 'application/xml; charset=utf-8', body: '<note><text>stored</text></note>' };
          }
          return { status: 200, contentType: 'application/vnd.acme.report+xml; charset=utf-8', body: '<r:report xmlns:r="urn:acme:report"><r:title>Q3</r:title><r:pages>12</r:pages><r:available>true</r:available></r:report>' };
        }

        (globalThis as any).fetch = async (input: any, init: any) => {
          const headers = new Headers(init?.headers);
          sent = { accept: headers.get('Accept') ?? '', contentType: headers.get('Content-Type') ?? '', body: typeof init?.body === 'string' ? init.body : '' };
          const a = answer(init?.method ?? 'GET', String(input), sent.body);
          return new Response(a.body, { status: a.status, headers: { 'content-type': a.contentType } });
        };

        axios.defaults.adapter = (async (config: any) => {
          const headers = config.headers ?? {};
          const get = (name: string) => String((typeof headers.get === 'function' ? headers.get(name) : headers[name]) ?? '');
          const body = typeof config.data === 'string' ? config.data : '';
          sent = { accept: get('Accept'), contentType: get('Content-Type'), body };
          const a = answer(config.method, String(config.url), body);
          const data = config.transformResponse ? [].concat(config.transformResponse).reduce((d: any, fn: any) => fn.call(config, d, { 'content-type': a.contentType }), a.body) : a.body;
          return { data, status: a.status, statusText: String(a.status), headers: { 'content-type': a.contentType }, config, request: {} };
        }) as any;

        async function run(label: string, pets: any, notes: any, reports: any): Promise<void> {
          const list = await pets.listPets();
          check(label + ' list', list.status === 'ok' && same(list.data, [{ id: 1, name: 'Rex', tags: ['good', 'boy'] }, { id: 2, name: 'Fido' }]), JSON.stringify(list.data ?? list.error?.message));
          check(label + ' list-accept', sent.accept === 'application/xml, application/problem+json', 'accept ' + sent.accept);

          const xml = await pets.getPet(1);
          check(label + ' alternative-xml', xml.status === 'ok' && same(xml.data, { id: 1, name: 'Rex & co' }), JSON.stringify(xml.data ?? xml.error?.message));
          check(label + ' alternative-accept', sent.accept === 'application/json, application/problem+json', 'accept ' + sent.accept);

          const json = await pets.getPet(2);
          check(label + ' alternative-json', json.status === 'ok' && same(json.data, { id: 2, name: 'Fido' }), JSON.stringify(json.data));

          const missing = await pets.getPet(404);
          check(label + ' problem-xml', missing.status === 'notFound' && missing.error.message === 'No pet 404', missing.status + ' ' + missing.error?.message);

          const created = await notes.addNote({ text: 'Hi & <bye>' });
          check(label + ' xml-body', sent.body === '<note><text>Hi &amp; &lt;bye&gt;</text></note>', 'body ' + sent.body);
          check(label + ' xml-body-type', sent.contentType.startsWith('application/xml'), 'content-type ' + sent.contentType);
          check(label + ' created', created.status === 'created' && same(created.data, { text: 'stored' }), created.status + ' ' + JSON.stringify(created.data));

          const invalid = await notes.addNote({ text: 'bad' });
          check(label + ' problem-xml-errors', invalid.status === 'badRequest' && same(invalid.error.errors, { text: ['Too long'] }), invalid.status + ' ' + JSON.stringify(invalid.error?.errors));

          const report = await reports.getReport(7);
          check(label + ' namespaced', report.status === 'ok' && same(report.data, { title: 'Q3', pages: 12, available: true }), JSON.stringify(report.data ?? report.error?.message));
        }

        const fetchApi = new FetchApiClient('http://stub');
        await run('fetch', new FetchPets(fetchApi), new FetchNotes(fetchApi), new FetchReports(fetchApi));
        const axiosApi = new AxiosApiClient('http://stub');
        await run('axios', new AxiosPets(axiosApi), new AxiosNotes(axiosApi), new AxiosReports(axiosApi));

        if (failures.length > 0) {
          console.error(failures.join('\n'));
          process.exit(1);
        }

        console.log('xml gate: all checks passed');
        """;
}