// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// CI guard-rail for the generated TypeScript client: generates the StrictTscClient
/// scenario (Axios + React Query) and runs <c>tsc --noEmit</c> against real
/// <c>@tanstack/react-query@^5.81</c> + <c>axios</c> types under <c>noUnusedLocals</c>.
/// <para>
/// This is the regression gate — esbuild/Vite tolerate the five error
/// classes (onSuccess arity, duplicate x-continuation, unused AxiosResponse,
/// missing item-type import, unused enum import), so only a strict <c>tsc</c>
/// pass over generated output catches them. The test self-skips when a Node toolchain
/// is unavailable rather than passing silently.
/// </para>
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptClientTscGateTests
{
    [Fact]
    public void StrictTscClient_GeneratedOutput_CompilesUnderStrictTsc()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            // In CI the gate MUST run — a Node toolchain is provisioned via actions/setup-node.
            // Failing (instead of skipping) there keeps the guard tamper-evident: a missing
            // Node step or broken PATH surfaces as a red build rather than a silent green.
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the tsc gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var yamlPath = ResolveScenarioYaml();
        if (yamlPath == null)
        {
            Assert.Skip("StrictTscClient.yaml scenario spec could not be located.");
        }

        var yaml = File.ReadAllText(yamlPath);
        var doc = OpenApiDocumentHelper.TryParseYaml(yaml, "StrictTscClient.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var config = new TypeScriptClientConfig
        {
            HttpClient = TypeScriptHttpClient.Axios,
            HooksStyle = TypeScriptHooksStyle.ReactQuery,
            ConvertDates = true,
            GenerateFileHeaders = true,
            Scaffold = false,
        };

        var workDir = Path.Combine(Path.GetTempPath(), "atc-tsc-gate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            TypeScriptClientGenerationService.Generate(doc, workDir, config);

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "tsconfig.json"), TsConfig);

            var install = NodeToolchain.RunNpm(
                "install --no-audit --no-fund --loglevel=error",
                workDir,
                TimeSpan.FromMinutes(5));
            Assert.True(
                install.ExitCode == 0,
                $"npm install failed (exit {install.ExitCode}):\n{install.Output}");

            var tsc = NodeToolchain.RunNpx(
                "tsc --noEmit -p tsconfig.json",
                workDir,
                TimeSpan.FromMinutes(3));

            Assert.True(
                tsc.ExitCode == 0,
                $"tsc --noEmit reported errors in generated TypeScript client (exit {tsc.ExitCode}):\n{tsc.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private const string PackageJson = """
        {
          "name": "strict-tsc-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "dependencies": {
            "@tanstack/react-query": "^5.81.0",
            "axios": "^1.7.0",
            "react": "^18.3.0"
          },
          "devDependencies": {
            "@types/react": "^18.3.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    // strict + noUnusedLocals is what surfaces (TS6133/TS6196); the real
    // react-query types surface (TS2554). noUnusedParameters is deliberately off
    // to avoid flagging intentionally-unused generated callback params.
    private const string TsConfig = """
        {
          "compilerOptions": {
            "target": "ES2020",
            "lib": ["ES2020", "DOM"],
            "module": "ESNext",
            "moduleResolution": "bundler",
            "strict": true,
            "noUnusedLocals": true,
            "noUncheckedIndexedAccess": true,
            "esModuleInterop": true,
            "forceConsistentCasingInFileNames": true,
            "skipLibCheck": true,
            "isolatedModules": true,
            "noEmit": true
          },
          "include": ["**/*.ts"],
          "exclude": ["node_modules"]
        }
        """;

    private static bool IsContinuousIntegration()
        => string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    private static string? ResolveScenarioYaml()
    {
        // Copied next to the test assembly via the csproj <Content> include.
        var copied = Path.Combine(AppContext.BaseDirectory, "Scenarios", "StrictTscClient", "StrictTscClient.yaml");
        if (File.Exists(copied))
        {
            return copied;
        }

        // Fallback: walk up to the repo and read the source spec directly.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "test", "Scenarios", "StrictTscClient", "StrictTscClient.yaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

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