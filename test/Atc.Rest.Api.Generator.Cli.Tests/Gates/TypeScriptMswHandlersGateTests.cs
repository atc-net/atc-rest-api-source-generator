// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for the generated MSW handlers: installed with <c>msw/node</c>, they must answer every operation
/// with a body of the declared shape, and route each request to its own operation.
/// <para>
/// Asserts:
/// </para>
/// <list type="bullet">
///   <item><description>an array of strings (inline, or through a <c>$ref</c> alias) is mocked as an array of strings, not <c>[{}]</c>;</description></item>
///   <item><description>a top-level primitive is mocked as that primitive;</description></item>
///   <item><description>a path parameter whose name is no identifier (<c>{pet-id}</c>) still matches any value;</description></item>
///   <item><description>a literal route (<c>/pets/mine</c>) is not shadowed by a parameterized one declared before it (<c>/pets/{pet-id}</c>).</description></item>
/// </list>
/// Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptMswHandlersGateTests
{
    [Fact]
    public void GeneratedMswHandlers_AnswerWithTheDeclaredShapeAndTheirOwnRoute()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "MswHandlers.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-msw-handlers", Guid.NewGuid().ToString("N"));
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
                $"Generated MSW handlers gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            NodeWorkspace.Delete(workDir);
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.3
        info:
          title: MswHandlers
          version: 1.0.0
        servers:
          - url: http://localhost
        paths:
          /pets:
            get:
              operationId: listPetNames
              responses:
                '200':
                  description: The names.
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: string
          /pets/{pet-id}:
            get:
              operationId: getPet
              parameters:
                - name: pet-id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: The pet.
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
          /pets/mine:
            get:
              operationId: getMyPets
              responses:
                '200':
                  description: The caller's pets.
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Ownership'
          /tags:
            get:
              operationId: listTags
              responses:
                '200':
                  description: The tags.
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Tags'
          /tags/count:
            get:
              operationId: countTags
              responses:
                '200':
                  description: The number of tags.
                  content:
                    application/json:
                      schema:
                        type: integer
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
            Ownership:
              type: object
              required: [owner]
              properties:
                owner:
                  type: string
                petIds:
                  type: array
                  items:
                    type: integer
            Tags:
              type: array
              items:
                type: string
        """;

    private const string PackageJson = """
        {
          "name": "msw-handlers-gate",
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

    private const string Harness = """
        import { setupServer } from 'msw/node';
        import { handlers } from './generated/mocks';

        const failures: string[] = [];
        function check(name: string, cond: boolean, detail: string): void {
          if (!cond) {
            failures.push(name + ': ' + detail);
          }
        }

        async function get(path: string): Promise<{ status: number; body: unknown }> {
          try {
            const response = await fetch('http://localhost' + path);
            const text = await response.text();
            return { status: response.status, body: text.length > 0 ? JSON.parse(text) : undefined };
          } catch (e: any) {
            return { status: -1, body: 'not intercepted: ' + String(e?.message ?? e) };
          }
        }

        async function main(): Promise<void> {
          const server = setupServer(...handlers);
          server.listen({ onUnhandledRequest: 'error' });

          try {
            const names = await get('/pets');
            check('GET /pets (string[])', names.status === 200 && Array.isArray(names.body) && (names.body as unknown[]).length > 0 && (names.body as unknown[]).every((n) => typeof n === 'string'), 'expected an array of strings, got ' + names.status + ' ' + JSON.stringify(names.body));

            const pet = await get('/pets/42');
            check('GET /pets/{pet-id}', pet.status === 200 && typeof (pet.body as any)?.name === 'string', 'expected the Pet mock, got ' + pet.status + ' ' + JSON.stringify(pet.body));

            const mine = await get('/pets/mine');
            check('GET /pets/mine', mine.status === 200 && typeof (mine.body as any)?.owner === 'string', 'expected the Ownership mock (not the Pet one of /pets/{pet-id}), got ' + mine.status + ' ' + JSON.stringify(mine.body));
            check('GET /pets/mine petIds', Array.isArray((mine.body as any)?.petIds) && (mine.body as any).petIds.every((n: unknown) => typeof n === 'number'), 'expected petIds as numbers, got ' + JSON.stringify((mine.body as any)?.petIds));

            const tags = await get('/tags');
            check('GET /tags ($ref string[])', tags.status === 200 && Array.isArray(tags.body) && (tags.body as unknown[]).length > 0 && (tags.body as unknown[]).every((t) => typeof t === 'string'), 'expected an array of strings, got ' + tags.status + ' ' + JSON.stringify(tags.body));

            const count = await get('/tags/count');
            check('GET /tags/count (integer)', count.status === 200 && typeof count.body === 'number', 'expected a number, got ' + count.status + ' ' + JSON.stringify(count.body));
          } finally {
            server.close();
          }

          if (failures.length > 0) {
            console.error('MSW HANDLER FAILURES:\n' + failures.join('\n'));
            process.exitCode = 1;
            return;
          }

          console.log('ALL MSW HANDLER CHECKS PASSED');
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exit(2);
        });
        """;

    private static bool IsContinuousIntegration()
        => string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
}