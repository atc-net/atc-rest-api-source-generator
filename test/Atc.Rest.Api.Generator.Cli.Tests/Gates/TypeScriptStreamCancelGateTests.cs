// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for a consumer that stops reading a stream early: the generated clients (Fetch and Axios) are
/// called against a Node http server that keeps sending Server-Sent Events / JSON Lines until the connection
/// closes. When the consumer breaks out of <c>for await</c> after two items, the HTTP body must be cancelled so
/// the server observes the disconnect promptly, instead of the response staying open until the process ends.
/// Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptStreamCancelGateTests
{
    [Fact]
    public void GeneratedClients_StreamConsumerStopsEarly_CancelsTheBody()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "StreamCancel.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-stream-cancel", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            GenerateClient(doc, Path.Combine(workDir, "fetch"), TypeScriptHttpClient.Fetch);
            GenerateClient(doc, Path.Combine(workDir, "axios"), TypeScriptHttpClient.Axios);

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            NodeWorkspace.Link(workDir);

            var run = NodeToolchain.RunNpx(
                "tsx harness.mts",
                workDir,
                TimeSpan.FromMinutes(3));

            Assert.True(
                run.ExitCode == 0,
                $"Generated client stream-cancel gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.1
        info:
          title: StreamCancel
          version: 1.0.0
        paths:
          /feed/events:
            get:
              operationId: streamEvents
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    text/event-stream:
                      schema:
                        $ref: '#/components/schemas/Tick'
          /feed/lines:
            get:
              operationId: streamLines
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    application/jsonl:
                      schema:
                        $ref: '#/components/schemas/Tick'
        components:
          schemas:
            Tick:
              type: object
              properties:
                n:
                  type: integer
        """;

    private const string PackageJson = """
        {
          "name": "stream-cancel-gate",
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

    // A server that sends one item every 50 ms until the connection closes, and reports when it saw the close.
    private const string Harness = """
        import { createServer } from 'node:http';
        import type { AddressInfo } from 'node:net';
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { FeedClient as FetchFeedClient } from './fetch/client/FeedClient';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { FeedClient as AxiosFeedClient } from './axios/client/FeedClient';

        let onClose: ((at: number) => void) | undefined;

        const server = createServer((req, res) => {
          const sse = req.url?.endsWith('/events') ?? false;
          res.writeHead(200, { 'Content-Type': sse ? 'text/event-stream' : 'application/jsonl' });
          let n = 0;
          const timer = setInterval(() => {
            n += 1;
            res.write(sse ? 'data: {"n":' + n + '}\n\n' : '{"n":' + n + '}\n');
          }, 50);
          res.on('close', () => {
            clearInterval(timer);
            onClose?.(Date.now());
          });
        });

        const failures: string[] = [];

        async function consumeTwo(name: string, stream: AsyncGenerator<unknown>): Promise<void> {
          const closedAt = new Promise<number>((resolve) => { onClose = resolve; });
          let count = 0;
          for await (const _item of stream) {
            count += 1;
            if (count === 2) {
              break;
            }
          }

          const brokeAt = Date.now();
          let waitTimer: ReturnType<typeof setTimeout> | undefined;
          const outcome = await Promise.race([
            closedAt.then((at) => 'server saw the close ' + (at - brokeAt) + ' ms after the consumer stopped'),
            new Promise<string>((resolve) => { waitTimer = setTimeout(() => resolve('server still sending 2000 ms after the consumer stopped'), 2000); }),
          ]);
          clearTimeout(waitTimer);

          if (count !== 2 || !outcome.startsWith('server saw the close')) {
            failures.push(name + ': read ' + count + ' item(s); ' + outcome);
          } else {
            console.log(name + ': ' + outcome);
          }
        }

        async function main(): Promise<void> {
          await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
          const baseUrl = 'http://127.0.0.1:' + (server.address() as AddressInfo).port;

          const fetchFeed = new FetchFeedClient(new FetchApiClient(baseUrl));
          const axiosFeed = new AxiosFeedClient(new AxiosApiClient(baseUrl));

          await consumeTwo('fetch sse', fetchFeed.streamEvents());
          await consumeTwo('fetch json-lines', fetchFeed.streamLines());
          await consumeTwo('axios sse', axiosFeed.streamEvents());
          await consumeTwo('axios json-lines', axiosFeed.streamLines());

          server.close();

          if (failures.length > 0) {
            console.error('STREAM CANCEL FAILURES:\n' + failures.join('\n'));
            process.exit(1);
          }
          console.log('ALL STREAM CANCEL CHECKS PASSED');
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