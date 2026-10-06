// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for the renaming between the API's property names and the TypeScript models' (the default
/// camelCase naming strategy over a specification with snake_case and PascalCase properties): the generated Fetch
/// and Axios clients run under Node against a mocked transport that speaks the wire names.
/// <list type="bullet">
///   <item><description>a response is renamed to the models' names - nested models, arrays, a <c>oneOf</c> member -
///   while a dictionary's keys stay as they are;</description></item>
///   <item><description>a request body is sent with the API's names;</description></item>
///   <item><description>a declared error model is renamed in <c>error.body</c>, while the message and the ProblemDetails
///   fields come from the wire body;</description></item>
///   <item><description>the generated code type-checks under strict <c>tsc</c>.</description></item>
/// </list>
/// Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptWireNamesGateTests
{
    [Fact]
    public void GeneratedFetchClient_RenamesKeysBetweenTheWireAndTheModels()
        => RunGate(TypeScriptHttpClient.Fetch, FetchHarness, FetchPackageJson);

    [Fact]
    public void GeneratedAxiosClient_RenamesKeysBetweenTheWireAndTheModels()
        => RunGate(TypeScriptHttpClient.Axios, AxiosHarness, AxiosPackageJson);

    /// <summary>
    /// With runtime Zod validation the response is renamed first: the schemas speak the models' names, so the
    /// renamed person validates (a wire-named one would fail with <c>schemaMismatch</c>).
    /// </summary>
    [Fact]
    public void GeneratedFetchClient_WithZodValidation_ValidatesTheRenamedModel()
        => RunGate(TypeScriptHttpClient.Fetch, FetchHarness, FetchZodPackageJson, zodRuntimeValidate: true);

    private static void RunGate(
        TypeScriptHttpClient httpClient,
        string harness,
        string packageJson,
        bool zodRuntimeValidate = false)
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(skipReason + " (running under CI where the behaviour gate is required)");
            }

            Assert.Skip(skipReason);
        }

        Assert.True(OpenApiDocumentHelper.TryParseYaml(SpecYaml, "WireNames.yaml", out var document));

        var workDir = Path.Combine(Path.GetTempPath(), "atc-ts-wire-names", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            TypeScriptClientGenerationService.Generate(
                document!,
                Path.Combine(workDir, "generated"),
                new TypeScriptClientConfig
                {
                    HttpClient = httpClient,
                    HooksStyle = TypeScriptHooksStyle.None,
                    GenerateFileHeaders = true,
                    Scaffold = false,
                    GenerateZodSchemas = zodRuntimeValidate,
                    ZodRuntimeValidate = zodRuntimeValidate,
                });

            Assert.True(File.Exists(Path.Combine(workDir, "generated", "helpers", "wireNames.ts")), "helpers/wireNames.ts was not generated");

            File.WriteAllText(Path.Combine(workDir, "package.json"), packageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), harness + Cases);

            NodeWorkspace.Link(workDir);

            var typeCheck = NodeToolchain.RunNpx("tsc --noEmit -p tsconfig.json", workDir, TimeSpan.FromMinutes(3));
            Assert.True(typeCheck.ExitCode == 0, $"The generated {httpClient} client does not type-check:\n{typeCheck.Output}");

            var run = NodeToolchain.RunNpx("tsx harness.mts", workDir, TimeSpan.FromMinutes(3));
            Assert.True(run.ExitCode == 0, $"Generated {httpClient} client wire-names gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            try
            {
                NodeWorkspace.Delete(workDir);
            }
            catch (IOException)
            {
                // node_modules can stay locked for a moment.
            }
            catch (UnauthorizedAccessException)
            {
                // As above.
            }
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.3
        info:
          title: WireNames
          version: 1.0.0
        paths:
          /people:
            get:
              operationId: listPeople
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Person'
          /people/{person_id}:
            get:
              operationId: getPerson
              parameters:
                - name: person_id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Person'
                '404':
                  description: Not Found
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/ApiProblem'
            put:
              operationId: updatePerson
              parameters:
                - name: person_id
                  in: path
                  required: true
                  schema:
                    type: string
              requestBody:
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Person'
              responses:
                '204':
                  description: Updated
        components:
          schemas:
            Person:
              type: object
              properties:
                user_name:
                  type: string
                UserId:
                  type: string
                displayName:
                  type: string
                home_address:
                  $ref: '#/components/schemas/Address'
                tags:
                  type: array
                  items:
                    $ref: '#/components/schemas/Tag'
                labels:
                  type: object
                  additionalProperties:
                    $ref: '#/components/schemas/Tag'
                pet:
                  oneOf:
                    - $ref: '#/components/schemas/Cat'
                    - $ref: '#/components/schemas/Dog'
            Address:
              type: object
              properties:
                street_name:
                  type: string
            Tag:
              type: object
              properties:
                tag_name:
                  type: string
            Cat:
              type: object
              properties:
                cat_name:
                  type: string
                meow_volume:
                  type: integer
            Dog:
              type: object
              properties:
                dog_name:
                  type: string
                bark_volume:
                  type: integer
            ApiProblem:
              type: object
              properties:
                error_code:
                  type: string
                detail:
                  type: string
        """;

    private const string FetchPackageJson = """
        {
          "name": "fetch-wire-names-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "devDependencies": {
            "tsx": "^4.19.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    private const string FetchZodPackageJson = """
        {
          "name": "fetch-zod-wire-names-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "dependencies": {
            "zod": "^3.23.0"
          },
          "devDependencies": {
            "tsx": "^4.19.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    private const string AxiosPackageJson = """
        {
          "name": "axios-wire-names-gate",
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
            "target": "ES2022",
            "lib": ["ES2022", "DOM"],
            "module": "ESNext",
            "moduleResolution": "bundler",
            "strict": true,
            "noUnusedLocals": true,
            "noUncheckedIndexedAccess": true,
            "esModuleInterop": true,
            "skipLibCheck": true,
            "noEmit": true
          },
          "include": ["generated/**/*.ts"]
        }
        """;

    // Mocked global fetch: `respond` installs the next response; `sent` holds the last request body.
    private const string FetchHarness = """
        import { ApiClient } from './generated/client/ApiClient';
        import { PeopleClient } from './generated/client/PeopleClient';

        let nextResponse: () => Response = () => new Response(null, { status: 500 });
        let sent: unknown = undefined;

        (globalThis as any).fetch = async (_url: string, init?: RequestInit) => {
          sent = typeof init?.body === 'string' ? JSON.parse(init.body) : undefined;
          return nextResponse();
        };

        function respond(body: unknown, status: number, contentType = 'application/json'): void {
          nextResponse = () => new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': contentType } });
        }

        function lastSent(): unknown { return sent; }

        const people: any = new PeopleClient(new ApiClient('http://localhost'));

        """;

    // Mocked Axios adapter: the raw wire body goes through the instance's transformResponse, as for a real call.
    private const string AxiosHarness = """
        import axios from 'axios';
        import { ApiClient } from './generated/client/ApiClient';
        import { PeopleClient } from './generated/client/PeopleClient';

        let currentAdapter: (config: any) => Promise<any> = () => Promise.reject(new Error('no adapter configured'));
        let sent: unknown = undefined;

        axios.defaults.adapter = ((config: any) => {
          sent = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
          return currentAdapter(config);
        }) as any;

        function respond(body: unknown, status: number, contentType = 'application/json'): void {
          currentAdapter = (config: any) => Promise.resolve({
            data: body === undefined ? '' : JSON.stringify(body),
            status,
            statusText: '',
            headers: { 'content-type': contentType },
            config,
            request: {},
          });
        }

        function lastSent(): unknown { return sent; }

        const people: any = new PeopleClient(new ApiClient('http://localhost'));

        """;

    private const string Cases = """
        const failures: string[] = [];
        function check(name: string, cond: boolean, detail: string): void {
          if (!cond) {
            failures.push(name + ': ' + detail);
          }
        }

        const wirePerson = {
          user_name: 'anna',
          UserId: '7',
          displayName: 'Anna',
          home_address: { street_name: 'Main Street' },
          tags: [{ tag_name: 'a' }],
          labels: { Some_Key: { tag_name: 'b' } },
          pet: { cat_name: 'Tom', meow_volume: 3 },
        };

        async function main(): Promise<void> {
          // W1: a response is renamed to the models' names, a dictionary's keys stay.
          respond(wirePerson, 200);
          const r1 = await people.getPerson('7');
          check('W1 arm', r1.status === 'ok', 'expected ok, got ' + r1.status);
          const p = r1.data;
          check('W1 userName', p?.userName === 'anna', JSON.stringify(p));
          check('W1 userId', p?.userId === '7', JSON.stringify(p));
          check('W1 displayName', p?.displayName === 'Anna', JSON.stringify(p));
          check('W1 no wire keys', !('user_name' in p) && !('UserId' in p), JSON.stringify(p));
          check('W1 nested model', p?.homeAddress?.streetName === 'Main Street', JSON.stringify(p?.homeAddress));
          check('W1 array of models', p?.tags?.[0]?.tagName === 'a', JSON.stringify(p?.tags));
          check('W1 dictionary keys stay, values renamed', p?.labels?.Some_Key?.tagName === 'b', JSON.stringify(p?.labels));
          check('W1 oneOf member', p?.pet?.catName === 'Tom' && p?.pet?.meowVolume === 3, JSON.stringify(p?.pet));

          // W2: an array response.
          respond([wirePerson, { ...wirePerson, user_name: 'bo' }], 200);
          const r2 = await people.listPeople();
          check('W2 array', r2.data?.[1]?.userName === 'bo' && r2.data?.[0]?.homeAddress?.streetName === 'Main Street', JSON.stringify(r2.data));

          // W3: a request body is sent with the API's names.
          respond(undefined, 204);
          await people.updatePerson('7', {
            userName: 'anna',
            userId: '7',
            displayName: 'Anna',
            homeAddress: { streetName: 'Main Street' },
            tags: [{ tagName: 'a' }],
            labels: { Some_Key: { tagName: 'b' } },
            pet: { dogName: 'Rex', barkVolume: 9 },
          });
          const w: any = lastSent();
          check('W3 renamed keys', w?.user_name === 'anna' && w?.UserId === '7' && w?.displayName === 'Anna', JSON.stringify(w));
          check('W3 no model keys', !('userName' in (w ?? {})), JSON.stringify(w));
          check('W3 nested model', w?.home_address?.street_name === 'Main Street', JSON.stringify(w?.home_address));
          check('W3 array of models', w?.tags?.[0]?.tag_name === 'a', JSON.stringify(w?.tags));
          check('W3 dictionary keys stay', w?.labels?.Some_Key?.tag_name === 'b', JSON.stringify(w?.labels));
          check('W3 oneOf member', w?.pet?.dog_name === 'Rex' && w?.pet?.bark_volume === 9, JSON.stringify(w?.pet));

          // W4: a declared error model is renamed in error.body; the message and detail come from the wire body.
          respond({ error_code: 'E404', detail: 'Person 7 is gone.' }, 404);
          const r4 = await people.getPerson('7');
          check('W4 arm', r4.status === 'notFound', 'expected notFound, got ' + r4.status);
          check('W4 body', r4.error?.body?.errorCode === 'E404', JSON.stringify(r4.error?.body));
          check('W4 detail', r4.error?.detail === 'Person 7 is gone.', JSON.stringify(r4.error?.detail));
          check('W4 message', r4.error?.message === 'Person 7 is gone.', JSON.stringify(r4.error?.message));

          if (failures.length > 0) {
            console.error(failures.join('\n'));
            process.exit(1);
          }

          console.log('wire-names gate passed');
        }

        await main();
        """;
}