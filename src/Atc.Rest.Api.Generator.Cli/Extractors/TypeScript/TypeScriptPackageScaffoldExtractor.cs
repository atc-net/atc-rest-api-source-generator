namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates package.json and tsconfig.json scaffold files for the TypeScript output.
/// </summary>
public static class TypeScriptPackageScaffoldExtractor
{
    /// <summary>
    /// Generates a package.json string with conditional dependencies based on configuration.
    /// </summary>
    /// <param name="packageName">The npm package name.</param>
    /// <param name="packageVersion">The npm package version.</param>
    /// <param name="description">Optional package description (from OpenAPI info.description).</param>
    /// <param name="config">TypeScript client generation configuration.</param>
    /// <returns>The formatted package.json content.</returns>
    public static string GeneratePackageJson(
        string packageName,
        string packageVersion,
        string? description,
        TypeScriptClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var root = new JsonObject
        {
            ["name"] = packageName,
            ["version"] = packageVersion,
            ["private"] = true,
            ["type"] = "module",
        };

        if (!string.IsNullOrWhiteSpace(description))
        {
            root["description"] = description;
        }

        //// Dual ESM/CJS layout: ESM at dist/, CJS at dist/cjs/. `main` points at CJS
        //// for legacy `require()` consumers; `module` is the bundler hint for ESM;
        //// `types` is the canonical ESM declaration file (CJS types are not duplicated).
        root["main"] = "./dist/cjs/index.js";
        root["module"] = "./dist/index.js";
        root["types"] = "./dist/index.d.ts";

        // exports[*] order matters: TypeScript reads `types` first, ES module-aware
        // loaders read `import`, CJS-aware loaders read `require`, everything else
        // falls through to `default`. Keeping `default` last avoids it shadowing
        // more-specific conditions.
        root["exports"] = new JsonObject
        {
            ["."] = new JsonObject
            {
                ["types"] = "./dist/index.d.ts",
                ["import"] = "./dist/index.js",
                ["require"] = "./dist/cjs/index.js",
                ["default"] = "./dist/index.js",
            },
        };

        root["scripts"] = new JsonObject
        {
            ["build"] = "npm run build:esm && npm run build:cjs && node ./scripts/postbuild-cjs.mjs",
            ["build:esm"] = "tsc -p tsconfig.json",
            ["build:cjs"] = "tsc -p tsconfig.cjs.json",
            ["clean"] = "rm -rf dist",
        };

        // Conditional runtime dependencies
        var dependencies = new JsonObject();
        if (config.HttpClient == TypeScriptHttpClient.Axios)
        {
            dependencies["axios"] = "^1.7.0";
        }

        if (config.GenerateZodSchemas)
        {
            dependencies["zod"] = "^3.0.0";
        }

        if (dependencies.Count > 0)
        {
            root["dependencies"] = dependencies;
        }

        // Conditional peer dependencies (React Query hooks)
        if (config.HooksStyle == TypeScriptHooksStyle.ReactQuery)
        {
            root["peerDependencies"] = new JsonObject
            {
                // >= 5.81: mutation lifecycle callbacks gained a 4th parameter
                // (onMutateResult); the generated composed onSuccess forwards all four.
                ["@tanstack/react-query"] = "^5.81.0",
                ["react"] = "^18.0.0",
            };
        }

        // Dev dependencies
        var devDependencies = new JsonObject
        {
            ["typescript"] = "^5.0.0",
        };

        if (config.HooksStyle == TypeScriptHooksStyle.ReactQuery)
        {
            devDependencies["@types/react"] = "^18.0.0";
        }

        root["devDependencies"] = devDependencies;

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,

            // The default escaper mangles `&` (in `&&`) into `&`. The output is
            // JSON consumed by npm/node, not embedded HTML — relax the default so the
            // emitted scripts read naturally.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        return JsonSerializer.Serialize(root, options) + "\n";
    }

    /// <summary>
    /// Generates a tsconfig.json string with standard TypeScript library configuration.
    /// Uses JSONC (JSON-with-comments) — tsconfig.json natively supports comments, and
    /// `skipLibCheck` benefits from inline documentation since it's a meaningful trade-off.
    /// </summary>
    /// <returns>The formatted tsconfig.json content.</returns>
    public static string GenerateTsConfig()
        => """
           {
             "compilerOptions": {
               "target": "ES2020",
               "lib": ["ES2020", "DOM"],
               "module": "ESNext",
               "moduleResolution": "bundler",
               "strict": true,
               "noImplicitAny": true,
               "strictNullChecks": true,
               "noUncheckedIndexedAccess": true,
               "declaration": true,
               "declarationMap": true,
               "sourceMap": true,
               "outDir": "./dist",
               "rootDir": ".",
               // skipLibCheck speeds up compilation by skipping type-checking inside
               // @types/* packages we don't own. Generated code is still strictly
               // typed — disable this if you want to surface upstream type errors.
               "skipLibCheck": true,
               "esModuleInterop": true,
               "forceConsistentCasingInFileNames": true,
               "isolatedModules": true
             },
             "include": ["**/*.ts"],
             "exclude": ["dist", "node_modules"]
           }
           """ + "\n";

    /// <summary>
    /// Generates the CJS tsconfig that the dual-publish build invokes alongside the
    /// ESM config. Extends the ESM config so the strictness / lib / target choices
    /// stay in one place; only overrides what CJS needs: module format, output
    /// folder, and turning off the declaration / source-map duplication (those are
    /// already emitted from the ESM pass).
    /// </summary>
    /// <returns>The formatted tsconfig.cjs.json content.</returns>
    public static string GenerateTsConfigCjs()
        => """
           {
             "extends": "./tsconfig.json",
             "compilerOptions": {
               "module": "CommonJS",
               "moduleResolution": "node",
               "outDir": "./dist/cjs",
               "declaration": false,
               "declarationMap": false,
               "sourceMap": false
             }
           }
           """ + "\n";

    /// <summary>
    /// Generates the post-build script that drops a sub-package.json into dist/cjs/
    /// with <c>"type": "commonjs"</c>. The root package.json declares <c>"type":
    /// "module"</c>, so without this override Node would treat the .js files in
    /// dist/cjs/ as ESM and the dual-publish would not actually work.
    /// </summary>
    /// <returns>The formatted scripts/postbuild-cjs.mjs content.</returns>
    public static string GeneratePostbuildCjsScript()
        => """
           import { mkdirSync, writeFileSync } from 'node:fs';

           // Drop a sub-package.json so Node treats dist/cjs/*.js as CommonJS,
           // overriding the "type": "module" declaration in the root package.json.
           mkdirSync('dist/cjs', { recursive: true });
           writeFileSync(
             'dist/cjs/package.json',
             JSON.stringify({ type: 'commonjs' }, null, 2) + '\n',
           );
           """ + "\n";

    /// <summary>
    /// Generates a README.md describing the scaffolded package — quick-start usage,
    /// available clients/hooks, and the regen command. Replaces the silent gap where
    /// today's scaffolded package ships with no documentation at the root.
    /// </summary>
    /// <param name="packageName">The npm package name.</param>
    /// <param name="title">The OpenAPI info.title for the heading.</param>
    /// <param name="description">Optional package description (from OpenAPI info.description).</param>
    /// <param name="segmentNames">Per-segment client class names (e.g. "PetsClient").</param>
    /// <param name="config">TypeScript client generation configuration.</param>
    /// <returns>The formatted README.md content.</returns>
    public static string GenerateReadme(
        string packageName,
        string? title,
        string? description,
        IReadOnlyList<string> segmentNames,
        TypeScriptClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(segmentNames);
        ArgumentNullException.ThrowIfNull(config);

        var sb = new StringBuilder();
        var heading = !string.IsNullOrWhiteSpace(title) ? title!.Trim() : packageName;
        sb.Append("# ").AppendLine(heading);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine(description!.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("Generated by [atc-rest-api-source-generator](https://github.com/atc-net/atc-rest-api-source-generator).");
        sb.AppendLine("Do not edit files by hand — they will be overwritten on regen.");
        sb.AppendLine();

        sb.AppendLine("## Install");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.Append("npm install ").AppendLine(packageName);
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Quick start");
        sb.AppendLine();

        if (config.HooksStyle == TypeScriptHooksStyle.ReactQuery)
        {
            sb.AppendLine("Wrap your React tree in `ApiProvider` so hooks can resolve the configured client:");
            sb.AppendLine();
            sb.AppendLine("```tsx");
            sb.Append("import { ApiProvider } from '").Append(packageName).AppendLine("';");
            sb.AppendLine("import { QueryClient, QueryClientProvider } from '@tanstack/react-query';");
            sb.AppendLine();
            sb.AppendLine("const queryClient = new QueryClient();");
            sb.AppendLine();
            sb.AppendLine("export function Root() {");
            sb.AppendLine("  return (");
            sb.AppendLine("    <QueryClientProvider client={queryClient}>");
            sb.AppendLine("      <ApiProvider baseUrl=\"https://api.example.com\">");
            sb.AppendLine("        <App />");
            sb.AppendLine("      </ApiProvider>");
            sb.AppendLine("    </QueryClientProvider>");
            sb.AppendLine("  );");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Then use the generated hooks anywhere in the tree:");
            sb.AppendLine();
            sb.AppendLine("```tsx");
            sb.Append("import { useListPets } from '").Append(packageName).AppendLine("';");
            sb.AppendLine();
            sb.AppendLine("export function PetList() {");
            sb.AppendLine("  const { data, isLoading } = useListPets();");
            sb.AppendLine("  // ...");
            sb.AppendLine("}");
            sb.AppendLine("```");
        }
        else
        {
            sb.AppendLine("Instantiate the per-segment client classes against your `ApiClient`:");
            sb.AppendLine();
            sb.AppendLine("```ts");
            sb.Append("import { ApiClient");
            if (segmentNames.Count > 0)
            {
                sb.Append(", ").Append(segmentNames[0]);
            }

            sb.Append(" } from '").Append(packageName).AppendLine("';");
            sb.AppendLine();
            sb.AppendLine("const api = new ApiClient({ baseUrl: 'https://api.example.com' });");
            if (segmentNames.Count > 0)
            {
                var firstSegment = segmentNames[0];
                var camel = char.ToLowerInvariant(firstSegment[0]) + firstSegment[1..];
                sb.Append("const ").Append(camel).Append(" = new ").Append(firstSegment).AppendLine("(api);");
            }

            sb.AppendLine("```");
        }

        sb.AppendLine();

        sb.AppendLine("## Authentication");
        sb.AppendLine();
        sb.AppendLine("Attach a bearer token (or other credential) by passing headers through `ApiClient`:");
        sb.AppendLine();
        sb.AppendLine("```ts");
        sb.AppendLine("const api = new ApiClient({");
        sb.AppendLine("  baseUrl: 'https://api.example.com',");
        sb.AppendLine("  defaultHeaders: () => ({ Authorization: `Bearer ${getToken()}` }),");
        sb.AppendLine("});");
        sb.AppendLine("```");
        sb.AppendLine();

        if (segmentNames.Count > 0)
        {
            sb.AppendLine("## Available clients");
            sb.AppendLine();
            foreach (var segment in segmentNames)
            {
                sb.Append("- `").Append(segment).AppendLine("`");
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Building");
        sb.AppendLine();
        sb.AppendLine("`npm run build` emits dual ESM + CJS output:");
        sb.AppendLine();
        sb.AppendLine("- `dist/index.js` — ESM entry, consumed via `import` and the `module` field");
        sb.AppendLine("- `dist/cjs/index.js` — CJS entry, consumed via `require` (legacy Node)");
        sb.AppendLine("- `dist/index.d.ts` — TypeScript declarations (shared between both)");
        sb.AppendLine();
        sb.AppendLine("The post-build step drops `dist/cjs/package.json` with `\"type\": \"commonjs\"`");
        sb.AppendLine("so Node resolves the CJS files correctly even though the root package is ESM.");
        sb.AppendLine();

        sb.AppendLine("## Regenerating");
        sb.AppendLine();
        sb.AppendLine("Re-run the generator whenever the OpenAPI spec changes:");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("atc-rest-api-gen generate client-typescript -s <spec.yaml> -o <output>");
        sb.AppendLine("```");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Derives a kebab-case npm package name from an OpenAPI info.title.
    /// For example: "My Demo API - Full" → "my-demo-api-full".
    /// </summary>
    /// <param name="title">The OpenAPI info.title string.</param>
    /// <returns>A kebab-case package name.</returns>
    public static string DerivePackageName(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "generated-api-client";
        }

        var sb = new StringBuilder(title.Length);
        var lastWasHyphen = false;

        foreach (var ch in title)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                sb.Append('-');
                lastWasHyphen = true;
            }
        }

        var kebab = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(kebab) ? "generated-api-client" : kebab;
    }
}