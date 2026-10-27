namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for the object parts of a multipart form in the generated clients (Fetch and Axios): a field whose
/// <c>encoding</c> names XML is a part of that media type with the model written as an XML body of it is (a list of one a
/// part per item), and a flat model whose <c>encoding</c> names <c>application/x-www-form-urlencoded</c> is a part of its
/// fields by the API's property names (a calendar day as one with convertDates). A Node http server receives the form;
/// the clients are compiled by tsc first. The generated server reads these parts (MultipartObjectPartRuntimeTests).
/// Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptMultipartPartsGateTests
{
    [Fact]
    public void GeneratedClients_SendXmlAndUrlEncodedParts_AsTheEncodingNames()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "Parts.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-multipart-parts-gate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            GenerateClient(doc, Path.Combine(workDir, "fetch"), TypeScriptHttpClient.Fetch, convertDates: true);
            GenerateClient(doc, Path.Combine(workDir, "axios"), TypeScriptHttpClient.Axios, convertDates: false);

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            NodeWorkspace.Link(workDir);

            var compile = NodeToolchain.RunNpx("tsc -p .", workDir, TimeSpan.FromMinutes(3));
            Assert.True(
                compile.ExitCode == 0,
                $"The generated clients with object parts do not compile (exit {compile.ExitCode}):\n{compile.Output}");

            var run = NodeToolchain.RunNpx("tsx harness.mts", workDir, TimeSpan.FromMinutes(3));
            Assert.True(
                run.ExitCode == 0,
                $"Generated client multipart parts gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.3
        info:
          title: Parts
          version: 1.0.0
        paths:
          /shipments:
            post:
              operationId: createShipment
              requestBody:
                required: true
                content:
                  multipart/form-data:
                    schema:
                      $ref: '#/components/schemas/ShipmentForm'
                    encoding:
                      address:
                        contentType: application/xml
                      stops:
                        contentType: application/xml
                      window:
                        contentType: application/x-www-form-urlencoded
                      windows:
                        contentType: application/x-www-form-urlencoded
              responses:
                '204':
                  description: Received
        components:
          schemas:
            Address:
              type: object
              required: [street]
              properties:
                street:
                  type: string
                post_code:
                  type: string
            Window:
              type: object
              required: [start]
              properties:
                start:
                  type: integer
                express:
                  type: boolean
                day:
                  type: string
                  format: date
                note_text:
                  type: string
            ShipmentForm:
              type: object
              required: [label, address]
              properties:
                label:
                  type: string
                address:
                  $ref: '#/components/schemas/Address'
                stops:
                  type: array
                  items:
                    $ref: '#/components/schemas/Address'
                window:
                  $ref: '#/components/schemas/Window'
                windows:
                  type: array
                  items:
                    $ref: '#/components/schemas/Window'
        """;

    private const string PackageJson = """
        {
          "name": "multipart-parts-gate",
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
            "lib": ["ES2020", "DOM"],
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

    // The server keeps the parts of the form it receives: their names, media types and texts
    private const string Harness = """
        import { createServer } from 'node:http';
        import type { AddressInfo } from 'node:net';
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { ShipmentsClient as FetchShipmentsClient } from './fetch/client/ShipmentsClient';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { ShipmentsClient as AxiosShipmentsClient } from './axios/client/ShipmentsClient';

        type Part = { name: string; type: string; text: string };
        let received: Part[] = [];

        const server = createServer((req, res) => {
          const chunks: Buffer[] = [];
          req.on('data', (chunk: Buffer) => chunks.push(chunk));
          req.on('end', () => {
            const boundary = /boundary=(?:"([^"]+)"|([^;]+))/.exec(req.headers['content-type'] ?? '');
            const delimiter = '--' + (boundary?.[1] ?? boundary?.[2] ?? '');
            received = Buffer.concat(chunks).toString('utf8')
              .split(delimiter)
              .slice(1, -1)
              .map((raw) => {
                const at = raw.indexOf('\r\n\r\n');
                const head = raw.slice(0, at);
                const text = raw.slice(at + 4).replace(/\r\n$/, '');
                return {
                  name: /name="([^"]+)"/.exec(head)?.[1] ?? '',
                  type: /content-type:\s*([^\r\n;]+)/i.exec(head)?.[1]?.trim() ?? '',
                  text,
                };
              });
            res.writeHead(204);
            res.end();
          });
        });

        const failures: string[] = [];
        function check(label: string, ok: boolean, detail: unknown): void {
          if (!ok) failures.push(label + ': ' + JSON.stringify(detail));
        }

        async function main(): Promise<void> {
          await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
          const baseUrl = 'http://127.0.0.1:' + (server.address() as AddressInfo).port;

          const address = { street: 'Main 1 & 2', postCode: '1000' };
          const stops = [{ street: 'A' }, { street: 'B' }];

          const fetchResult = await new FetchShipmentsClient(new FetchApiClient(baseUrl)).createShipment({
            label: 'parcel',
            address,
            stops,
            window: { start: 1, express: true, day: new Date(Date.UTC(2026, 9, 19)), noteText: 'back door & gate' },
            windows: [{ start: 2 }, { start: 3, express: false }],
          });
          check('fetch status', fetchResult.status === 'noContent', fetchResult.status);
          verify('fetch', received, '2026-10-19');

          const axiosResult = await new AxiosShipmentsClient(new AxiosApiClient(baseUrl)).createShipment({
            label: 'parcel',
            address,
            stops,
            window: { start: 1, express: true, day: '2026-10-19', noteText: 'back door & gate' },
            windows: [{ start: 2 }, { start: 3, express: false }],
          });
          check('axios status', axiosResult.status === 'noContent', axiosResult.status);
          verify('axios', received, '2026-10-19');

          server.close();
          if (failures.length > 0) {
            console.error('MULTIPART PART FAILURES:\n' + failures.join('\n'));
            process.exitCode = 1;
            return;
          }
          console.log('ALL MULTIPART PART CHECKS PASSED');
        }

        function verify(label: string, parts: Part[], day: string): void {
          const named = (name: string) => parts.filter((p) => p.name === name);
          check(label + ' label', named('label')[0]?.text === 'parcel', named('label'));

          const [addressPart] = named('address');
          check(label + ' address type', addressPart?.type === 'application/xml', addressPart);
          check(label + ' address xml', /<Address[ >]/.test(addressPart?.text ?? '') && (addressPart?.text ?? '').includes('<street>Main 1 &amp; 2</street>') && (addressPart?.text ?? '').includes('<post_code>1000</post_code>'), addressPart);

          const stopParts = named('stops');
          check(label + ' stops', stopParts.length === 2 && stopParts.every((p) => p.type === 'application/xml') && (stopParts[1]?.text ?? '').includes('<street>B</street>'), stopParts);

          const [windowPart] = named('window');
          check(label + ' window type', windowPart?.type === 'application/x-www-form-urlencoded', windowPart);
          const window = new URLSearchParams(windowPart?.text ?? '');
          check(label + ' window fields', window.get('start') === '1' && window.get('express') === 'true' && window.get('day') === day && window.get('note_text') === 'back door & gate' && !window.has('noteText'), windowPart?.text);

          const windowParts = named('windows').map((p) => p.text);
          check(label + ' windows', JSON.stringify(windowParts) === JSON.stringify(['start=2', 'start=3&express=false']), windowParts);
          console.log(label + ': checked');
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exit(2);
        });
        """;

    private static void GenerateClient(
        OpenApiDocument doc,
        string outputPath,
        TypeScriptHttpClient httpClient,
        bool convertDates)
    {
        Directory.CreateDirectory(outputPath);
        var config = new TypeScriptClientConfig
        {
            HttpClient = httpClient,
            HooksStyle = TypeScriptHooksStyle.None,
            ConvertDates = convertDates,
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