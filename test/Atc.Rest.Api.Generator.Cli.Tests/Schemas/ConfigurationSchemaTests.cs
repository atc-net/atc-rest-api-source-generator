namespace Atc.Rest.Api.Generator.Cli.Tests.Schemas;

/// <summary>
/// The JSON schemas in the repository's <c>schemas</c> folder are built from the configuration classes: a test fails
/// (and rewrites the file) when a setting changes without the schema, and every marker file of the repository and
/// every file the CLI writes is valid against its schema.
/// </summary>
public sealed class ConfigurationSchemaTests
{
    private static readonly string Root = FindRepositoryRoot();

    // JsonSchema.Net registers a schema by its $id once per process
    private static readonly ConcurrentDictionary<string, Lazy<JsonSchema>> BuiltSchemas = new(StringComparer.Ordinal);

    public static TheoryData<string> SchemaFiles()
        => new(Schemas.Keys.Order(StringComparer.Ordinal));

    public static TheoryData<string> MarkerFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in new[] { "sample", Path.Combine("test", "Scenarios") }
                     .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(Root, folder), ".atc-rest-api-*", SearchOption.AllDirectories))
                     .Where(file => SchemaFileOf(file) is not null &&
                                    !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                                    !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(Root, file).Replace('\\', '/'));
        }

        return data;
    }

    private static Dictionary<string, Func<string>> Schemas { get; } = new(StringComparer.Ordinal)
    {
        ["atc-rest-api-server.schema.json"] = () => MarkerSchema(
            typeof(ServerConfig),
            Constants.JsonSchema.Server,
            "atc-rest-api-server marker file",
            "The settings of the .atc-rest-api-server marker file: the server contracts (models, parameters, results, handler interfaces, endpoints) the source generator writes from the OpenAPI specification."),
        ["atc-rest-api-server-handlers.schema.json"] = () => MarkerSchema(
            typeof(ServerDomainConfig),
            Constants.JsonSchema.ServerHandlers,
            "atc-rest-api-server-handlers marker file",
            "The settings of the .atc-rest-api-server-handlers marker file: the handler implementations the source generator scaffolds."),
        ["atc-rest-api-client.schema.json"] = () => MarkerSchema(
            typeof(ClientConfig),
            Constants.JsonSchema.Client,
            "atc-rest-api-client marker file",
            "The settings of the .atc-rest-api-client marker file: the C# client the source generator writes from the OpenAPI specification."),
        ["atc-rest-api-gen-options.schema.json"] = () => ConfigurationSchemaBuilder.Build(
            typeof(ApiGeneratorOptions),
            Constants.JsonSchema.ApiGeneratorOptions,
            "atc-rest-api-gen options file",
            "The settings of ApiGeneratorOptions.json, read by the atc-rest-api-gen CLI (the build reads the marker files).",
            setting => setting.PropertyType.IsEnum
                ? Enum.GetNames(setting.PropertyType).Select(n => (JsonNode?)JsonValue.Create(n)).ToList()
                : ValuesOf(setting),
            (_, value) => JsonSerializer.SerializeToNode(value, value.GetType(), ApiOptionsHelper.JsonOptions)),
    };

    [Theory]
    [MemberData(nameof(SchemaFiles))]
    public void SchemaFile_IsBuiltFromTheConfigurationClasses(string schemaFile)
    {
        var path = Path.Combine(Root, "schemas", schemaFile);
        var expected = Schemas[schemaFile]();
        var actual = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : null;

        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, expected);
            Assert.Fail($"schemas/{schemaFile} was out of date and has been rewritten; review and commit it.");
        }
    }

    [Theory]
    [MemberData(nameof(MarkerFiles))]
    public void MarkerFile_IsValidAgainstItsSchema(string markerFile)
    {
        var path = Path.Combine(Root, markerFile);

        AssertValid(SchemaFileOf(path)!, File.ReadAllText(path));
    }

    [Fact]
    public void TheFilesTheCliWrites_AreValidAgainstTheirSchemas()
    {
        var options = JsonSerializerOptionsFactory.Create();

        AssertValid("atc-rest-api-server.schema.json", ConfigurationFileJsonHelper.Serialize(new ApiGeneratorOptions().ToServerConfig(), Constants.JsonSchema.Server, options));
        AssertValid("atc-rest-api-server-handlers.schema.json", ConfigurationFileJsonHelper.Serialize(new ApiGeneratorOptions().ToServerDomainConfig(), Constants.JsonSchema.ServerHandlers, options));
        AssertValid("atc-rest-api-client.schema.json", ConfigurationFileJsonHelper.Serialize(new ApiGeneratorOptions().ToClientConfig(), Constants.JsonSchema.Client, options));
        AssertValid("atc-rest-api-gen-options.schema.json", ConfigurationFileJsonHelper.Serialize(new ApiGeneratorOptions(), Constants.JsonSchema.ApiGeneratorOptions, ApiOptionsHelper.JsonOptions));
    }

    [Theory]
    [InlineData("atc-rest-api-server.schema.json", """{ "validateSpecificationStrategyy": "Strict" }""")]
    [InlineData("atc-rest-api-server.schema.json", """{ "validateSpecificationStrategy": "Minimal" }""")]
    [InlineData("atc-rest-api-server.schema.json", """{ "healthChecks": { "pathh": "/health" } }""")]
    [InlineData("atc-rest-api-client.schema.json", """{ "subFolderStrategy": "None" }""")]
    [InlineData("atc-rest-api-server-handlers.schema.json", """{ "stubImplementation": "error-500" }""")]
    [InlineData("atc-rest-api-gen-options.schema.json", """{ "general": { "validateSpecificationStrategy": "Minimal" } }""")]
    [InlineData("atc-rest-api-gen-options.schema.json", """{ "server": { "domain": { "handlerSufix": "Handler" } } }""")]
    public void Schema_RejectsAnUnknownSettingOrValue(
        string schemaFile,
        string json)
    {
        var result = Evaluate(schemaFile, json);

        Assert.False(result.IsValid, json);
    }

    private static string MarkerSchema(
        Type configType,
        string id,
        string title,
        string description)
        => ConfigurationSchemaBuilder.Build(
            configType,
            id,
            title,
            description,
            ValuesOf,
            WriteMarkerValue);

    private static List<JsonNode?>? ValuesOf(PropertyInfo setting)
    {
        if (MarkerConfigInspector.ValuesOf(setting) is not { } values)
        {
            return null;
        }

        var spellings = values.Spellings.Select(s => (JsonNode?)JsonValue.Create(s)).ToList();
        if (values.AcceptsBooleans)
        {
            spellings.Add(JsonValue.Create(true));
            spellings.Add(JsonValue.Create(false));
        }

        return spellings;
    }

    private static JsonNode? WriteMarkerValue(
        PropertyInfo setting,
        object value)
    {
        var options = new JsonSerializerOptions(JsonHelper.ConfigOptions);
        if (setting.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType is { } converterType)
        {
            options.Converters.Add((JsonConverter)Activator.CreateInstance(converterType)!);
        }

        return JsonSerializer.SerializeToNode(value, value.GetType(), options);
    }

    private static void AssertValid(
        string schemaFile,
        string json)
    {
        var result = Evaluate(schemaFile, json);

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                (result.Details ?? [])
                    .Where(d => d.Errors is { Count: > 0 })
                    .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))));
    }

    private static EvaluationResults Evaluate(
        string schemaFile,
        string json)
    {
        var schema = BuiltSchemas.GetOrAdd(schemaFile, file => new Lazy<JsonSchema>(() => JsonSchema.FromText(Schemas[file]()))).Value;
        using var document = JsonDocument.Parse(json);

        return schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
    }

    private static string? SchemaFileOf(string path)
        => Path.GetFileName(path) switch
        {
            Constants.MarkerFile.Server or Constants.MarkerFile.ServerJson => "atc-rest-api-server.schema.json",
            Constants.MarkerFile.ServerHandlers or Constants.MarkerFile.ServerHandlersJson => "atc-rest-api-server-handlers.schema.json",
            Constants.MarkerFile.Client or Constants.MarkerFile.ClientJson => "atc-rest-api-client.schema.json",
            _ => null,
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "test", "Scenarios")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("test/Scenarios not found above " + AppContext.BaseDirectory);
    }
}