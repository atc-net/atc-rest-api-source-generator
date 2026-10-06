// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for <c>--convert-dates</c>: the generated Fetch client converts exactly the values the specification
/// declares as <c>format: date</c> / <c>date-time</c> - a model property, an array item - to <c>Date</c>, leaves a
/// plain string alone even when it looks like a date, and writes a <c>format: date</c> back as <c>yyyy-MM-dd</c>.
/// The types say <c>Date</c> for those values, so the runtime must agree with them. A <c>format: date</c> is a
/// calendar day: it is read and written as a local date, so the harness runs west (Los Angeles) and east
/// (Auckland) of UTC, where a UTC-based conversion is off by one day in one direction or the other.
/// Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptConvertDatesGateTests
{
    [Fact]
    public void GeneratedFetchClient_ConvertDates_ConvertsTheDeclaredDatesOnly()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        Assert.True(OpenApiDocumentHelper.TryParseYaml(SpecYaml, "Dates.yaml", out var document));

        var workDir = Path.Combine(Path.GetTempPath(), "atc-convert-dates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            var output = Path.Combine(workDir, "client");
            Directory.CreateDirectory(output);
            TypeScriptClientGenerationService.Generate(
                document!,
                output,
                new TypeScriptClientConfig
                {
                    HttpClient = TypeScriptHttpClient.Fetch,
                    HooksStyle = TypeScriptHooksStyle.None,
                    ConvertDates = true,
                    GenerateFileHeaders = true,
                    Scaffold = false,
                });

            var model = File.ReadAllText(Path.Combine(output, "models", "Event.ts"));
            Assert.Contains("readonly history?: Date[];", model, StringComparison.Ordinal);

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            NodeWorkspace.Link(workDir);

            foreach (var timeZone in new[] { "America/Los_Angeles", "Pacific/Auckland" })
            {
                var run = NodeToolchain.RunNpx(
                    "tsx harness.mts",
                    workDir,
                    TimeSpan.FromMinutes(3),
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["TZ"] = timeZone });
                Assert.True(run.ExitCode == 0, $"convert-dates gate failed under TZ={timeZone} (exit {run.ExitCode}):\n{run.Output}");
            }
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.1
        info:
          title: Dates
          version: 1.0.0
        paths:
          /events:
            get:
              operationId: getEvent
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Event'
            post:
              operationId: createEvent
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Event'
              responses:
                '204':
                  description: Created
        components:
          schemas:
            Event:
              type: object
              properties:
                day:
                  type: string
                  format: date
                at:
                  type: string
                  format: date-time
                note:
                  type: string
                history:
                  type: array
                  items:
                    type: string
                    format: date-time
        """;

    private const string PackageJson = """
        {
          "name": "convert-dates-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "devDependencies": {
            "tsx": "^4.19.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    private const string Harness = """
        import { ApiClient } from './client/client/ApiClient';
        import { EventsClient } from './client/client/EventsClient';

        let nextResponse: () => Response = () => new Response(null, { status: 500 });
        let sentBody: string | undefined;

        (globalThis as any).fetch = async (_url: string, init?: RequestInit) => {
          sentBody = typeof init?.body === 'string' ? init.body : undefined;
          return nextResponse();
        };

        const failures: string[] = [];
        function check(name: string, cond: boolean, detail: string): void {
          if (!cond) {
            failures.push(name + ': ' + detail);
          }
        }

        async function main(): Promise<void> {
          const events = new EventsClient(new ApiClient('http://localhost'));

          nextResponse = () => new Response(
            JSON.stringify({ day: '2024-02-29', at: '2024-02-29T10:15:00Z', note: '2024-02-29T10:15:00Z', history: ['2024-01-01T00:00:00Z'] }),
            { status: 200, headers: { 'Content-Type': 'application/json' } });
          const read = await events.getEvent();
          check('read-ok', read.status === 'ok', 'expected ok, got ' + read.status);
          const event: any = read.status === 'ok' ? read.data : {};
          check('date-is-Date', event.day instanceof Date, 'format: date should be a Date, got ' + JSON.stringify(event.day));
          // A format: date is a calendar day, read as the local day (not UTC midnight shifted into the zone)
          const localDay = (d: unknown, y: number, m: number, day: number) => d instanceof Date && d.getFullYear() === y && d.getMonth() === m && d.getDate() === day;
          check('date-value', localDay(event.day, 2024, 1, 29), 'format: date 2024-02-29 should read as local Feb 29 in TZ ' + process.env.TZ + ', got ' + String(event.day));
          check('date-time-is-Date', event.at instanceof Date, 'format: date-time should be a Date, got ' + JSON.stringify(event.at));
          check('date-time-value', event.at instanceof Date && event.at.toISOString() === '2024-02-29T10:15:00.000Z', 'got ' + String(event.at));
          check('plain-string-stays', event.note === '2024-02-29T10:15:00Z', 'a plain string must stay a string, got ' + String(event.note));
          check('array-item-is-Date', Array.isArray(event.history) && event.history[0] instanceof Date, 'array items should be Dates, got ' + JSON.stringify(event.history));

          nextResponse = () => new Response(
            JSON.stringify({ day: '2026-10-06' }),
            { status: 200, headers: { 'Content-Type': 'application/json' } });
          const october = await events.getEvent();
          const octoberDay: unknown = october.status === 'ok' ? (october.data as any).day : undefined;
          check('date-reads-as-local-day', localDay(octoberDay, 2026, 9, 6), 'format: date 2026-10-06 should read as local Oct 6 in TZ ' + process.env.TZ + ', got ' + String(octoberDay));

          // A local day is written as that day, whatever the zone's offset from UTC
          nextResponse = () => new Response(null, { status: 204 });
          await events.createEvent({ day: new Date(2024, 1, 29), at: new Date(Date.UTC(2024, 1, 29, 10, 15)), note: 'x' } as any);
          const sent = JSON.parse(sentBody ?? '{}');
          check('date-sent-as-date', sent.day === '2024-02-29', 'format: date should be sent as yyyy-MM-dd in TZ ' + process.env.TZ + ', got ' + JSON.stringify(sent.day));
          check('date-time-sent', sent.at === '2024-02-29T10:15:00.000Z', 'format: date-time should be sent as ISO, got ' + JSON.stringify(sent.at));

          await events.createEvent({ day: new Date(2026, 9, 6), at: new Date(Date.UTC(2026, 9, 6)), note: 'x' } as any);
          const sentOctober = JSON.parse(sentBody ?? '{}');
          check('local-day-sent-as-that-day', sentOctober.day === '2026-10-06', 'local Oct 6 should be sent as 2026-10-06 in TZ ' + process.env.TZ + ', got ' + JSON.stringify(sentOctober.day));

          if (failures.length > 0) {
            console.error('CONVERT-DATES FAILURES:\n' + failures.join('\n'));
            process.exit(1);
          }
          console.log('ALL CONVERT-DATES CHECKS PASSED');
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