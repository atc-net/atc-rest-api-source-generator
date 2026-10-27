// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for per-operation retry (<c>x-retry-*</c>) in the generated TypeScript clients (Fetch and
/// Axios): the generated segment clients are called against a mocked transport that counts the attempts.
/// <para>
/// Asserts, for both clients (the same matrix as the C# <c>RetryPolicyRuntimeTests</c>):
/// </para>
/// <list type="bullet">
///   <item><description>GET/PUT/DELETE retry with the document policy; POST/PATCH are sent once unless the operation opts in;</description></item>
///   <item><description>path- and operation-level <c>x-retry-enabled: false</c> and <c>x-retry-policy</c> are honored;</description></item>
///   <item><description>a 429 or 503 whose Retry-After is longer than the policy timeout goes back to the caller (another 5xx's Retry-After is not read);</description></item>
///   <item><description>"no retry" keeps the policy's timeout, and an operation timeout next to it replaces it;</description></item>
///   <item><description>a timeout rejects with a <c>TimeoutError</c> with both transports, the caller's abort with the transport's own error;</description></item>
///   <item><description>a hand-written <c>request('POST', ...)</c> is not retried, and <c>retryPolicy: false</c> switches retry off.</description></item>
/// </list>
/// Also type-checks the generated code with <c>tsc</c>. Self-skips without a Node toolchain, but fails under CI.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptClientRetryGateTests
{
    [Fact]
    public void GeneratedFetchClient_Retries_FollowTheSpecification()
        => RunGate(TypeScriptHttpClient.Fetch, FetchHarness, FetchPackageJson, Cases);

    [Fact]
    public void GeneratedAxiosClient_Retries_FollowTheSpecification()
        => RunGate(TypeScriptHttpClient.Axios, AxiosHarness, AxiosPackageJson, Cases);

    [Fact]
    public void GeneratedFetchClient_RetriedAttempt_ReleasesItsResponseBody()
        => RunGate(TypeScriptHttpClient.Fetch, BodyReleaseHarness, FetchPackageJson, string.Empty);

    private static void RunGate(
        TypeScriptHttpClient httpClient,
        string harness,
        string packageJson,
        string cases)
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the behaviour gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "Retries.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-ts-retry", Guid.NewGuid().ToString("N"));
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
                    HttpClient = httpClient,
                    HooksStyle = TypeScriptHooksStyle.None,
                    GenerateFileHeaders = true,
                    Scaffold = false,
                });

            File.WriteAllText(Path.Combine(workDir, "package.json"), packageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), harness + cases);

            NodeWorkspace.Link(workDir);

            var tsc = NodeToolchain.RunNpx(
                "tsc -p .",
                workDir,
                TimeSpan.FromMinutes(3));
            Assert.True(
                tsc.ExitCode == 0,
                $"Generated {httpClient} client does not type-check (exit {tsc.ExitCode}):\n{tsc.Output}");

            var run = NodeToolchain.RunNpx(
                "tsx harness.mts",
                workDir,
                TimeSpan.FromMinutes(3));

            Assert.True(
                run.ExitCode == 0,
                $"Generated {httpClient} client retry gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.3
        info:
          title: Retries
          version: 1.0.0
        x-retry-policy: standard
        x-retry-max-attempts: 2
        x-retry-delay-seconds: 0.01
        x-retry-backoff: constant
        x-retry-use-jitter: false
        x-retry-timeout-seconds: 1
        paths:
          /items/{mode}:
            parameters:
              - $ref: '#/components/parameters/Mode'
            get:
              operationId: getItem
              responses:
                '204':
                  description: No Content
            put:
              operationId: putItem
              responses:
                '204':
                  description: No Content
            delete:
              operationId: deleteItem
              responses:
                '204':
                  description: No Content
            post:
              operationId: postItem
              responses:
                '204':
                  description: No Content
            patch:
              operationId: patchItem
              responses:
                '204':
                  description: No Content
          /disabled/{mode}:
            x-retry-enabled: false
            parameters:
              - $ref: '#/components/parameters/Mode'
            get:
              operationId: getDisabled
              responses:
                '204':
                  description: No Content
          /quiet/{mode}:
            parameters:
              - $ref: '#/components/parameters/Mode'
            get:
              operationId: getQuiet
              x-retry-enabled: false
              x-retry-timeout-seconds: 0.3
              responses:
                '204':
                  description: No Content
          /optin/{mode}:
            parameters:
              - $ref: '#/components/parameters/Mode'
            post:
              operationId: postOptin
              x-retry-enabled: true
              responses:
                '204':
                  description: No Content
          /own/{mode}:
            parameters:
              - $ref: '#/components/parameters/Mode'
            post:
              operationId: postOwn
              x-retry-policy: own
              x-retry-max-attempts: 4
              responses:
                '204':
                  description: No Content
          /wide/{mode}:
            x-retry-policy: wide
            x-retry-max-attempts: 4
            parameters:
              - $ref: '#/components/parameters/Mode'
            get:
              operationId: getWide
              responses:
                '204':
                  description: No Content
            post:
              operationId: postWide
              responses:
                '204':
                  description: No Content
          /total/{mode}:
            x-retry-policy: total
            x-retry-max-attempts: 5
            x-retry-total-timeout-seconds: 1.5
            parameters:
              - $ref: '#/components/parameters/Mode'
            get:
              operationId: getTotal
              responses:
                '204':
                  description: No Content
        components:
          parameters:
            Mode:
              name: mode
              in: path
              required: true
              schema:
                type: string
        """;

    private const string FetchPackageJson = """
        {
          "name": "fetch-retry-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "devDependencies": {
            "tsx": "^4.19.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    private const string AxiosPackageJson = """
        {
          "name": "axios-retry-gate",
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
          },
          "include": ["generated/**/*.ts"]
        }
        """;

    // The mocked wire, shared by both transports: answers by the last path segment (a status code, "429-short" /
    // "503-short" with Retry-After: 0, "429-long" / "503-long" / "500-long" with Retry-After: 120, "slow" after two
    // seconds unless aborted). An aborted
    // "slow" rejects as the transport does (abortedBy): fetch with the signal's reason, axios with its CanceledError.
    private const string Wire = """
        let calls = 0;

        function reply(url: string, signal?: AbortSignal): Promise<{ status: number; headers: Record<string, string> }> {
          calls += 1;
          const mode = url.split('?')[0].split('/').pop();
          switch (mode) {
            case '503': return Promise.resolve({ status: 503, headers: {} });
            case '429-short': return Promise.resolve({ status: 429, headers: { 'retry-after': '0' } });
            case '429-long': return Promise.resolve({ status: 429, headers: { 'retry-after': '120' } });
            case '503-short': return Promise.resolve({ status: 503, headers: { 'retry-after': '0' } });
            case '503-long': return Promise.resolve({ status: 503, headers: { 'retry-after': '120' } });
            case '500-long': return Promise.resolve({ status: 500, headers: { 'retry-after': '120' } });
            case 'slow':
              return new Promise((resolve, reject) => {
                const timer = setTimeout(() => resolve({ status: 204, headers: {} }), 2000);
                signal?.addEventListener('abort', () => { clearTimeout(timer); reject(abortedBy(signal)); }, { once: true });
              });
            default: {
              // A status code sweep: '418' answers 418; a 429 comes with Retry-After: 0.
              const status = Number(mode);
              if (Number.isInteger(status) && status >= 400) {
                return Promise.resolve({ status, headers: status === 429 ? { 'retry-after': '0' } : {} });
              }

              return Promise.resolve({ status: 204, headers: {} });
            }
          }
        }

        """;

    private const string FetchHarness = """
        import { ApiClient } from './generated/client/ApiClient';
        import { ItemsClient } from './generated/client/ItemsClient';
        import { DisabledClient } from './generated/client/DisabledClient';
        import { QuietClient } from './generated/client/QuietClient';
        import { OptinClient } from './generated/client/OptinClient';
        import { OwnClient } from './generated/client/OwnClient';
        import { WideClient } from './generated/client/WideClient';
        import { TotalClient } from './generated/client/TotalClient';

        // fetch rejects an aborted request with the signal's reason (an AbortError, or the TimeoutError it was aborted with)
        const abortedBy = (signal: AbortSignal) => signal.reason;
        const callerAbortName = 'AbortError';

        """ + Wire + """
        (globalThis as any).fetch = async (url: string, init?: RequestInit) => {
          const r = await reply(String(url), init?.signal ?? undefined);
          return new Response(null, { status: r.status, headers: r.headers });
        };

        function createApi(options?: any): any { return new ApiClient('http://localhost', options); }

        """;

    // The adapter is installed before the ApiClient creates its axios instance.
    private const string AxiosHarness = """
        import axios from 'axios';
        import { ApiClient } from './generated/client/ApiClient';
        import { ItemsClient } from './generated/client/ItemsClient';
        import { DisabledClient } from './generated/client/DisabledClient';
        import { QuietClient } from './generated/client/QuietClient';
        import { OptinClient } from './generated/client/OptinClient';
        import { OwnClient } from './generated/client/OwnClient';
        import { WideClient } from './generated/client/WideClient';
        import { TotalClient } from './generated/client/TotalClient';

        // axios rejects an aborted request with its CanceledError, whatever the signal was aborted with
        const abortedBy = (_signal: AbortSignal) => new axios.CanceledError();
        const callerAbortName = 'CanceledError';

        """ + Wire + """
        axios.defaults.adapter = (async (config: any) => {
          const r = await reply(String(config.url), config.signal);
          return { data: '', status: r.status, statusText: '', headers: r.headers, config, request: {} };
        }) as any;

        function createApi(options?: any): any { return new ApiClient('http://localhost', options); }

        """;

    // A retried attempt's body is never read: it must be cancelled, or Undici keeps the connection busy until it is
    // garbage-collected. Each 503 body is a stream that never ends on its own and records its cancellation.
    private const string BodyReleaseHarness = """
        import { ApiClient } from './generated/client/ApiClient';
        import { ItemsClient } from './generated/client/ItemsClient';

        let calls = 0;
        const released: number[] = [];

        (globalThis as any).fetch = async () => {
          calls += 1;
          const attempt = calls;
          if (attempt >= 3) {
            return new Response(null, { status: 204 });
          }

          const body = new ReadableStream<Uint8Array>({
            start(controller) {
              controller.enqueue(new TextEncoder().encode('<html>Service Unavailable</html>'));
            },
            cancel() {
              released.push(attempt);
            },
          });
          return new Response(body, { status: 503, headers: { 'Content-Type': 'text/html' } });
        };

        async function main(): Promise<void> {
          const items = new ItemsClient(new ApiClient('http://localhost'));
          const result: any = await items.getItem('503');
          await new Promise((resolve) => setTimeout(resolve, 50));

          const failures: string[] = [];
          if (calls !== 3) {
            failures.push('expected 3 attempts, got ' + calls);
          }

          if (result?.status !== 'noContent') {
            failures.push('expected the last attempt (noContent), got ' + result?.status);
          }

          if (released.join(',') !== '1,2') {
            failures.push('expected the bodies of the retried attempts 1 and 2 to be cancelled, got [' + released.join(',') + ']');
          }

          if (failures.length > 0) {
            console.error('BODY RELEASE FAILURES:\n' + failures.join('\n'));
            process.exitCode = 1;
            return;
          }

          console.log('ALL BODY RELEASE CHECKS PASSED');
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exit(2);
        });
        """;

    private const string Cases = """
        const failures: string[] = [];

        // rejectsWith: the name of the error the call must reject with
        async function attempt(name: string, expected: number, call: () => Promise<unknown>, maxMs?: number, rejectsWith?: string): Promise<void> {
          calls = 0;
          const started = Date.now();
          let outcome = 'returned';
          let rejectedWith: string | undefined;
          try {
            const result: any = await call();
            outcome = 'returned ' + (result?.status ?? '');
          } catch (e: any) {
            rejectedWith = e?.name;
            outcome = 'threw ' + (e?.name ?? '') + ' ' + (e?.message ?? '');
          }

          const elapsed = Date.now() - started;
          if (calls !== expected) {
            failures.push(name + ': ' + calls + ' attempt(s), expected ' + expected + ' (' + outcome + ')');
          }

          if (rejectsWith !== undefined && rejectedWith !== rejectsWith) {
            failures.push(name + ': expected to reject with a ' + rejectsWith + ' (' + outcome + ')');
          }

          if (maxMs !== undefined && elapsed > maxMs) {
            failures.push(name + ': took ' + elapsed + ' ms, expected at most ' + maxMs + ' ms (' + outcome + ')');
          }
        }

        async function main(): Promise<void> {
          const api = createApi();
          const items = new ItemsClient(api);
          const disabled = new DisabledClient(api);
          const quiet = new QuietClient(api);
          const optin = new OptinClient(api);
          const own = new OwnClient(api);
          const wide = new WideClient(api);
          const total = new TotalClient(api);

          // standard = 2 retries (3 attempts); own / wide = 4 retries (5 attempts).
          await attempt('getItem 503', 3, () => items.getItem('503'));
          await attempt('getItem 429-short', 3, () => items.getItem('429-short'));
          await attempt('getItem 429-long', 1, () => items.getItem('429-long'));
          // A 503 is answered with Retry-After as a 429 is (RFC 9110, 10.2.3); another 5xx's Retry-After means nothing
          await attempt('getItem 503-short', 3, () => items.getItem('503-short'));
          await attempt('getItem 503-long', 1, () => items.getItem('503-long'));
          await attempt('getItem 500-long', 3, () => items.getItem('500-long'));
          await attempt('postOptin 503-long', 1, () => optin.postOptin('503-long'));
          await attempt('putItem 503', 3, () => items.putItem('503'));
          await attempt('putItem 429-short', 3, () => items.putItem('429-short'));
          await attempt('deleteItem 503', 3, () => items.deleteItem('503'));
          await attempt('postItem 503', 1, () => items.postItem('503'));
          await attempt('postItem 429-short', 1, () => items.postItem('429-short'));
          // A timeout rejects with a TimeoutError with either transport - never with what the caller's own abort rejects with
          await attempt('postItem slow (policy timeout 1 s still applies)', 1, () => items.postItem('slow'), 1800, 'TimeoutError');
          await attempt('patchItem 503', 1, () => items.patchItem('503'));
          await attempt('getDisabled 503', 1, () => disabled.getDisabled('503'));
          await attempt('getQuiet 503', 1, () => quiet.getQuiet('503'));
          await attempt('getQuiet slow (operation timeout 0.3 s)', 1, () => quiet.getQuiet('slow'), 900, 'TimeoutError');
          await attempt('getItem slow (every attempt times out)', 3, () => items.getItem('slow'), undefined, 'TimeoutError');
          const caller = new AbortController();
          setTimeout(() => caller.abort(), 100);
          await attempt('getItem slow (aborted by the caller)', 1, () => items.getItem('slow', caller.signal), undefined, callerAbortName);
          await attempt('postOptin 503', 3, () => optin.postOptin('503'));
          await attempt('postOptin 429-short', 3, () => optin.postOptin('429-short'));
          await attempt('postOptin 429-long', 1, () => optin.postOptin('429-long'));
          await attempt('postOwn 503', 5, () => own.postOwn('503'));
          await attempt('getWide 503', 5, () => wide.getWide('503'));
          await attempt('postWide 503', 1, () => wide.postWide('503'));
          // x-retry-total-timeout-seconds bounds the attempts and the waits between them: five retries are allowed, each
          // attempt times out after a second, and the total of 1.5 s ends the call in the second attempt
          await attempt('getTotal slow (total timeout 1.5 s)', 2, () => total.getTotal('slow'), 2200, 'TimeoutError');
          await attempt('getTotal 503', 6, () => total.getTotal('503'));

          // Hand-written requests follow the same method rule; retryPolicy: false switches retry off.
          await attempt('request POST 503', 1, () => api.request('POST', '/items/503'));
          await attempt('request GET 503', 3, () => api.request('GET', '/items/503'));
          await attempt('retryPolicy false getItem 503', 1, () => new ItemsClient(createApi({ retryPolicy: false })).getItem('503'));

          // Every error status code: a GET and an opted-in POST are retried only for the transient ones
          // (408, 429, every 5xx - the same set as the C# client), a plain POST never.
          const allErrorStatusCodes = [
            400, 401, 402, 403, 404, 405, 406, 407, 408, 409, 410, 411, 412, 413, 414, 415, 416, 417, 418,
            421, 422, 423, 424, 425, 426, 428, 429, 431, 451, 499,
            500, 501, 502, 503, 504, 505, 506, 507, 508, 510, 511, 599,
          ];
          for (const code of allErrorStatusCodes) {
            const transient = code === 408 || code === 429 || code >= 500;
            await attempt('getItem ' + code, transient ? 3 : 1, () => items.getItem(String(code)));
            await attempt('postOptin ' + code, transient ? 3 : 1, () => optin.postOptin(String(code)));
            await attempt('postItem ' + code, 1, () => items.postItem(String(code)));
          }

          if (failures.length > 0) {
            console.error('RETRY FAILURES:\n' + failures.join('\n'));
            process.exitCode = 1;
            return;
          }

          console.log('ALL RETRY CHECKS PASSED');
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