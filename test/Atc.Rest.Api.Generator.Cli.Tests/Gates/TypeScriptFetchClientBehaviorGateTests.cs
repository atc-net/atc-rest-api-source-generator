// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime behaviour gate for the generated Fetch <c>ApiClient</c>. Complements the tsc /
/// snapshot gates by actually <em>executing</em> the generated client against a mocked global
/// <c>fetch</c>. The Fetch client already handles JSON string success bodies correctly (it wraps
/// <c>response.json()</c> in a real try/catch), so those cases are regression guards; the load-
/// bearing assertion is that an <c>application/problem+json</c> error body is now recognised as
/// JSON and its <c>errors</c> reach the <c>badRequest</c> arm rather than falling through to
/// <c>blob()</c> and being lost.
/// <para>
/// Asserts, in both <c>convertDates</c> modes:
/// </para>
/// <list type="bullet">
///   <item><description><c>application/json</c> + <c>"token"</c> → <c>{ status: 'ok', data: 'token' }</c></description></item>
///   <item><description><c>application/json</c> + a genuinely non-JSON body → <c>{ status: 'parseError' }</c></description></item>
///   <item><description><c>text/plain</c> + a string body → <c>{ status: 'ok' }</c></description></item>
///   <item><description><c>application/problem+json</c> 400 with <c>errors</c> → <c>{ status: 'badRequest' }</c></description></item>
/// </list>
/// Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptFetchClientBehaviorGateTests
{
    [Fact]
    public void GeneratedFetchClient_JsonBodies_ResolveToExpectedArms()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the behaviour gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "StringResponse.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-fetch-behaviour", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            GenerateClient(doc, Path.Combine(workDir, "convertDatesOff"), convertDates: false);
            GenerateClient(doc, Path.Combine(workDir, "convertDatesOn"), convertDates: true);

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            var install = NodeToolchain.RunNpm(
                "install --no-audit --no-fund --loglevel=error",
                workDir,
                TimeSpan.FromMinutes(5));
            Assert.True(
                install.ExitCode == 0,
                $"npm install failed (exit {install.ExitCode}):\n{install.Output}");

            var run = NodeToolchain.RunNpx(
                "tsx harness.mts",
                workDir,
                TimeSpan.FromMinutes(3));

            Assert.True(
                run.ExitCode == 0,
                $"Generated Fetch client behaviour gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private static void GenerateClient(
        OpenApiDocument doc,
        string outputPath,
        bool convertDates)
    {
        Directory.CreateDirectory(outputPath);
        var config = new TypeScriptClientConfig
        {
            HttpClient = TypeScriptHttpClient.Fetch,
            HooksStyle = TypeScriptHooksStyle.None,
            ConvertDates = convertDates,
            GenerateFileHeaders = true,
            Scaffold = false,
        };

        TypeScriptClientGenerationService.Generate(doc, outputPath, config);
    }

    private const string SpecYaml = """
        openapi: 3.0.1
        info:
          title: StringResponse
          version: 1.0.0
        paths:
          /token:
            get:
              operationId: getToken
              responses:
                '200':
                  description: A job token.
                  content:
                    application/json:
                      schema:
                        type: string
        """;

    private const string PackageJson = """
        {
          "name": "fetch-behaviour-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "devDependencies": {
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
            "esModuleInterop": true,
            "skipLibCheck": true,
            "noEmit": true
          }
        }
        """;

    // Runs the generated Fetch client against a mocked global fetch. Each case installs the next
    // Response the client will receive; a call counter guards against a false green where fetch
    // is never actually invoked. tsx strips types and runs directly (types are the tsc gate's job).
    private const string Harness = """
        import { ApiClient as ApiClientOff } from './convertDatesOff/client/ApiClient';
        import { ApiClient as ApiClientOn } from './convertDatesOn/client/ApiClient';

        let nextResponse: () => Response = () => new Response(null, { status: 500 });
        let fetchCalls = 0;

        (globalThis as any).fetch = async () => {
          fetchCalls += 1;
          return nextResponse();
        };

        function respondWith(body: string, contentType: string, status = 200): void {
          nextResponse = () => new Response(body, { status, headers: { 'Content-Type': contentType } });
        }

        const failures: string[] = [];
        function check(name: string, cond: boolean, detail: string): void {
          if (!cond) {
            failures.push(name + ': ' + detail);
          }
        }

        async function runCases(label: string, client: any): Promise<void> {
          // Case 1: application/json + JSON string body -> ok, verbatim string.
          respondWith(JSON.stringify('token'), 'application/json');
          const callsBefore = fetchCalls;
          const r1 = await client.request('get', '/token');
          check(label + ' fetch-invoked', fetchCalls > callsBefore, 'mocked fetch was never called');
          check(label + ' json-string-ok', r1.status === 'ok', 'expected ok, got ' + r1.status);
          check(label + ' json-string-data', r1.status === 'ok' && r1.data === 'token', 'expected data "token", got ' + JSON.stringify(r1.data));

          // Case 2: application/json + genuinely non-JSON body -> parseError.
          respondWith('<html>not json</html>', 'application/json');
          const r2 = await client.request('get', '/broken');
          check(label + ' broken-json-parseError', r2.status === 'parseError', 'expected parseError, got ' + r2.status);

          // Case 3: text/plain + string body -> ok, verbatim.
          respondWith('hello world', 'text/plain');
          const r3 = await client.request('get', '/text');
          check(label + ' text-plain-ok', r3.status === 'ok', 'expected ok, got ' + r3.status);
          check(label + ' text-plain-data', r3.status === 'ok' && r3.data === 'hello world', 'expected verbatim, got ' + JSON.stringify(r3.data));

          // Case 4: application/problem+json 400 with errors -> badRequest (the fix).
          respondWith(JSON.stringify({ title: 'Validation failed', errors: { name: ['required'] } }), 'application/problem+json', 400);
          const r4 = await client.request('get', '/validate');
          check(label + ' problem-json-badRequest', r4.status === 'badRequest', 'expected badRequest, got ' + r4.status);
        }

        async function main(): Promise<void> {
          const off = new ApiClientOff('http://localhost');
          const on = new ApiClientOn('http://localhost');
          await runCases('convertDates=false', off);
          await runCases('convertDates=true', on);

          if (failures.length > 0) {
            console.error('BEHAVIOUR FAILURES:\n' + failures.join('\n'));
            process.exit(1);
          }
          console.log('ALL BEHAVIOUR CHECKS PASSED');
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