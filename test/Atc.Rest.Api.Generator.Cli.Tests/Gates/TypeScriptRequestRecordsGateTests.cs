// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for a request body sent as a stream of records (JSON Lines, a JSON Text Sequence) by the generated
/// clients, against a Node http server: the Fetch client streams an async source outside a browser (each record reaches
/// the server before the source yields the next), buffers it with <c>requestStreaming: 'buffer'</c>, and sends a list
/// whole; the Axios client buffers; a body streamed in while the answer streams out works with both. Self-skips without
/// a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptRequestRecordsGateTests
{
    private const string SpecYaml = """
        openapi: 3.1.1
        info:
          title: Records
          version: 1.0.0
        paths:
          /records/lines:
            post:
              operationId: importLines
              requestBody:
                required: true
                content:
                  application/jsonl:
                    schema:
                      $ref: '#/components/schemas/Reading'
              responses:
                '200':
                  description: What arrived
                  content:
                    text/plain:
                      schema:
                        type: string
          /records/seq:
            post:
              operationId: importSequence
              requestBody:
                required: true
                content:
                  application/json-seq:
                    schema:
                      $ref: '#/components/schemas/Reading'
              responses:
                '200':
                  description: What arrived
                  content:
                    text/plain:
                      schema:
                        type: string
          /records/echo:
            post:
              operationId: echoReadings
              requestBody:
                required: true
                content:
                  application/x-ndjson:
                    schema:
                      $ref: '#/components/schemas/Reading'
              responses:
                '200':
                  description: The readings back
                  content:
                    application/x-ndjson:
                      schema:
                        $ref: '#/components/schemas/Reading'
        components:
          schemas:
            Reading:
              title: Reading
              type: object
              required: [deviceId, value]
              properties:
                deviceId:
                  type: string
                value:
                  type: number
        """;

    private const string PackageJson = """
        {
          "name": "atc-request-records-gate",
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

    private const string Harness = """
        import { createServer } from 'node:http';
        import type { AddressInfo } from 'node:net';
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { RecordsClient as FetchRecordsClient } from './fetch/client/RecordsClient';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { RecordsClient as AxiosRecordsClient } from './axios/client/RecordsClient';

        type Reading = { deviceId: string; value: number };

        // Resolved when the server has read the first line of the body in flight
        let firstLineArrived: () => void = () => {};
        let firstLine = new Promise<void>((resolve) => { firstLineArrived = resolve; });

        const server = createServer((req, res) => {
          const framing = req.headers['transfer-encoding'] === 'chunked' ? 'chunked' : 'length';
          let text = '';
          req.setEncoding('utf8');
          req.on('data', (chunk: string) => {
            text += chunk;
            if (text.includes('\n')) firstLineArrived();
          });
          req.on('end', () => {
            if (req.url === '/records/echo') {
              res.writeHead(200, { 'Content-Type': 'application/x-ndjson' });
              for (const line of text.split('\n').filter((l) => l.length > 0)) {
                const reading = JSON.parse(line) as Reading;
                res.write(JSON.stringify({ ...reading, deviceId: reading.deviceId.toUpperCase() }) + '\n');
              }
              res.end();
              return;
            }

            res.writeHead(200, { 'Content-Type': 'text/plain' });
            res.end(framing + ' ' + req.headers['content-type'] + ' ' + JSON.stringify(text));
          });
        });

        const failures: string[] = [];

        function expect(name: string, actual: string, expected: string): void {
          if (actual !== expected) failures.push(name + ': got ' + actual + ' expected ' + expected);
          else console.log(name + ': ' + actual);
        }

        async function* readings(...ids: string[]): AsyncGenerator<Reading> {
          for (const id of ids) {
            await new Promise((resolve) => setTimeout(resolve, 1));
            yield { deviceId: id, value: 1 };
          }
        }

        // The second record is held back until the server has the first: a client that buffers would never send it
        async function* heldBack(): AsyncGenerator<Reading> {
          firstLine = new Promise<void>((resolve) => { firstLineArrived = resolve; });
          yield { deviceId: 'a', value: 1 };
          const arrived = await Promise.race([firstLine.then(() => true), new Promise<boolean>((resolve) => setTimeout(() => resolve(false), 3000))]);
          yield { deviceId: arrived ? 'b' : 'not-streamed', value: 1 };
        }

        function text(result: { status: string; data?: unknown; error?: unknown }): string {
          return result.status === 'ok' ? String(result.data) : result.status + ' ' + String((result.error as Error | undefined)?.message);
        }

        async function main(): Promise<void> {
          await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
          const baseUrl = 'http://127.0.0.1:' + (server.address() as AddressInfo).port;
          const lines = (...ids: string[]) => JSON.stringify(ids.map((id) => JSON.stringify({ deviceId: id, value: 1 }) + '\n').join(''));

          const fetchClient = new FetchRecordsClient(new FetchApiClient(baseUrl));
          const fetchBuffered = new FetchRecordsClient(new FetchApiClient(baseUrl, { requestStreaming: 'buffer' }));
          const axiosClient = new AxiosRecordsClient(new AxiosApiClient(baseUrl));

          expect('fetch streams an async source', text(await fetchClient.importLines(heldBack())), 'chunked application/jsonl ' + lines('a', 'b'));
          expect('fetch buffers with requestStreaming buffer', text(await fetchBuffered.importLines(readings('a', 'b'))), 'length application/jsonl ' + lines('a', 'b'));
          expect('fetch sends a list whole', text(await fetchClient.importLines([{ deviceId: 'x', value: 1 }])), 'length application/jsonl ' + lines('x'));
          expect('fetch json-seq', text(await fetchClient.importSequence(readings('a'))), 'chunked application/json-seq ' + JSON.stringify('\x1e' + JSON.stringify({ deviceId: 'a', value: 1 }) + '\n'));
          expect('axios buffers', text(await axiosClient.importLines(readings('a', 'b'))), 'length application/jsonl ' + lines('a', 'b'));
          expect('axios json-seq', text(await axiosClient.importSequence([{ deviceId: 'a', value: 1 }])), 'length application/json-seq ' + JSON.stringify('\x1e' + JSON.stringify({ deviceId: 'a', value: 1 }) + '\n'));

          for (const [label, client] of [['fetch', fetchClient], ['axios', axiosClient]] as const) {
            const echoed: string[] = [];
            for await (const reading of client.echoReadings(readings('a', 'b'))) echoed.push(reading.deviceId);
            expect(label + ' streams in and out', echoed.join(','), 'A,B');
          }

          server.close();

          if (failures.length > 0) {
            console.error('REQUEST RECORDS FAILURES:\n' + failures.join('\n'));
            process.exitCode = 1;
            return;
          }
          console.log('ALL REQUEST RECORDS CHECKS PASSED');
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exit(2);
        });
        """;

    [Fact]
    public void GeneratedClients_SendRecords_StreamedOrBuffered()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        Assert.True(OpenApiDocumentHelper.TryParseYaml(SpecYaml, "Records.yaml", out var doc));

        var workDir = Path.Combine(Path.GetTempPath(), "atc-request-records", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            GenerateClient(doc!, Path.Combine(workDir, "fetch"), TypeScriptHttpClient.Fetch);
            GenerateClient(doc!, Path.Combine(workDir, "axios"), TypeScriptHttpClient.Axios);

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            NodeWorkspace.Link(workDir);

            var run = NodeToolchain.RunNpx("tsx harness.mts", workDir, TimeSpan.FromMinutes(3));

            Assert.True(
                run.ExitCode == 0,
                $"Generated client request-records gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private static void GenerateClient(
        OpenApiDocument doc,
        string outputPath,
        TypeScriptHttpClient httpClient)
    {
        Directory.CreateDirectory(outputPath);
        var config = new TypeScriptClientConfig
        {
            HttpClient = httpClient,
            HooksStyle = TypeScriptHooksStyle.None,
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
