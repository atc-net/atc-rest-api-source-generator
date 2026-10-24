// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for a request body that is text (<c>text/plain</c>, a string) sent by the generated clients, against a
/// Node http server: the Fetch and the Axios client send the string as it is - UTF-8, with the declared media type and
/// its charset - not as JSON. Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptTextRequestBodyGateTests
{
    private const string SpecYaml = """
        openapi: 3.1.1
        info:
          title: Notes
          version: 1.0.0
        paths:
          /notes:
            post:
              operationId: createNote
              requestBody:
                required: true
                content:
                  text/plain:
                    schema:
                      type: string
              responses:
                '200':
                  description: What arrived
                  content:
                    text/plain:
                      schema:
                        type: string
        """;

    private const string PackageJson = """
        {
          "name": "atc-text-request-body-gate",
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
        import { NotesClient as FetchNotesClient } from './fetch/client/NotesClient';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { NotesClient as AxiosNotesClient } from './axios/client/NotesClient';

        // Answers the Content-Type and the body as the server decodes it from UTF-8
        const server = createServer((req, res) => {
          const chunks: Buffer[] = [];
          req.on('data', (chunk: Buffer) => chunks.push(chunk));
          req.on('end', () => {
            res.writeHead(200, { 'Content-Type': 'text/plain; charset=utf-8' });
            res.end(req.headers['content-type'] + ' [' + Buffer.concat(chunks).toString('utf8') + ']');
          });
        });

        const failures: string[] = [];

        function expect(name: string, actual: string, expected: string): void {
          if (actual !== expected) failures.push(name + ': got ' + actual + ' expected ' + expected);
          else console.log(name + ': ' + actual);
        }

        function text(result: { status: string; data?: unknown; error?: unknown }): string {
          return result.status === 'ok' ? String(result.data) : result.status + ' ' + String((result.error as Error | undefined)?.message);
        }

        async function main(): Promise<void> {
          await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
          const baseUrl = 'http://127.0.0.1:' + (server.address() as AddressInfo).port;

          const note = 'Grüße ✓ "not JSON"';
          expect('fetch sends the text', text(await new FetchNotesClient(new FetchApiClient(baseUrl)).createNote(note)), 'text/plain; charset=utf-8 [' + note + ']');
          expect('axios sends the text', text(await new AxiosNotesClient(new AxiosApiClient(baseUrl)).createNote(note)), 'text/plain; charset=utf-8 [' + note + ']');

          server.close();

          if (failures.length > 0) {
            console.error('TEXT REQUEST BODY FAILURES:\n' + failures.join('\n'));
            process.exitCode = 1;
            return;
          }
          console.log('ALL TEXT REQUEST BODY CHECKS PASSED');
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exit(2);
        });
        """;

    [Fact]
    public void GeneratedClients_SendTheText_AsItIs()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        Assert.True(OpenApiDocumentHelper.TryParseYaml(SpecYaml, "Notes.yaml", out var doc));

        var workDir = Path.Combine(Path.GetTempPath(), "atc-text-request-body", Guid.NewGuid().ToString("N"));
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
                $"Generated client text-request-body gate failed (exit {run.ExitCode}):\n{run.Output}");
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