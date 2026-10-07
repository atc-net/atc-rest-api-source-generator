// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for the URL the generated TypeScript clients request (Fetch and Axios):
/// <list type="bullet">
///   <item><description>a path parameter is percent-encoded, so <c>a/b?c#d</c> stays one path segment;</description></item>
///   <item><description>a relative base URL (<c>/api</c>, or an empty one) resolves against the origin of the page instead of throwing;</description></item>
///   <item><description>without a page (server-side rendering, Node) a relative base URL stays relative instead of throwing.</description></item>
/// </list>
/// Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptRequestUrlGateTests
{
    private const string SpecYaml = """
        openapi: 3.0.1
        info:
          title: RequestUrl
          version: 1.0.0
        paths:
          /items/{id}:
            get:
              operationId: getItem
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '204':
                  description: Found.
        """;

    private const string PackageJson = """
        {
          "name": "request-url-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "dependencies": {
            "axios": "^1.7.0"
          },
          "devDependencies": {
            "tsx": "^4.19.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    // The Fetch client is called through its generated method against a mocked global fetch that records
    // the URL. buildUrl is called directly on both clients (it is private on the Axios one, hence `as any`).
    private const string Harness = """
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { ItemsClient as FetchItemsClient } from './fetch/client/ItemsClient';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';

        const requested: string[] = [];
        (globalThis as any).fetch = async (input: any) => {
          requested.push(typeof input === 'string' ? input : input.url ?? String(input));
          return new Response(null, { status: 204 });
        };

        const failures: string[] = [];
        function check(name: string, actual: unknown, expected: unknown): void {
          if (actual !== expected) {
            failures.push(name + ': expected ' + JSON.stringify(expected) + ', got ' + JSON.stringify(actual));
          }
        }

        function buildUrl(client: any, path: string, query?: Record<string, string>): string {
          try {
            return client.buildUrl(path, query);
          } catch (e) {
            return 'THROWS ' + (e as Error).message;
          }
        }

        async function main(): Promise<void> {
          // 1. A path value with reserved characters stays one segment.
          const items = new FetchItemsClient(new FetchApiClient('http://localhost:5000'));
          await items.getItem('a/b?c#d');
          check('fetch path encoding', requested[0], 'http://localhost:5000/items/a%2Fb%3Fc%23d');

          // 2. An absolute base URL is used as it is.
          for (const [label, ctor] of [['fetch', FetchApiClient], ['axios', AxiosApiClient]] as const) {
            check(label + ' absolute base', buildUrl(new ctor('http://localhost:5000/api'), '/items', { q: '1' }), 'http://localhost:5000/api/items?q=1');
          }

          // 3. Without a page (server-side rendering, Node) a relative base URL stays relative instead of throwing: the
          // request goes to a fetch that knows its origin (or fails there), and building a URL never throws.
          for (const [label, ctor] of [['fetch', FetchApiClient], ['axios', AxiosApiClient]] as const) {
            check(label + ' relative base without a page', buildUrl(new ctor('/api'), '/items', { q: '1' }), '/api/items?q=1');
            check(label + ' empty base without a page', buildUrl(new ctor(''), '/items'), '/items');
            check(label + ' relative base without a page, array query', buildUrl(new ctor('/api/'), '/items', { tag: ['a b', 'c'] } as any), '/api/items?tag=a+b&tag=c');
            check(label + ' relative base without a page, path with a query', buildUrl(new ctor('/api'), '/search?raw=1', { q: '2' }), '/api/search?raw=1&q=2');          }

          const relativeItems = new FetchItemsClient(new FetchApiClient('/api'));
          let relativeError = '';
          try {
            await relativeItems.getItem('a/b');
          } catch (e) {
            relativeError = (e as Error).message;
          }
          check('fetch relative request without a page', relativeError === '' ? requested[requested.length - 1] : 'THROWS ' + relativeError, '/api/items/a%2Fb');

          // 4. A relative base URL resolves against the origin of the page (a browser app served with its API).
          (globalThis as any).location = { origin: 'http://localhost:5173' };
          for (const [label, ctor] of [['fetch', FetchApiClient], ['axios', AxiosApiClient]] as const) {
            check(label + ' relative base', buildUrl(new ctor('/api'), '/items', { q: '1' }), 'http://localhost:5173/api/items?q=1');
            check(label + ' empty base', buildUrl(new ctor(''), '/items'), 'http://localhost:5173/items');
          }

          if (failures.length > 0) {
            console.error('REQUEST URL FAILURES:\n' + failures.join('\n'));
            process.exit(1);
          }
          console.log('ALL REQUEST URL CHECKS PASSED');
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exit(2);
        });
        """;

    private const string TsConfig = """
        {
          "compilerOptions": {
            "target": "ES2020",
            "lib": ["ES2020", "DOM"],
            "module": "ESNext",
            "moduleResolution": "bundler",
            "strict": true,
            "esModuleInterop": true,
            "skipLibCheck": true,
            "noEmit": true
          }
        }
        """;

    [Fact]
    public void GeneratedClients_BuildTheRequestUrl()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "RequestUrl.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-request-url", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            GenerateClient(doc, Path.Combine(workDir, "fetch"), TypeScriptHttpClient.Fetch);
            GenerateClient(doc, Path.Combine(workDir, "axios"), TypeScriptHttpClient.Axios);

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            NodeWorkspace.Link(workDir);

            var run = NodeToolchain.RunNpx(
                "tsx harness.mts",
                workDir,
                TimeSpan.FromMinutes(3));

            Assert.True(
                run.ExitCode == 0,
                $"Generated client request-URL gate failed (exit {run.ExitCode}):\n{run.Output}");
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