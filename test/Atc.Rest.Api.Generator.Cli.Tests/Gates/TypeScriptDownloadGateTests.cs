// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for file downloads in the generated TypeScript clients (Fetch and Axios): a PDF and an Excel download
/// resolve to <c>{ status: 'ok', data: Blob }</c> with the bytes intact, <c>getDownloadFileName(result.response)</c>
/// gives the <c>Content-Disposition</c> file name (<c>filename*</c> first, RFC 6266 / 5987), and a 404 as
/// <c>application/problem+json</c> is the <c>notFound</c> arm - never read as a Blob.
/// Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptDownloadGateTests
{
    [Fact]
    public void GeneratedClients_Downloads_AreBlobsWithTheirFileName()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(skipReason + " (running under CI where the download gate is required)");
            }

            Assert.Skip(skipReason);
        }

        Assert.True(OpenApiDocumentHelper.TryParseYaml(SpecYaml, "Downloads.yaml", out var doc));

        var workDir = Path.Combine(Path.GetTempPath(), "atc-download-gate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            foreach (var (folder, httpClient) in new[] { ("fetch", TypeScriptHttpClient.Fetch), ("axios", TypeScriptHttpClient.Axios) })
            {
                var output = Path.Combine(workDir, folder);
                Directory.CreateDirectory(output);
                TypeScriptClientGenerationService.Generate(doc!, output, new TypeScriptClientConfig
                {
                    HttpClient = httpClient,
                    HooksStyle = TypeScriptHooksStyle.None,
                    GenerateFileHeaders = true,
                    Scaffold = false,
                });
            }

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            var install = NodeToolchain.RunNpm("install --no-audit --no-fund --loglevel=error", workDir, TimeSpan.FromMinutes(5));
            Assert.True(install.ExitCode == 0, $"npm install failed (exit {install.ExitCode}):\n{install.Output}");

            var run = NodeToolchain.RunNpx("tsx harness.mts", workDir, TimeSpan.FromMinutes(3));
            Assert.True(run.ExitCode == 0, $"Download gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            try
            {
                Directory.Delete(workDir, recursive: true);
            }
            catch (IOException)
            {
                // Best effort: a locked temp folder must not fail the gate.
            }
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.3
        info:
          title: Downloads
          version: 1.0.0
        paths:
          /reports/{id}/pdf:
            get:
              operationId: getReportPdf
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: A PDF
                  content:
                    application/pdf:
                      schema:
                        type: string
                        format: binary
                '404':
                  description: Not found
          /reports/{id}/excel:
            get:
              operationId: getReportExcel
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: integer
              responses:
                '200':
                  description: An Excel workbook
                  content:
                    application/vnd.openxmlformats-officedocument.spreadsheetml.sheet:
                      schema:
                        type: string
                        format: binary
        """;

    private const string PackageJson = """
        {
          "name": "download-gate",
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

    private const string Harness = """
        import axios from 'axios';
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { ReportsClient as FetchReportsClient } from './fetch/client/ReportsClient';
        import { getDownloadFileName as fetchFileName } from './fetch/helpers';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { ReportsClient as AxiosReportsClient } from './axios/client/ReportsClient';
        import { getDownloadFileName as axiosFileName } from './axios/helpers';

        const pdf = new Uint8Array([0x25, 0x50, 0x44, 0x46]);
        const disposition = "attachment; filename=\"Rapport-_.pdf\"; filename*=UTF-8''Rapport-%C3%85.pdf";
        const failures: string[] = [];
        const check = (name: string, cond: boolean, detail: string) => { if (!cond) failures.push(name + ': ' + detail); };

        // Fetch: a mocked global fetch.
        (globalThis as any).fetch = async (input: any) => {
          const url = String(input);
          if (url.includes('/404/')) {
            return new Response(JSON.stringify({ title: 'Not Found', status: 404, detail: 'No report' }), { status: 404, headers: { 'content-type': 'application/problem+json' } });
          }

          const excel = url.endsWith('/excel');
          return new Response(new Blob([pdf]), {
            status: 200,
            headers: excel
              ? { 'content-type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }
              : { 'content-type': 'application/pdf', 'content-disposition': disposition },
          });
        };

        // Axios: a mocked adapter answering like the server (the Blob as data for a blob response).
        axios.defaults.adapter = (async (config: any) => {
          const url = String(config.url);
          if (url.includes('/404/')) {
            return { data: JSON.stringify({ title: 'Not Found', status: 404, detail: 'No report' }), status: 404, statusText: 'Not Found', headers: { 'content-type': 'application/problem+json' }, config, request: {} };
          }

          const excel = url.endsWith('/excel');
          return {
            data: new Blob([pdf]),
            status: 200,
            statusText: 'OK',
            headers: excel
              ? { 'content-type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }
              : { 'content-type': 'application/pdf', 'content-disposition': disposition },
            config,
            request: {},
          };
        }) as any;

        async function run(label: string, reports: any, fileName: (response: any) => string | undefined): Promise<void> {
          const r = await reports.getReportPdf(1);
          check(label + ' pdf-ok', r.status === 'ok', 'expected ok, got ' + r.status);
          check(label + ' pdf-blob', r.status === 'ok' && r.data instanceof Blob, 'expected a Blob');
          const bytes = r.status === 'ok' ? new Uint8Array(await r.data.arrayBuffer()) : new Uint8Array();
          check(label + ' pdf-bytes', Array.from(bytes).join(',') === '37,80,68,70', 'bytes ' + Array.from(bytes).join(','));
          check(label + ' pdf-file-name', fileName(r.response) === 'Rapport-Å.pdf', 'file name ' + fileName(r.response));

          const x = await reports.getReportExcel(1);
          check(label + ' excel-blob', x.status === 'ok' && x.data instanceof Blob, 'expected a Blob, got ' + x.status);
          check(label + ' excel-inline', fileName(x.response) === undefined, 'expected no file name, got ' + fileName(x.response));

          const e = await reports.getReportPdf(404);
          check(label + ' 404-arm', e.status === 'notFound', 'expected notFound, got ' + e.status);
          check(label + ' 404-detail', e.status === 'notFound' && e.error.message === 'No report', 'message ' + (e.error && e.error.message));
        }

        await run('fetch', new FetchReportsClient(new FetchApiClient('http://stub')), fetchFileName);
        await run('axios', new AxiosReportsClient(new AxiosApiClient('http://stub')), axiosFileName);

        if (failures.length > 0) {
          console.error(failures.join('\n'));
          process.exit(1);
        }

        console.log('download gate: all checks passed');
        """;
}