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

        root["main"] = "./dist/index.js";
        root["types"] = "./dist/index.d.ts";

        root["exports"] = new JsonObject
        {
            ["."] = new JsonObject
            {
                ["types"] = "./dist/index.d.ts",
                ["import"] = "./dist/index.js",
            },
        };

        root["scripts"] = new JsonObject
        {
            ["build"] = "tsc",
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
                ["@tanstack/react-query"] = "^5.0.0",
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