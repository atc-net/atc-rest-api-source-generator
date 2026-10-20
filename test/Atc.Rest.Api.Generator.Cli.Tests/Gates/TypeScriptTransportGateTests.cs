namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for the transport a generated TypeScript client sends with: the Fetch client's <c>fetch</c> option and
/// the Axios client's <c>axiosInstance</c> option. A request, an error and a stream go through the caller's fetch
/// (called as a plain function, as the browser's <c>window.fetch</c> needs) or the caller's axios instance (its
/// defaults and interceptors apply, the client's own settings still do, and its <c>defaultHeaders</c> are sent). A
/// Node http server answers; the clients are compiled by tsc first. Self-skips without a Node toolchain, but fails
/// under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptTransportGateTests
{
    [Fact]
    public void GeneratedClients_SendWithTheCallersFetchOrAxiosInstance()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "Seam.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-transport-gate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            GenerateClient(doc, Path.Combine(workDir, "fetch"), TypeScriptHttpClient.Fetch);
            GenerateClient(doc, Path.Combine(workDir, "axios"), TypeScriptHttpClient.Axios);

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            NodeWorkspace.Link(workDir);

            var compile = NodeToolchain.RunNpx("tsc -p .", workDir, TimeSpan.FromMinutes(3));
            Assert.True(
                compile.ExitCode == 0,
                $"The generated clients with a transport option do not compile (exit {compile.ExitCode}):\n{compile.Output}");

            var run = NodeToolchain.RunNpx("tsx harness.mts", workDir, TimeSpan.FromMinutes(3));
            Assert.True(
                run.ExitCode == 0,
                $"Generated client transport gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.3
        info:
          title: Seam
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: listPets
              x-return-async-enumerable: true
              responses:
                '200':
                  description: The pets
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Pet'
          /pets/{id}:
            get:
              operationId: getPet
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: The pet
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
                '404':
                  description: Not found
        components:
          schemas:
            Pet:
              type: object
              required: [id, name]
              properties:
                id:
                  type: integer
                name:
                  type: string
        """;

    private const string PackageJson = """
        {
          "name": "transport-gate",
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
            "target": "ES2020",
            "lib": ["ES2020", "DOM", "DOM.Iterable"],
            "module": "ESNext",
            "moduleResolution": "bundler",
            "strict": true,
            "noUncheckedIndexedAccess": true,
            "noUnusedLocals": true,
            "noUnusedParameters": true,
            "esModuleInterop": true,
            "skipLibCheck": true,
            "types": ["node"],
            "noEmit": true
          },
          "include": ["fetch", "axios", "harness.mts"]
        }
        """;

    // The server answers /pets/1 with a pet, any other id with a 404, and /pets with a JSON array; it keeps the
    // headers of the last request
    private const string Harness = """
        import { createServer, type IncomingHttpHeaders } from 'node:http';
        import type { AddressInfo } from 'node:net';
        import axios from 'axios';
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { PetsClient as FetchPetsClient } from './fetch/client/PetsClient';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { PetsClient as AxiosPetsClient } from './axios/client/PetsClient';

        let seen: IncomingHttpHeaders = {};
        const server = createServer((req, res) => {
          seen = req.headers;
          if (req.url === '/pets') {
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end('[{"id":1,"name":"Rex"},{"id":2,"name":"Fido"}]');
            return;
          }

          if (req.url === '/pets/1') {
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end('{"id":1,"name":"Rex"}');
            return;
          }

          res.writeHead(404, { 'Content-Type': 'application/problem+json' });
          res.end('{"title":"Not Found","status":404,"detail":"No such pet"}');
        });

        const failures: string[] = [];
        function check(label: string, ok: boolean, detail: unknown): void {
          if (!ok) failures.push(label + ': ' + JSON.stringify(detail));
        }

        async function main(): Promise<void> {
          await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
          const baseUrl = 'http://127.0.0.1:' + (server.address() as AddressInfo).port;

          // Fetch: the caller's fetch sends every request, called as a plain function (no `this`, as window.fetch needs)
          const sent: string[] = [];
          const myFetch = function (this: unknown, input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
            if (this !== undefined) throw new Error('fetch was called as a method');
            sent.push(String(input));
            const headers = new Headers(init?.headers);
            headers.set('X-Transport', 'mine');
            return fetch(input, { ...init, headers });
          } as typeof fetch;
          const fetchPets = new FetchPetsClient(new FetchApiClient(baseUrl, { fetch: myFetch }));

          const fetchOk = await fetchPets.getPet(1);
          check('fetch ok', fetchOk.status === 'ok' && fetchOk.data.name === 'Rex', fetchOk.status);
          check('fetch header', seen['x-transport'] === 'mine', seen);
          const fetchMissing = await fetchPets.getPet(2);
          check('fetch notFound', fetchMissing.status === 'notFound' && fetchMissing.error.message === 'No such pet', fetchMissing.status);
          const fetchStreamed: string[] = [];
          for await (const pet of fetchPets.listPets()) fetchStreamed.push(pet.name);
          check('fetch stream', fetchStreamed.join() === 'Rex,Fido', fetchStreamed);
          check('fetch calls', sent.length === 3 && sent[0] === baseUrl + '/pets/1' && sent[2] === baseUrl + '/pets', sent);

          // Without the option the global fetch sends, as before
          const plain = await new FetchPetsClient(new FetchApiClient(baseUrl)).getPet(1);
          check('fetch default', plain.status === 'ok' && seen['x-transport'] === undefined, seen);

          // Axios: the caller's instance - its defaults and interceptors apply; the client's settings still do
          // (a 404 is a result arm, not a throw; a body is parsed), and its defaultHeaders are sent
          const instance = axios.create({ headers: { common: { 'X-Instance': 'mine' } }, validateStatus: (status) => status < 300 });
          instance.interceptors.request.use((config) => {
            config.headers.set('X-Instance-Interceptor', 'yes');
            return config;
          });
          const axiosPets = new AxiosPetsClient(new AxiosApiClient(baseUrl, {
            axiosInstance: instance,
            defaultHeaders: { 'X-Default': 'sent' },
            requestInterceptors: [(config) => {
              config.headers.set('X-Client-Interceptor', 'yes');
              return config;
            }],
          }));

          const axiosOk = await axiosPets.getPet(1);
          check('axios ok', axiosOk.status === 'ok' && axiosOk.data.name === 'Rex', axiosOk.status);
          check('axios headers', seen['x-instance'] === 'mine' && seen['x-instance-interceptor'] === 'yes' && seen['x-client-interceptor'] === 'yes' && seen['x-default'] === 'sent', seen);
          const axiosMissing = await axiosPets.getPet(2);
          check('axios notFound', axiosMissing.status === 'notFound' && axiosMissing.error.message === 'No such pet', axiosMissing.status);
          const axiosStreamed: string[] = [];
          for await (const pet of axiosPets.listPets()) axiosStreamed.push(pet.name);
          check('axios stream', axiosStreamed.join() === 'Rex,Fido', axiosStreamed);
          check('axios stream headers', seen['x-instance'] === 'mine' && seen['x-default'] === 'sent', seen);

          // The instance's own defaults are the caller's: the client changed none of them
          check('axios instance untouched', instance.defaults.baseURL === undefined && instance.defaults.headers.common['X-Default'] === undefined, instance.defaults.baseURL);

          // Node exits on its own once the sockets are closed: process.exit() while a fetch's keep-alive socket is still
          // closing aborts Node on Windows
          server.closeAllConnections();
          server.close();
          if (failures.length > 0) {
            console.error('TRANSPORT FAILURES:\n' + failures.join('\n'));
            process.exitCode = 1;
            return;
          }
          console.log('ALL TRANSPORT CHECKS PASSED');
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exitCode = 2;
          server.closeAllConnections();
          server.close();
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