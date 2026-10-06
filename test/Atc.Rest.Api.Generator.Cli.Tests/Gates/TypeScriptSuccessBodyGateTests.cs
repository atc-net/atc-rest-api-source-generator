// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for the body of a success response in the generated clients (Fetch and Axios), called through
/// their generated operations against a Node http server. One rule for both clients: an operation that declares
/// a JSON model reads its body as JSON whatever the Content-Type says, an empty body is <c>data: undefined</c>,
/// and a body that is not JSON is a <c>parseError</c>.
/// <list type="bullet">
///   <item><description>a 200 with a JSON body and no Content-Type header is the model (not a Blob, not the raw text);</description></item>
///   <item><description>a 201 with an empty body and no Content-Type is <c>created</c> with <c>data: undefined</c> (not a Blob, not <c>''</c>);</description></item>
///   <item><description>a 200 with <c>text/html</c> on a JSON operation is a <c>parseError</c> (not the HTML typed as the model);</description></item>
///   <item><description>a 200 with an empty <c>application/json</c> body is <c>data: undefined</c>;</description></item>
///   <item><description>a 201 of an operation that declares no body is <c>created</c> with <c>data: undefined</c>.</description></item>
/// </list>
/// Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptSuccessBodyGateTests
{
    [Fact]
    public void GeneratedClients_SuccessBody_FollowsTheDeclaredModelNotTheContentType()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "SuccessBody.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-success-body", Guid.NewGuid().ToString("N"));
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
                $"Generated client success-body gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.1
        info:
          title: SuccessBody
          version: 1.0.0
        paths:
          /items:
            post:
              operationId: createItem
              responses:
                '201':
                  description: Created
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Item'
          /items/plain:
            post:
              operationId: createPlain
              responses:
                '201':
                  description: Created
          /items/no-content-type:
            get:
              operationId: getWithoutContentType
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Item'
          /items/html:
            get:
              operationId: getHtml
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Item'
          /items/empty:
            get:
              operationId: getEmpty
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Item'
        components:
          schemas:
            Item:
              type: object
              properties:
                id:
                  type: string
        """;

    private const string PackageJson = """
        {
          "name": "success-body-gate",
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

    // A server whose answers bend the Content-Type: JSON without one, empty bodies, HTML on a JSON route.
    private const string Harness = """
        import { createServer } from 'node:http';
        import type { AddressInfo } from 'node:net';
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { ItemsClient as FetchItemsClient } from './fetch/client/ItemsClient';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { ItemsClient as AxiosItemsClient } from './axios/client/ItemsClient';

        const server = createServer((req, res) => {
          const route = req.method + ' ' + req.url;
          switch (route) {
            case 'GET /items/no-content-type':
              res.writeHead(200);
              res.end('{"id":"1"}');
              return;
            case 'POST /items':
              res.writeHead(201);
              res.end();
              return;
            case 'POST /items/plain':
              res.writeHead(201);
              res.end();
              return;
            case 'GET /items/html':
              res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
              res.end('<html><body>Sign in</body></html>');
              return;
            case 'GET /items/empty':
              res.writeHead(200, { 'Content-Type': 'application/json' });
              res.end();
              return;
            default:
              res.writeHead(404);
              res.end();
          }
        });

        const failures: string[] = [];
        function check(name: string, result: any, cond: boolean): void {
          if (!cond) {
            const data = result?.data;
            const shape = data === undefined ? 'undefined' : typeof Blob !== 'undefined' && data instanceof Blob ? 'Blob(' + data.size + ')' : JSON.stringify(data);
            failures.push(name + ': got status ' + String(result?.status) + ', data ' + shape);
          }
        }

        async function main(): Promise<void> {
          await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
          const baseUrl = 'http://127.0.0.1:' + (server.address() as AddressInfo).port;

          for (const [label, items] of [
            ['fetch', new FetchItemsClient(new FetchApiClient(baseUrl))],
            ['axios', new AxiosItemsClient(new AxiosApiClient(baseUrl))],
          ] as const) {
            const client: any = items;

            const noContentType = await client.getWithoutContentType();
            check(label + ' 200 JSON without Content-Type -> the model', noContentType, noContentType.status === 'ok' && noContentType.data?.id === '1');

            const created = await client.createItem();
            check(label + ' 201 empty body on a model -> created, data undefined', created, created.status === 'created' && created.data === undefined);

            const html = await client.getHtml();
            check(label + ' 200 text/html on a JSON operation -> parseError', html, html.status === 'parseError');

            const empty = await client.getEmpty();
            check(label + ' 200 empty application/json -> ok, data undefined', empty, empty.status === 'ok' && empty.data === undefined);

            const plain = await client.createPlain();
            check(label + ' 201 of an operation without a body -> created, data undefined', plain, plain.status === 'created' && plain.data === undefined);
          }

          let exitCode = 0;
          if (failures.length > 0) {
            console.error('SUCCESS BODY FAILURES:\n' + failures.join('\n'));
            exitCode = 1;
          } else {
            console.log('ALL SUCCESS BODY CHECKS PASSED');
          }

          // The kept-alive client connections are closed and the loop drains; process.exit while libuv is still
          // closing handles trips an assertion in Node on Windows, so it is only the fallback.
          process.exitCode = exitCode;
          server.closeAllConnections();
          server.close();
          setTimeout(() => process.exit(exitCode), 2000).unref();
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