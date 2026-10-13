// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for the stream readers of the generated clients (Fetch and Axios): a Node http server sends a JSON
/// array, JSON Lines, a JSON Text Sequence and a multipart/mixed body in small chunks that cut items in the middle
/// of strings, numbers and nested values, with items of every JSON kind (objects, arrays, strings, numbers,
/// literals) and braces inside strings. The client must yield exactly the items the server sent - and, with a record
/// limit shorter than an item, reject instead of buffering on. Self-skips without a Node toolchain, but fails under CI
/// where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptStreamFramingsGateTests
{
    [Fact]
    public void GeneratedClients_ReadEveryFraming_WhateverTheChunksAndTheItems()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "StreamFramings.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-stream-framings", Guid.NewGuid().ToString("N"));
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
                $"Generated client stream-framings gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.1
        info:
          title: StreamFramings
          version: 1.0.0
        paths:
          /feed/array:
            get:
              operationId: streamArray
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items: {}
          /feed/lines:
            get:
              operationId: streamLines
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    application/jsonl:
                      schema: {}
          /feed/seq:
            get:
              operationId: streamSeq
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    application/json-seq:
                      schema: {}
          /feed/mixed:
            get:
              operationId: streamMixed
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    multipart/mixed:
                      schema: {}
          /feed/sse:
            get:
              operationId: streamSse
              x-return-async-enumerable: true
              x-sse-item-events: [update]
              responses:
                '200':
                  description: OK
                  content:
                    text/event-stream:
                      schema: {}
          /feed/mixed-no-boundary:
            get:
              operationId: streamMixedNoBoundary
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    multipart/mixed:
                      schema: {}
        """;

    private const string PackageJson = """
        {
          "name": "stream-framings-gate",
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

    // The items every framing carries, and a server that writes each body in 5-byte chunks with a pause between them
    private const string Harness = """
        import { createServer } from 'node:http';
        import type { AddressInfo } from 'node:net';
        import { ApiClient as FetchApiClient, streamLimits as fetchStreamLimits } from './fetch/client/ApiClient';
        import { FeedClient as FetchFeedClient } from './fetch/client/FeedClient';
        import { ApiClient as AxiosApiClient, streamLimits as axiosStreamLimits } from './axios/client/ApiClient';
        import { FeedClient as AxiosFeedClient } from './axios/client/FeedClient';

        const items: unknown[] = [{ n: 1, s: 'a}b]c' }, 'x]y{z', 2.5, -7, [3, { four: 4 }], null, true, { n: 2, nested: { deep: [1, '2'] } }];
        const json = items.map((i) => JSON.stringify(i));

        const bodies: Record<string, { contentType: string; text: string }> = {
          '/feed/array': { contentType: 'application/json', text: '[' + json.join(',\n') + '\n]' },
          '/feed/lines': { contentType: 'application/jsonl', text: json.join('\n') + '\n\n' + '' },
          '/feed/seq': { contentType: 'application/json-seq', text: json.map((j) => '\x1e' + j + '\n').join('') },
          '/feed/mixed': {
            contentType: 'multipart/mixed; boundary=frame',
            text: json.map((j) => '--frame\r\nContent-Type: application/json\r\n\r\n' + j + '\r\n').join('') + '--frame--\r\n',
          },
          // Besides the items (in named and unnamed events): a comment, an event without data, a heartbeat whose data
          // is no item - all skipped
          '/feed/sse': {
            contentType: 'text/event-stream',
            text: ': keep-alive\n\n' + json.map((j, i) => (i % 2 === 0 ? 'event: update\n' : '') + 'data: ' + j + '\n\n' + (i === 1 ? 'data:\n\nevent: ping\ndata: ping\n\nevent: ping\ndata: {"n":99}\n\nevent: ping\n\n' : '')).join(''),
          },
          // No boundary named: the parts cannot be found - an error, not an empty stream
          '/feed/mixed-no-boundary': {
            contentType: 'multipart/mixed',
            text: json.map((j) => '--frame\r\nContent-Type: application/json\r\n\r\n' + j + '\r\n').join('') + '--frame--\r\n',
          },
        };

        const server = createServer((req, res) => {
          const body = bodies[req.url ?? ''];
          if (!body) { res.writeHead(404); res.end(); return; }
          res.writeHead(200, { 'Content-Type': body.contentType });
          const chunks: string[] = [];
          for (let i = 0; i < body.text.length; i += 5) chunks.push(body.text.substring(i, i + 5));
          let k = 0;
          const timer = setInterval(() => {
            if (k >= chunks.length) { clearInterval(timer); res.end(); return; }
            res.write(chunks[k++]);
          }, 2);
        });

        const failures: string[] = [];

        async function check(name: string, stream: AsyncGenerator<unknown>): Promise<void> {
          const got: unknown[] = [];
          try {
            for await (const item of stream) got.push(item);
          } catch (e) {
            failures.push(name + ': threw ' + String((e as Error)?.message ?? e));
            return;
          }
          const expected = JSON.stringify(items);
          const actual = JSON.stringify(got);
          if (actual !== expected) {
            failures.push(name + ': got ' + actual + ' expected ' + expected);
          } else {
            console.log(name + ': ' + got.length + ' items');
          }
        }

        async function checkThrows(name: string, stream: AsyncGenerator<unknown>, rangeError = false): Promise<void> {
          const got: unknown[] = [];
          try {
            for await (const item of stream) got.push(item);
          } catch (e) {
            if (rangeError && !(e instanceof RangeError && got.length === 0)) {
              failures.push(name + ': not a RangeError before the first item, but ' + String(e) + ' after ' + JSON.stringify(got));
              return;
            }

            console.log(name + ': threw');
            return;
          }
          failures.push(name + ': did not throw, got ' + JSON.stringify(got));
        }

        async function main(): Promise<void> {
          await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
          const baseUrl = 'http://127.0.0.1:' + (server.address() as AddressInfo).port;

          const fetchFeed = new FetchFeedClient(new FetchApiClient(baseUrl));
          const axiosFeed = new AxiosFeedClient(new AxiosApiClient(baseUrl));

          for (const [label, feed] of [['fetch', fetchFeed], ['axios', axiosFeed]] as const) {
            await check(label + ' json-array', feed.streamArray());
            await check(label + ' json-lines', feed.streamLines());
            await check(label + ' json-seq', feed.streamSeq());
            await check(label + ' multipart', feed.streamMixed());
            await check(label + ' server-sent events', feed.streamSse());
            await checkThrows(label + ' multipart without a boundary', feed.streamMixedNoBoundary());
          }

          // A record longer than the limit (a server that never ends one) is an error, not a buffer that grows until
          // the tab or the process runs out of memory: every framing, both clients. The longest item is over 16.
          fetchStreamLimits.maxRecordLength = 16;
          axiosStreamLimits.maxRecordLength = 16;
          for (const [label, feed] of [['fetch', fetchFeed], ['axios', axiosFeed]] as const) {
            await checkThrows(label + ' json-array past the record limit', feed.streamArray(), true);
            await checkThrows(label + ' json-lines past the record limit', feed.streamLines(), true);
            await checkThrows(label + ' json-seq past the record limit', feed.streamSeq(), true);
            await checkThrows(label + ' multipart past the record limit', feed.streamMixed(), true);
            await checkThrows(label + ' server-sent events past the record limit', feed.streamSse(), true);
          }

          server.close();

          if (failures.length > 0) {
            console.error('STREAM FRAMING FAILURES:\n' + failures.join('\n'));
            process.exit(1);
          }
          console.log('ALL STREAM FRAMING CHECKS PASSED');
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