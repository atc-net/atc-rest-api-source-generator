// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for the credentials the generated TypeScript clients (Fetch and Axios) send, per operation, as the
/// specification's security requirements say:
/// <list type="bullet">
///   <item><description>an anonymous operation (<c>security: []</c>) sends no access token;</description></item>
///   <item><description>an operation that requires a bearer token (http bearer, OAuth2, OpenID Connect) sends <c>getAccessToken</c>'s token;</description></item>
///   <item><description>an operation that requires an API key sends <c>apiKeys[scheme]</c> in the header, query parameter or cookie the scheme declares, and no access token;</description></item>
///   <item><description>alternatives (bearer or API key) send the credentials the client has;</description></item>
///   <item><description>a stream follows the same rules;</description></item>
///   <item><description>a specification that declares no security at all sends the access token on every operation, as before.</description></item>
/// </list>
/// Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptSecurityGateTests
{
    [Fact]
    public void GeneratedClients_SendTheCredentialsTheOperationRequires()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        Assert.True(OpenApiDocumentHelper.TryParseYaml(SecuredSpecYaml, "Secured.yaml", out var secured));
        Assert.True(OpenApiDocumentHelper.TryParseYaml(OpenSpecYaml, "Open.yaml", out var open));

        var workDir = Path.Combine(Path.GetTempPath(), "atc-ts-security", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            GenerateClient(secured!, Path.Combine(workDir, "secured-fetch"), TypeScriptHttpClient.Fetch);
            GenerateClient(secured!, Path.Combine(workDir, "secured-axios"), TypeScriptHttpClient.Axios);
            GenerateClient(open!, Path.Combine(workDir, "open-fetch"), TypeScriptHttpClient.Fetch);
            GenerateClient(open!, Path.Combine(workDir, "open-axios"), TypeScriptHttpClient.Axios);

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            NodeWorkspace.Link(workDir);

            var tsc = NodeToolchain.RunNpx("tsc -p .", workDir, TimeSpan.FromMinutes(3));
            Assert.True(tsc.ExitCode == 0, $"The generated clients do not type-check (exit {tsc.ExitCode}):\n{tsc.Output}");

            var run = NodeToolchain.RunNpx("tsx harness.mts", workDir, TimeSpan.FromMinutes(3));
            Assert.True(run.ExitCode == 0, $"Generated client security gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            NodeWorkspace.Delete(workDir);
        }
    }

    private const string SecuredSpecYaml = """
        openapi: 3.0.3
        info:
          title: Secured
          version: 1.0.0
        security:
          - bearer: []
        paths:
          /public:
            get:
              operationId: getPublic
              security: []
              responses:
                '204':
                  description: No Content
          /private:
            get:
              operationId: getPrivate
              responses:
                '204':
                  description: No Content
          /oauth:
            get:
              operationId: getOAuth
              security:
                - oauth: [read]
              responses:
                '204':
                  description: No Content
          /keyed:
            get:
              operationId: getKeyed
              security:
                - headerKey: []
              responses:
                '204':
                  description: No Content
          /query-keyed:
            get:
              operationId: getQueryKeyed
              security:
                - queryKey: []
              parameters:
                - name: page
                  in: query
                  schema:
                    type: integer
              responses:
                '204':
                  description: No Content
          /cookie-keyed:
            get:
              operationId: getCookieKeyed
              security:
                - cookieKey: []
              responses:
                '204':
                  description: No Content
          /either:
            get:
              operationId: getEither
              security:
                - bearer: []
                - headerKey: []
              responses:
                '204':
                  description: No Content
          /feed:
            get:
              operationId: streamFeed
              x-return-async-enumerable: true
              security: []
              responses:
                '200':
                  description: OK
                  content:
                    application/jsonl:
                      schema:
                        $ref: '#/components/schemas/Tick'
        components:
          securitySchemes:
            bearer:
              type: http
              scheme: bearer
            oauth:
              type: oauth2
              flows:
                clientCredentials:
                  tokenUrl: https://login.contoso.com/token
                  scopes:
                    read: Read
            headerKey:
              type: apiKey
              in: header
              name: X-API-Key
            queryKey:
              type: apiKey
              in: query
              name: api_key
            cookieKey:
              type: apiKey
              in: cookie
              name: session
          schemas:
            Tick:
              type: object
              properties:
                n:
                  type: integer
        """;

    private const string OpenSpecYaml = """
        openapi: 3.0.3
        info:
          title: Open
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: getThings
              responses:
                '204':
                  description: No Content
        """;

    private const string PackageJson = """
        {
          "name": "ts-security-gate",
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

    private const string TsConfig = """
        {
          "compilerOptions": {
            "target": "ES2022",
            "lib": ["ES2022", "DOM"],
            "module": "ESNext",
            "moduleResolution": "bundler",
            "strict": true,
            "noUnusedLocals": true,
            "esModuleInterop": true,
            "skipLibCheck": true,
            "noEmit": true
          },
          "include": ["secured-fetch/**/*.ts", "secured-axios/**/*.ts", "open-fetch/**/*.ts", "open-axios/**/*.ts"]
        }
        """;

    // Both transports record what was sent: the Fetch client (and the Axios client's streams) through a mocked global
    // fetch, the Axios client through a mocked adapter.
    private const string Harness = """
        import axios from 'axios';
        import { ApiService as SecuredFetchService } from './secured-fetch/client/ApiService';
        import { ApiService as SecuredAxiosService } from './secured-axios/client/ApiService';
        import { ApiService as OpenFetchService } from './open-fetch/client/ApiService';
        import { ApiService as OpenAxiosService } from './open-axios/client/ApiService';

        type Sent = { url: string; header: (name: string) => string };
        let sent: Sent = { url: '', header: () => '' };

        (globalThis as any).fetch = async (input: any, init?: any) => {
          const request = input instanceof Request ? input : undefined;
          const headers = new Headers(request ? request.headers : init?.headers);
          sent = { url: String(request ? request.url : input), header: (name) => headers.get(name) ?? '' };
          return String(sent.url).includes('/feed')
            ? new Response('{"n":1}\n', { status: 200, headers: { 'Content-Type': 'application/jsonl' } })
            : new Response(null, { status: 204 });
        };

        axios.defaults.adapter = (async (config: any) => {
          const headers = config.headers ?? {};
          const query = new URLSearchParams(Object.entries(config.params ?? {}).filter(([, v]) => v !== undefined).map(([k, v]) => [k, String(v)])).toString();
          sent = { url: String(config.url) + (query ? '?' + query : ''), header: (name) => String((typeof headers.get === 'function' ? headers.get(name) : headers[name]) ?? '') };
          return { data: '', status: 204, statusText: 'No Content', headers: {}, config, request: {} };
        }) as any;

        const failures: string[] = [];
        function check(name: string, cond: boolean, detail: string): void {
          if (!cond) {
            failures.push(name + ': ' + detail);
          }
        }

        const options = {
          getAccessToken: () => 'token-1',
          apiKeys: { headerKey: 'header-key', queryKey: () => 'query-key', cookieKey: async () => 'cookie-key' },
        };

        async function run(label: string, api: any): Promise<void> {
          await api.public.getPublic();
          check(label + ' anonymous', sent.header('Authorization') === '', 'an anonymous operation sent Authorization ' + sent.header('Authorization'));

          await api.private.getPrivate();
          check(label + ' bearer', sent.header('Authorization') === 'Bearer token-1', 'expected the token, got ' + JSON.stringify(sent.header('Authorization')));

          await api.oauth.getOAuth();
          check(label + ' oauth2', sent.header('Authorization') === 'Bearer token-1', 'expected the token, got ' + JSON.stringify(sent.header('Authorization')));

          await api.keyed.getKeyed();
          check(label + ' header key', sent.header('X-API-Key') === 'header-key', 'expected the API key header, got ' + JSON.stringify(sent.header('X-API-Key')));
          check(label + ' header key without token', sent.header('Authorization') === '', 'an API key operation sent Authorization ' + sent.header('Authorization'));

          await api.queryKeyed.getQueryKeyed({ page: 2 });
          check(label + ' query key', sent.url.includes('api_key=query-key') && sent.url.includes('page=2'), 'expected api_key and page in the query, got ' + sent.url);

          await api.cookieKeyed.getCookieKeyed();
          check(label + ' cookie key', sent.header('Cookie') === 'session=cookie-key', 'expected the session cookie, got ' + JSON.stringify(sent.header('Cookie')));

          await api.either.getEither();
          check(label + ' alternatives', sent.header('Authorization') === 'Bearer token-1' && sent.header('X-API-Key') === 'header-key', 'expected the token and the API key, got ' + sent.header('Authorization') + ' / ' + sent.header('X-API-Key'));

          for await (const _ of api.feed.streamFeed()) {
            // drain
          }
          check(label + ' anonymous stream', sent.header('Authorization') === '', 'an anonymous stream sent Authorization ' + sent.header('Authorization'));
        }

        async function main(): Promise<void> {
          await run('fetch', new SecuredFetchService('http://localhost', options as any));
          await run('axios', new SecuredAxiosService('http://localhost', options as any));

          // A specification without security: the access token goes with every request, as before
          for (const [label, api] of [['fetch', new OpenFetchService('http://localhost', options as any)], ['axios', new OpenAxiosService('http://localhost', options as any)]] as const) {
            await (api as any).things.getThings();
            check(label + ' no security declared', sent.header('Authorization') === 'Bearer token-1', 'expected the token, got ' + JSON.stringify(sent.header('Authorization')));
          }

          if (failures.length > 0) {
            console.error('SECURITY FAILURES:\n' + failures.join('\n'));
            process.exitCode = 1;
            return;
          }

          console.log('ALL SECURITY CHECKS PASSED');
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exitCode = 2;
        });
        """;

    private static void GenerateClient(
        OpenApiDocument doc,
        string outputPath,
        TypeScriptHttpClient httpClient)
    {
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
    }

    private static bool IsContinuousIntegration()
        => string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
}