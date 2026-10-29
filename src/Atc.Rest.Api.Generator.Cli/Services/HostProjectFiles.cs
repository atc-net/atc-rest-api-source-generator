namespace Atc.Rest.Api.Generator.Cli.Services;

/// <summary>
/// The files of a generated host project that are written once and then the user's: <c>Program.cs</c>,
/// <c>GlobalUsings.cs</c> and <c>Properties/launchSettings.json</c> - for every project structure.
/// </summary>
internal static class HostProjectFiles
{
    /// <summary>
    /// Writes <c>Properties/launchSettings.json</c> into the host project when it has none.
    /// </summary>
    public static void WriteLaunchSettings(
        string hostProjectPath,
        string projectName,
        HostUiType hostUi)
    {
        var launchSettingsPath = Path.Combine(hostProjectPath, "Properties", "launchSettings.json");
        if (File.Exists(launchSettingsPath))
        {
            return;
        }

        TrackedFile.CreateDirectory(Path.GetDirectoryName(launchSettingsPath)!);
        TrackedFile.WriteAllText(launchSettingsPath, LaunchSettingsContent(projectName, hostUi));
        AnsiConsole.MarkupLine("[green]✓[/] Created Properties/launchSettings.json");
    }

    /// <summary>
    /// <c>Properties/launchSettings.json</c>: <c>dotnet run</c> and the IDE start the host in Development (where the
    /// UI is mapped) and open the UI. The ports come from the project name, so they are the same on every machine and
    /// differ between projects.
    /// </summary>
    public static string LaunchSettingsContent(
        string projectName,
        HostUiType hostUi)
    {
        var hash = projectName.Aggregate(17, (h, c) => unchecked((h * 31) + c)) & 0x7fffffff;
        var httpPort = 5000 + (hash % 1000);
        var httpsPort = 7000 + (hash % 1000);
        var launch = hostUi == HostUiType.None
            ? string.Empty
            : $"\n      \"launchBrowser\": true,\n      \"launchUrl\": \"{UiPath(hostUi)}\",";
        return $$"""
            {
              "$schema": "https://json.schemastore.org/launchsettings.json",
              "profiles": {
                "http": {
                  "commandName": "Project",
                  "dotnetRunMessages": true,{{launch}}
                  "applicationUrl": "http://localhost:{{httpPort}}",
                  "environmentVariables": {
                    "ASPNETCORE_ENVIRONMENT": "Development"
                  }
                },
                "https": {
                  "commandName": "Project",
                  "dotnetRunMessages": true,{{launch}}
                  "applicationUrl": "https://localhost:{{httpsPort}};http://localhost:{{httpPort}}",
                  "environmentVariables": {
                    "ASPNETCORE_ENVIRONMENT": "Development"
                  }
                }
              }
            }
            """;
    }

    /// <summary>
    /// The host's Program.cs on the generated <c>Add/Use/Map{Api}Api()</c> and the handler project's registration.
    /// </summary>
    /// <param name="apiName">The API's name in the generated methods (from the server's namespace or <c>apiName</c>).</param>
    /// <param name="handlersMethodSuffix">The last term of the handler project's name: <c>AddApiHandlersFrom{suffix}()</c>.</param>
    /// <param name="hostUi">The API documentation UI.</param>
    /// <param name="hostUiMode">When the UI is mapped.</param>
    /// <returns>The Program.cs content.</returns>
    public static string ProgramContent(
        string apiName,
        string handlersMethodSuffix,
        HostUiType hostUi,
        HostUiModeType hostUiMode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();

        // Add OpenAPI services if UI is enabled
        if (hostUi != HostUiType.None)
        {
            sb.AppendLine("// Configure OpenAPI document generation");
            sb.AppendLine("builder.Services.AddOpenApi();");
            sb.AppendLine();
        }

        // The three generated calls: services, middleware, endpoints
        sb.AppendLine("// The generated API services: ProblemDetails for every error, the JSON options and the policies the");
        sb.AppendLine("// specification declares");
        sb.AppendLine(CultureInfo.InvariantCulture, $"builder.Services.Add{apiName}Api();");
        sb.AppendLine();
        sb.AppendLine("// Register handler implementations from the handler project");
        sb.AppendLine(CultureInfo.InvariantCulture, $"builder.Services.AddApiHandlersFrom{handlersMethodSuffix}();");
        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();
        sb.AppendLine("// The generated middleware, in the recommended order: error handling, status code pages, and what the");
        sb.AppendLine("// specification needs (authentication, rate limiting, ...)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"app.Use{apiName}Api();");
        sb.AppendLine();

        // Generate UI mapping based on configuration
        if (hostUi != HostUiType.None)
        {
            // The root redirects to the UI where the UI is mapped (outside Development it would be a 404)
            if (hostUiMode == HostUiModeType.DevelopmentOnly)
            {
                sb.AppendLine("if (app.Environment.IsDevelopment())");
                sb.AppendLine("{");
                sb.AppendLine(4, "app.MapOpenApi();");
                AppendUiMapping(sb, hostUi, 4);
                AppendRootRedirect(sb, hostUi, 4);
                sb.AppendLine("}");
            }
            else
            {
                sb.AppendLine("app.MapOpenApi();");
                AppendUiMapping(sb, hostUi, 0);
                AppendRootRedirect(sb, hostUi, 0);
            }

            sb.AppendLine();
        }

        sb.AppendLine("// Every endpoint of the API");
        sb.AppendLine(CultureInfo.InvariantCulture, $"app.Map{apiName}Api();");
        sb.AppendLine();
        sb.Append("await app.RunAsync();");

        return sb.ToString();
    }

    /// <summary>
    /// The host's global usings: the server's namespace (the handler registration is generated there) and its
    /// <c>Generated.Extensions</c> (<c>Add/Use/Map{Api}Api()</c>).
    /// </summary>
    /// <param name="serverNamespace">The server marker's namespace.</param>
    /// <param name="hostUi">The API documentation UI.</param>
    /// <returns>The GlobalUsings.cs content.</returns>
    public static string GlobalUsingsContent(
        string serverNamespace,
        HostUiType hostUi)
    {
        var sb = new StringBuilder();

        // The handler project's registration (AddApiHandlersFrom...) is generated in the server's namespace
        sb.Append("global using ");
        sb.Append(serverNamespace);
        sb.AppendLine(";");

        // The generated host extensions: Add{Api}Api(), Use{Api}Api(), Map{Api}Api()
        sb.Append("global using ");
        sb.Append(serverNamespace);
        sb.AppendLine(".Generated.Extensions;");

        // UI-specific namespaces
        if (hostUi == HostUiType.Scalar)
        {
            sb.AppendLine("global using Scalar.AspNetCore;");
        }

        return sb
            .ToString()
            .TrimEnd();
    }

    /// <summary>
    /// The UI's path, relative to the host: what the root redirects to and the browser opens.
    /// </summary>
    private static string UiPath(HostUiType hostUi)
        => hostUi == HostUiType.Swagger ? "swagger" : "scalar/v1";

    private static void AppendRootRedirect(
        StringBuilder sb,
        HostUiType hostUi,
        int indent)
    {
        sb.AppendLine();
        sb.AppendLine(indent, "// Redirect root to API documentation");
        sb.AppendLine(indent, "app");
        sb.AppendLine(indent + 4, $".MapGet(\"/\", () => Results.Redirect(\"/{UiPath(hostUi)}\"))");
        sb.AppendLine(indent + 4, ".ExcludeFromDescription();");
    }

    private static void AppendUiMapping(
        StringBuilder sb,
        HostUiType hostUi,
        int indent)
    {
        switch (hostUi)
        {
            case HostUiType.Scalar:
                sb.AppendLine(indent, "app.MapScalarApiReference();");
                break;
            case HostUiType.Swagger:
                sb.AppendLine(indent, "app.UseSwaggerUI(options =>");
                sb.AppendLine(indent, "{");
                sb.AppendLine(indent + 4, "options.SwaggerEndpoint(\"/openapi/v1.json\", \"v1\");");
                sb.AppendLine(indent, "});");
                break;
        }
    }
}