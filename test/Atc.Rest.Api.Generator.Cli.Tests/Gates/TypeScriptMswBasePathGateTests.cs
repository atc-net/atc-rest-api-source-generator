// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for the generated MSW handlers of an API with a base path: the AsyncEnumerableHooks scenario
/// declares <c>servers: - url: /api/v1</c>, so the generated client requests <c>/api/v1/items</c>. The handlers
/// are installed with <c>msw/node</c> and must answer that request (a handler for <c>/items</c> alone never
/// matches). Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptMswBasePathGateTests
{
    [Fact]
    public void GeneratedMswHandlers_MatchTheRequestsOfAnApiWithABasePath()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var yamlPath = CliTestHelper.GetScenarioYamlPath("AsyncEnumerableHooks");
        var doc = OpenApiDocumentHelper.TryParseYaml(File.ReadAllText(yamlPath), "AsyncEnumerableHooks.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-msw-base-path", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            var outputPath = Path.Combine(workDir, "generated");
            Directory.CreateDirectory(outputPath);
            TypeScriptClientGenerationService.Generate(
                doc,
                outputPath,
                new TypeScriptClientConfig
                {
                    HttpClient = TypeScriptHttpClient.Fetch,
                    HooksStyle = TypeScriptHooksStyle.None,
                    GenerateMswHandlers = true,
                    GenerateFileHeaders = true,
                    Scaffold = false,
                });

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            NodeWorkspace.Link(workDir);

            var run = NodeToolchain.RunNpx(
                "tsx harness.mts",
                workDir,
                TimeSpan.FromMinutes(3));

            Assert.True(
                run.ExitCode == 0,
                $"Generated MSW handlers base-path gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private const string PackageJson = """
        {
          "name": "msw-base-path-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "devDependencies": {
            "@types/node": "^22.0.0",
            "msw": "^2.7.0",
            "tsx": "^4.19.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    // The generated handlers intercept the generated client's requests (base URL with the spec's /api/v1 path).
    private const string Harness = """
        import { setupServer } from 'msw/node';
        import { handlers } from './generated/mocks';
        import { ApiClient } from './generated/client/ApiClient';
        import { ItemsClient } from './generated/client/ItemsClient';

        const failures: string[] = [];
        function check(name: string, cond: boolean, detail: string): void {
          if (!cond) {
            failures.push(name + ': ' + detail);
          }
        }

        async function main(): Promise<void> {
          const server = setupServer(...handlers);
          server.listen({ onUnhandledRequest: 'error' });

          try {
            const response = await fetch('http://localhost/api/v1/items/00000000-0000-0000-0000-000000000001/logs');
            check('fetch /api/v1/items/{id}/logs', response.status === 200, 'expected the mocked 200, got ' + response.status);
          } catch (e: any) {
            check('fetch /api/v1/items/{id}/logs', false, 'not intercepted: ' + String(e?.message ?? e));
          }

          const items = new ItemsClient(new ApiClient('http://localhost/api/v1'));
          try {
            const logs: any = await items.getItemLogs('00000000-0000-0000-0000-000000000001');
            check('client getItemLogs', logs.status === 'ok' && logs.data !== undefined, 'expected the mocked ok, got ' + String(logs.status) + ' with data ' + JSON.stringify(logs.data));
          } catch (e: any) {
            check('client getItemLogs', false, 'not intercepted: ' + String(e?.message ?? e));
          }

          try {
            let count = 0;
            for await (const _item of items.listItems()) {
              count += 1;
            }
            check('client listItems (stream)', count > 0, 'expected the mocked items, got ' + count);
          } catch (e: any) {
            check('client listItems (stream)', false, 'not intercepted: ' + String(e?.message ?? e));
          }

          server.close();

          if (failures.length > 0) {
            console.error('MSW BASE PATH FAILURES:\n' + failures.join('\n'));
            process.exitCode = 1;
            return;
          }
          console.log('ALL MSW BASE PATH CHECKS PASSED');
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exit(2);
        });
        """;

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