// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime behaviour gate for the generated Axios <c>ApiClient</c>. Snapshot (<c>.verified.ts</c>)
/// tests assert the generated <em>source</em> but never <em>execute</em> it, so they cannot catch
/// the class of bug where a JSON string success body (<c>application/json</c> + <c>type: string</c>,
/// e.g. a job-id token) is wrongly surfaced as <c>parseError</c>.
/// <para>
/// This gate generates a minimal <c>type: string</c> 200 client in both <c>convertDates</c> modes,
/// then runs it under a real Axios with a mocked adapter and asserts:
/// </para>
/// <list type="bullet">
///   <item><description><c>application/json</c> + <c>"token"</c> → <c>{ status: 'ok', data: 'token' }</c></description></item>
///   <item><description><c>application/json</c> + a genuinely non-JSON body → <c>{ status: 'parseError' }</c></description></item>
///   <item><description><c>text/plain</c> + a string body → <c>{ status: 'ok' }</c> (verbatim, no regression)</description></item>
/// </list>
/// The test self-skips when a Node toolchain is unavailable, but fails under CI where Node is
/// provisioned (same posture as the tsc gate) so the guard stays tamper-evident.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptAxiosClientBehaviorGateTests
{
    [Fact]
    public void GeneratedAxiosClient_JsonStringBody_ResolvesToOkNotParseError()
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

        var workDir = Path.Combine(Path.GetTempPath(), "atc-axios-behaviour", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            // Emit the same client in both convertDates modes so we prove the sentinel /
            // transformResponse exists regardless of the date-revival posture (edge case 1:
            // convertDates == false must still emit transformResponse).
            GenerateClient(doc!, Path.Combine(workDir, "convertDatesOff"), convertDates: false);
            GenerateClient(doc!, Path.Combine(workDir, "convertDatesOn"), convertDates: true);

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
                $"Generated Axios client behaviour gate failed (exit {run.ExitCode}):\n{run.Output}");
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
            HttpClient = TypeScriptHttpClient.Axios,
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
          "name": "axios-behaviour-gate",
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

    // Runs the generated client against a mocked Axios adapter. tsx strips types and runs
    // directly — the point is runtime behaviour, not type-checking (the tsc gate covers types).
    //
    // The mocked adapter returns the raw wire body as `response.data`; Axios then applies the
    // instance's configured transformResponse to it, exactly as it would for a real HTTP call.
    // Setting axios.defaults.adapter to a wrapper BEFORE constructing ApiClient ensures the
    // instance (which inherits axios.defaults at create time) picks up the mock. A call counter
    // guards against a false green where the mock is never actually invoked.
    private const string Harness = """
        import axios from 'axios';
        import { ApiClient as ApiClientOff } from './convertDatesOff/client/ApiClient';
        import { ApiClient as ApiClientOn } from './convertDatesOn/client/ApiClient';

        let currentAdapter: (config: any) => Promise<any> = () =>
          Promise.reject(new Error('no adapter configured'));
        let adapterCalls = 0;

        axios.defaults.adapter = ((config: any) => {
          adapterCalls += 1;
          return currentAdapter(config);
        }) as any;

        function respondWith(body: string, contentType: string): void {
          currentAdapter = (config: any) =>
            Promise.resolve({
              data: body,
              status: 200,
              statusText: 'OK',
              headers: { 'content-type': contentType },
              config,
              request: {},
            });
        }

        const failures: string[] = [];
        function check(name: string, cond: boolean, detail: string): void {
          if (!cond) {
            failures.push(name + ': ' + detail);
          }
        }

        async function runCases(label: string, client: any): Promise<void> {
          // Case 1: application/json + JSON string body -> ok, verbatim string (the bug).
          // Wire form is the JSON-encoded (quoted) string, i.e. `"token"`.
          respondWith(JSON.stringify('token'), 'application/json');
          const callsBefore = adapterCalls;
          const r1 = await client.request('get', '/token');
          check(label + ' adapter-invoked', adapterCalls > callsBefore, 'mocked adapter was never called');
          check(label + ' json-string-ok', r1.status === 'ok', 'expected ok, got ' + r1.status);
          check(label + ' json-string-data', r1.status === 'ok' && r1.data === 'token', 'expected data "token", got ' + JSON.stringify(r1.data));

          // Case 2: application/json + genuinely non-JSON body -> parseError (preserved).
          respondWith('<html>not json</html>', 'application/json');
          const r2 = await client.request('get', '/broken');
          check(label + ' broken-json-parseError', r2.status === 'parseError', 'expected parseError, got ' + r2.status);

          // Case 3: text/plain + string body -> ok, verbatim (no regression).
          respondWith('hello world', 'text/plain');
          const r3 = await client.request('get', '/text');
          check(label + ' text-plain-ok', r3.status === 'ok', 'expected ok, got ' + r3.status);
          check(label + ' text-plain-data', r3.status === 'ok' && r3.data === 'hello world', 'expected verbatim, got ' + JSON.stringify(r3.data));

          // Case 4: responseType 'text' must force a raw string even when the server
          // still sends application/json — the caller opted out of JSON parsing.
          respondWith('{"a":1}', 'application/json');
          const r4 = await client.request('get', '/raw', { responseType: 'text' });
          check(label + ' text-forced-ok', r4.status === 'ok', 'expected ok, got ' + r4.status);
          check(label + ' text-forced-raw', r4.status === 'ok' && r4.data === '{"a":1}', 'expected raw string, got ' + JSON.stringify(r4.data));
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