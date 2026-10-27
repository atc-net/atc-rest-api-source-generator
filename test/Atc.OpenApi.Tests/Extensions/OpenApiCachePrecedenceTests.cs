namespace Atc.OpenApi.Tests.Extensions;

/// <summary>
/// Every <c>x-cache-*</c> setting is read from the operation, else its path, else the document, else its default - one
/// setting at a time (mutation testing found the order and the defaults unpinned).
/// </summary>
public class OpenApiCachePrecedenceTests
{
    public static TheoryData<string, string, string, string, string, string> Settings
        => new()
        {
            // key, document, path, operation, the CacheConfiguration property, its default
            { "x-cache-policy", "doc", "path", "op", nameof(CacheConfiguration.Policy), "doc" },
            { "x-cache-type", "hybrid", "output", "hybrid", nameof(CacheConfiguration.Type), nameof(CacheType.Output) },
            { "x-cache-expiration-seconds", "10", "20", "30", nameof(CacheConfiguration.ExpirationSeconds), "300" },
            { "x-cache-vary-by-query", "[a]", "[b]", "[c]", nameof(CacheConfiguration.VaryByQuery), string.Empty },
            { "x-cache-vary-by-header", "[a]", "[b]", "[c]", nameof(CacheConfiguration.VaryByHeader), string.Empty },
            { "x-cache-vary-by-route", "[a]", "[b]", "[c]", nameof(CacheConfiguration.VaryByRoute), string.Empty },
            { "x-cache-mode", "in-memory", "distributed", "in-memory", nameof(CacheConfiguration.Mode), nameof(CacheMode.Hybrid) },
            { "x-cache-sliding-expiration-seconds", "10", "20", "30", nameof(CacheConfiguration.SlidingExpirationSeconds), string.Empty },
            { "x-cache-key-prefix", "doc", "path", "op", nameof(CacheConfiguration.KeyPrefix), string.Empty },
            { "x-cache-shared", "true", "false", "true", nameof(CacheConfiguration.Shared), "False" },
        };

    [Theory]
    [MemberData(nameof(Settings))]
    public void ExtractCacheConfiguration_ASetting_IsTheOperationsElseThePathsElseTheDocumentsElseItsDefault(
        string key,
        string documentValue,
        string pathValue,
        string operationValue,
        string property,
        string defaultValue)
    {
        string Read(
            string? atDocument,
            string? atPath,
            string? atOperation)
        {
            // A document policy makes a configuration exist; the setting under test is the only other difference
            var policy = key == "x-cache-policy" ? atDocument : "doc";
            var yaml = $$"""
                openapi: 3.0.0
                info: { title: Contoso, version: 1.0.0 }
                x-cache-policy: {{policy}}
                {{(atDocument is null || key == "x-cache-policy" ? string.Empty : $"{key}: {atDocument}")}}
                paths:
                  /things:
                    {{(atPath is null ? string.Empty : $"{key}: {atPath}")}}
                    get:
                      operationId: listThings
                      {{(atOperation is null ? string.Empty : $"{key}: {atOperation}")}}
                      responses:
                        '200': { description: OK }
                """;
            Assert.True(OpenApiDocumentHelper.TryParseYaml(yaml, "contoso.yaml", out var parsed), yaml);
            var pathItem = (OpenApiPathItem)parsed!.Paths.First().Value;
            var configuration = pathItem.Operations!.First().Value.ExtractCacheConfiguration(pathItem, parsed);
            var value = typeof(CacheConfiguration).GetProperty(property)!.GetValue(configuration);
            return value is IEnumerable<string> list
                ? string.Join(",", list)
                : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        Assert.Equal(defaultValue, Read(key == "x-cache-policy" ? "doc" : null, null, null));
        Assert.Equal(Normalize(documentValue), Read(documentValue, null, null));
        Assert.Equal(Normalize(pathValue), Read(documentValue, pathValue, null));
        Assert.Equal(Normalize(operationValue), Read(documentValue, pathValue, operationValue));

        string Normalize(string value)
            => property switch
            {
                nameof(CacheConfiguration.Type) => value == "hybrid" ? nameof(CacheType.Hybrid) : nameof(CacheType.Output),
                nameof(CacheConfiguration.Mode) => value == "in-memory" ? nameof(CacheMode.InMemory) : nameof(CacheMode.Distributed),
                nameof(CacheConfiguration.Shared) => value == "true" ? "True" : "False",
                _ => value.Trim('[', ']'),
            };
    }

    [Fact]
    public void ExtractCacheConfiguration_AnOperationWithCachingOff_IsDisabled()
    {
        const string yaml = """
            openapi: 3.0.0
            info: { title: Contoso, version: 1.0.0 }
            x-cache-policy: doc
            paths:
              /things:
                get:
                  operationId: listThings
                  x-cache-enabled: false
                  responses:
                    '200': { description: OK }
            """;
        Assert.True(OpenApiDocumentHelper.TryParseYaml(yaml, "contoso.yaml", out var parsed));
        var pathItem = (OpenApiPathItem)parsed!.Paths.First().Value;

        var configuration = pathItem.Operations!.First().Value.ExtractCacheConfiguration(pathItem, parsed);

        Assert.NotNull(configuration);
        Assert.False(configuration.Enabled);
    }
}