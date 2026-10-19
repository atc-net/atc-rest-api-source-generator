namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for the typed headers of the generated clients' results (Fetch and Axios): a Node http server answers
/// with the headers the responses declare, and each arm's <c>headers</c> must hold them as their types - an enum, an
/// array (sent as one header and as repeated lines), a number, a boolean, a date-time (a Date with convertDates), an
/// object in the simple style (exploded and not). A value that is not of its type is undefined (an open enum keeps
/// it), an absent header is undefined, and an error arm reads the headers of its status or of <c>default</c>. The
/// clients are compiled by tsc first, so the readers are checked against the arms' types. Self-skips without a Node
/// toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptResponseHeadersGateTests
{
    [Fact]
    public void GeneratedClients_ReadTheDeclaredResponseHeaders_AsTheirTypes()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "Headers.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-response-headers-gate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            GenerateClient(doc, Path.Combine(workDir, "fetch"), TypeScriptHttpClient.Fetch, extensibleEnums: true, convertDates: false, TypeScriptEnumStyle.Union);
            GenerateClient(doc, Path.Combine(workDir, "axios"), TypeScriptHttpClient.Axios, extensibleEnums: true, convertDates: false, TypeScriptEnumStyle.Union);
            GenerateClient(doc, Path.Combine(workDir, "fetch-closed-dates"), TypeScriptHttpClient.Fetch, extensibleEnums: false, convertDates: true, TypeScriptEnumStyle.Union);
            GenerateClient(doc, Path.Combine(workDir, "axios-enum-style"), TypeScriptHttpClient.Axios, extensibleEnums: true, convertDates: true, TypeScriptEnumStyle.Enum);

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            NodeWorkspace.Link(workDir);

            var compile = NodeToolchain.RunNpx("tsc -p .", workDir, TimeSpan.FromMinutes(3));
            Assert.True(
                compile.ExitCode == 0,
                $"The generated clients with typed response headers do not compile (exit {compile.ExitCode}):\n{compile.Output}");

            var run = NodeToolchain.RunNpx("tsx harness.mts", workDir, TimeSpan.FromMinutes(3));
            Assert.True(
                run.ExitCode == 0,
                $"Generated client response headers gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private const string SpecYaml = """
        openapi: 3.1.0
        info:
          title: Headers
          version: 1.0.0
        paths:
          /things/{id}/status:
            get:
              operationId: getStatus
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: The status.
                  headers:
                    X-Mode:
                      schema:
                        $ref: '#/components/schemas/Mode'
                    X-Modes:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Mode'
                    X-Priority:
                      schema:
                        $ref: '#/components/schemas/Priority'
                    X-Ratio:
                      schema:
                        type: number
                        format: float
                    X-Count:
                      schema:
                        type: integer
                    X-Flag:
                      schema:
                        type: boolean
                    X-Expires:
                      schema:
                        type: string
                        format: date-time
                    X-Level:
                      schema:
                        type: string
                        enum: [low, very-high]
                    X-Levels:
                      schema:
                        type: array
                        items:
                          type: string
                          enum: [low, very-high]
                    X-Window:
                      schema:
                        $ref: '#/components/schemas/Window'
                    X-Window-Exploded:
                      explode: true
                      schema:
                        $ref: '#/components/schemas/Window'
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Status'
                '404':
                  description: Not found.
                  headers:
                    X-Trace:
                      schema:
                        type: string
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/Problem'
                '503':
                  description: Unavailable.
                  headers:
                    X-Mode:
                      schema:
                        $ref: '#/components/schemas/Mode'
                    Retry-After:
                      schema:
                        type: integer
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/Problem'
                default:
                  description: Any other error.
                  headers:
                    X-Trace:
                      schema:
                        type: string
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/Problem'
        components:
          schemas:
            Mode:
              type: string
              enum: [fast, slow-and-steady]
            Priority:
              type: integer
              enum: [1, 2, 3]
            Window:
              type: object
              required: [start]
              properties:
                start:
                  type: integer
                end:
                  type: integer
                mode:
                  $ref: '#/components/schemas/Mode'
            Status:
              type: object
              required: [name]
              properties:
                name:
                  type: string
            Problem:
              type: object
              properties:
                title:
                  type: string
                status:
                  type: integer
        """;

    private const string PackageJson = """
        {
          "name": "response-headers-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "dependencies": {
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
            "target": "ES2020",
            "lib": ["ES2020", "DOM"],
            "module": "ESNext",
            "moduleResolution": "bundler",
            "strict": true,
            "noUncheckedIndexedAccess": true,
            "noUnusedLocals": true,
            "noUnusedParameters": true,
            "esModuleInterop": true,
            "skipLibCheck": true,
            "allowImportingTsExtensions": true,
            "types": ["node"],
            "noEmit": true
          },
          "include": ["fetch", "axios", "fetch-closed-dates", "axios-enum-style", "harness.mts"]
        }
        """;

    // Each variant: its client, whether its enums keep a value they do not list (open), and whether a date-time is a Date
    private const string Harness = """
        import { createServer } from 'node:http';
        import type { AddressInfo } from 'node:net';
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { ThingsClient as FetchThingsClient } from './fetch/client/ThingsClient';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { ThingsClient as AxiosThingsClient } from './axios/client/ThingsClient';
        import { ApiClient as ClosedApiClient } from './fetch-closed-dates/client/ApiClient';
        import { ThingsClient as ClosedThingsClient } from './fetch-closed-dates/client/ThingsClient';
        import { ApiClient as EnumStyleApiClient } from './axios-enum-style/client/ApiClient';
        import { ThingsClient as EnumStyleThingsClient } from './axios-enum-style/client/ThingsClient';

        const problem = JSON.stringify({ title: 'No', status: 0 });
        const answers: Record<string, { status: number; headers: Record<string, string | string[]>; body: string; type: string }> = {
          all: {
            status: 200,
            headers: {
              'X-Mode': 'slow-and-steady',
              'X-Modes': ['fast', 'slow-and-steady'],
              'X-Priority': '2',
              'X-Ratio': '0.5',
              'X-Count': '7',
              'X-Flag': 'TRUE',
              'X-Expires': '2026-10-19T08:30:00Z',
              'X-Level': 'very-high',
              'X-Levels': 'low, very-high',
              'X-Window': 'start,1,end,5,mode,fast',
              'X-Window-Exploded': 'start=2',
            },
            body: JSON.stringify({ name: 'a' }),
            type: 'application/json',
          },
          odd: {
            status: 200,
            headers: {
              'X-Mode': 'turbo',
              'X-Priority': '9',
              'X-Count': '1.5',
              'X-Flag': 'yes',
              'X-Expires': 'soon',
              'X-Levels': 'low,unknown',
              'X-Window': 'end,5',
            },
            body: JSON.stringify({ name: 'b' }),
            type: 'application/json',
          },
          bare: { status: 200, headers: {}, body: JSON.stringify({ name: 'c' }), type: 'application/json' },
          busy: { status: 503, headers: { 'X-Mode': 'fast', 'Retry-After': '30', 'X-Trace': 't-1' }, body: problem, type: 'application/problem+json' },
          gone: { status: 404, headers: { 'X-Trace': 't-2' }, body: problem, type: 'application/problem+json' },
          teapot: { status: 418, headers: { 'X-Trace': 't-3' }, body: problem, type: 'application/problem+json' },
        };

        const server = createServer((req, res) => {
          const id = (req.url ?? '').split('/')[2] ?? '';
          const answer = answers[id] ?? { status: 404, headers: {}, body: problem, type: 'application/problem+json' };
          res.writeHead(answer.status, { 'Content-Type': answer.type, ...answer.headers });
          res.end(answer.body);
        });

        const failures: string[] = [];
        function same(label: string, actual: unknown, expected: unknown): void {
          const a = JSON.stringify(actual);
          const e = JSON.stringify(expected);
          if (a !== e) failures.push(label + ': expected ' + e + ', got ' + a);
        }

        async function main(): Promise<void> {
          await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
          const baseUrl = 'http://127.0.0.1:' + (server.address() as AddressInfo).port;

          const variants = [
            { label: 'fetch', things: new FetchThingsClient(new FetchApiClient(baseUrl)), open: true, inlineOpen: true, dates: false },
            { label: 'axios', things: new AxiosThingsClient(new AxiosApiClient(baseUrl)), open: true, inlineOpen: true, dates: false },
            { label: 'fetch-closed-dates', things: new ClosedThingsClient(new ClosedApiClient(baseUrl)), open: false, inlineOpen: false, dates: true },
            { label: 'axios-enum-style', things: new EnumStyleThingsClient(new EnumStyleApiClient(baseUrl)), open: false, inlineOpen: true, dates: true },
          ];

          for (const { label, things, open, inlineOpen, dates } of variants) {
            const all = await things.getStatus('all');
            if (all.status !== 'ok') {
              failures.push(label + ' all: expected ok, got ' + all.status);
            } else {
              same(label + ' all', all.headers, {
                'X-Mode': 'slow-and-steady',
                'X-Modes': ['fast', 'slow-and-steady'],
                'X-Priority': 2,
                'X-Ratio': 0.5,
                'X-Count': 7,
                'X-Flag': true,
                'X-Expires': dates ? '2026-10-19T08:30:00.000Z' : '2026-10-19T08:30:00Z',
                'X-Level': 'very-high',
                'X-Levels': ['low', 'very-high'],
                'X-Window': { start: 1, end: 5, mode: 'fast' },
                'X-Window-Exploded': { start: 2 },
              });
              if (dates && !(all.headers['X-Expires'] instanceof Date)) failures.push(label + ' all: X-Expires is no Date');
              same(label + ' all body', all.data, { name: 'a' });
            }

            const odd = await things.getStatus('odd');
            if (odd.status !== 'ok') {
              failures.push(label + ' odd: expected ok, got ' + odd.status);
            } else {
              same(label + ' odd', odd.headers, {
                'X-Mode': open ? 'turbo' : undefined,
                'X-Priority': undefined,
                'X-Count': undefined,
                'X-Flag': undefined,
                'X-Expires': dates ? undefined : 'soon',
                'X-Levels': inlineOpen ? ['low', 'unknown'] : undefined,
                'X-Window': undefined,
              });
            }

            const bare = await things.getStatus('bare');
            if (bare.status !== 'ok') failures.push(label + ' bare: expected ok, got ' + bare.status);
            else same(label + ' bare', Object.values(bare.headers).filter((value) => value !== undefined), []);

            const busy = await things.getStatus('busy');
            if (busy.status !== 'serverError') failures.push(label + ' busy: expected serverError, got ' + busy.status);
            else same(label + ' busy', busy.headers, { 'X-Mode': 'fast', 'Retry-After': 30, 'X-Trace': 't-1' });

            const gone = await things.getStatus('gone');
            if (gone.status !== 'notFound') failures.push(label + ' gone: expected notFound, got ' + gone.status);
            else same(label + ' gone', gone.headers, { 'X-Trace': 't-2' });

            const teapot = await things.getStatus('teapot');
            if (teapot.status !== 'clientError') failures.push(label + ' teapot: expected clientError, got ' + teapot.status);
            else same(label + ' teapot', teapot.headers, { 'X-Trace': 't-3' });

            console.log(label + ': checked');
          }

          server.close();

          if (failures.length > 0) {
            console.error('RESPONSE HEADER FAILURES:\n' + failures.join('\n'));
            process.exit(1);
          }
          console.log('ALL RESPONSE HEADER CHECKS PASSED');
          process.exit(0);
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exit(2);
        });
        """;

    private static void GenerateClient(
        OpenApiDocument doc,
        string outputPath,
        TypeScriptHttpClient httpClient,
        bool extensibleEnums,
        bool convertDates,
        TypeScriptEnumStyle enumStyle)
    {
        Directory.CreateDirectory(outputPath);
        var config = new TypeScriptClientConfig
        {
            HttpClient = httpClient,
            HooksStyle = TypeScriptHooksStyle.None,
            ExtensibleEnums = extensibleEnums,
            ConvertDates = convertDates,
            EnumStyle = enumStyle,
            GenerateFileHeaders = true,
            Scaffold = false,
        };

        TypeScriptClientGenerationService.Generate(doc, outputPath, config);
    }

    private static bool IsContinuousIntegration()
        => string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }
}