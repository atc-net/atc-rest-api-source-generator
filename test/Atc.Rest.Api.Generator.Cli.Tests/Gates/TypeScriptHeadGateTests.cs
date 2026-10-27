namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for a HEAD operation of the generated clients (Fetch and Axios): a Node http server answers a HEAD as
/// a server does - the Content-Type and the Content-Length of the GET's answer, and no body. The operation declares the
/// GET's content, as specifications often do. A 200 must be <c>ok</c> without data - and typed without a model - and a
/// 404 <c>notFound</c>; neither may be a <c>parseError</c> over the body that is not there. Self-skips without a Node toolchain, but fails under CI
/// where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptHeadGateTests
{
    [Fact]
    public void GeneratedClients_ReadAHeadAnswer_WithoutABody()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "Head.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-head-gate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            GenerateClient(doc, Path.Combine(workDir, "fetch"), TypeScriptHttpClient.Fetch);
            GenerateClient(doc, Path.Combine(workDir, "axios"), TypeScriptHttpClient.Axios);

            // No model for a body that is never there: the method is typed without a value
            foreach (var client in (string[])["fetch", "axios"])
            {
                var things = File.ReadAllText(Path.Combine(workDir, client, "client", "ThingsClient.ts"));
                Assert.DoesNotContain("Thing>", things, StringComparison.Ordinal);
                Assert.DoesNotContain("import type { Thing }", things, StringComparison.Ordinal);
            }

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            NodeWorkspace.Link(workDir);

            var run = NodeToolchain.RunNpx(
                "tsx harness.mts",
                workDir,
                TimeSpan.FromMinutes(3));

            Assert.True(
                run.ExitCode == 0,
                $"Generated client HEAD gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.1
        info:
          title: Head
          version: 1.0.0
        paths:
          /things/{id}:
            head:
              operationId: headThing
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: The thing exists
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
                '404':
                  description: Not found
        components:
          schemas:
            Thing:
              type: object
              required: [id]
              properties:
                id:
                  type: string
        """;

    private const string PackageJson = """
        {
          "name": "head-gate",
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

    private const string Harness = """
        import { createServer } from 'node:http';
        import type { AddressInfo } from 'node:net';
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { ThingsClient as FetchThingsClient } from './fetch/client/ThingsClient';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { ThingsClient as AxiosThingsClient } from './axios/client/ThingsClient';

        // A HEAD answer: the headers the GET would have had, the length of its body included, and no body
        const server = createServer((req, res) => {
          const found = req.url === '/things/there';
          res.writeHead(found ? 200 : 404, {
            'Content-Type': found ? 'application/json' : 'application/problem+json',
            'Content-Length': '14',
            ETag: '"v1"',
          });
          res.end();
        });

        const failures: string[] = [];

        async function main(): Promise<void> {
          await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
          const baseUrl = 'http://127.0.0.1:' + (server.address() as AddressInfo).port;

          const clients = [
            ['fetch', new FetchThingsClient(new FetchApiClient(baseUrl))],
            ['axios', new AxiosThingsClient(new AxiosApiClient(baseUrl))],
          ] as const;
          for (const [label, things] of clients) {
            const there = await things.headThing('there');
            if (there.status !== 'ok') failures.push(label + ' 200: expected ok, got ' + there.status);
            else if (there.data !== undefined) failures.push(label + ' 200: expected no data, got ' + JSON.stringify(there.data));
            else if (there.response.headers.get('etag') !== '"v1"') failures.push(label + ' 200: the ETag header is not readable');
            else console.log(label + ' 200: ok without data');

            const missing = await things.headThing('missing');
            if (missing.status !== 'notFound') failures.push(label + ' 404: expected notFound, got ' + missing.status);
            else console.log(label + ' 404: notFound');
          }

          server.close();

          if (failures.length > 0) {
            console.error('HEAD FAILURES:\n' + failures.join('\n'));
            process.exitCode = 1;
            return;
          }
          console.log('ALL HEAD CHECKS PASSED');
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