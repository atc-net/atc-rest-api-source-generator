namespace Atc.Rest.Api.SourceGenerator.Tests.Helpers;

[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
public class DomainGlobalUsingsHelperTests
{
    // ========== BuildRequiredUsings Tests ==========

    [Fact]
    public void BuildRequiredUsings_AlwaysIncludesSystemUsings()
    {
        // Arrange
        var emptyNamespaces = new HashSet<string>(StringComparer.Ordinal);
        var emptySegments = new List<string>();
        var doc = CreateEmptyOpenApiDocument();

        // Act
        var result = DomainGlobalUsingsHelper.BuildRequiredUsings(emptyNamespaces, "MyApp", emptySegments, doc);

        // Assert
        Assert.Contains("global using System;", result);
        Assert.Contains("global using System.Threading;", result);
        Assert.Contains("global using System.Threading.Tasks;", result);
    }

    [Fact]
    public void BuildRequiredUsings_WithDiscoveredNamespaces_AddsHandlerUsings()
    {
        // Arrange
        var discoveredNamespaces = new HashSet<string>(StringComparer.Ordinal)
        {
            "MyApp.Generated.Pets.Handlers",
        };

        var doc = CreateOpenApiDocumentWithPaths("/pets");

        // Act
        var result = DomainGlobalUsingsHelper.BuildRequiredUsings(discoveredNamespaces, "MyApp", [], doc);

        // Assert
        Assert.Contains("global using MyApp.Generated.Pets.Handlers;", result);
    }

    [Fact]
    public void BuildRequiredUsings_HandlerNamespace_AddsParametersAndResults()
    {
        // Arrange - Create a doc with /pets path that has operations with parameters
        var doc = CreateOpenApiDocumentWithParameters("/pets");

        var discoveredNamespaces = new HashSet<string>(StringComparer.Ordinal)
        {
            "MyApp.Generated.Pets.Handlers",
        };

        // Act
        var result = DomainGlobalUsingsHelper.BuildRequiredUsings(discoveredNamespaces, "MyApp", [], doc);

        // Assert - Should include handler namespace
        Assert.Contains("global using MyApp.Generated.Pets.Handlers;", result);

        // Should include results namespace (operations exist)
        Assert.Contains("global using MyApp.Generated.Pets.Results;", result);

        // Should include parameters namespace (parameters exist)
        Assert.Contains("global using MyApp.Generated.Pets.Parameters;", result);
    }

    [Fact]
    public void BuildRequiredUsings_NoDiscoveredNamespaces_FallsBackToPathSegments()
    {
        // Arrange
        var emptyNamespaces = new HashSet<string>(StringComparer.Ordinal);
        var segments = new List<string> { "Pets" };
        var doc = CreateOpenApiDocumentWithPaths("/pets");

        // Act
        var result = DomainGlobalUsingsHelper.BuildRequiredUsings(emptyNamespaces, "MyApp", segments, doc);

        // Assert - Should include system usings
        Assert.Contains("global using System;", result);

        // With path segments fallback, should produce segment-based usings via PathSegmentHelper
        // The exact usings depend on whether the segment has handlers/results/parameters
        Assert.True(result.Count > 3, "Should have more than just the 3 system usings");
    }

    [Fact]
    public void BuildRequiredUsings_EmptyDoc_ReturnsBaseUsingsOnly()
    {
        // Arrange
        var emptyNamespaces = new HashSet<string>(StringComparer.Ordinal);
        var emptySegments = new List<string>();
        var doc = CreateEmptyOpenApiDocument();

        // Act
        var result = DomainGlobalUsingsHelper.BuildRequiredUsings(emptyNamespaces, "MyApp", emptySegments, doc);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("global using System;", result);
        Assert.Contains("global using System.Threading;", result);
        Assert.Contains("global using System.Threading.Tasks;", result);
    }

    // ========== Helper Methods ==========

    private static OpenApiDocument CreateEmptyOpenApiDocument()
        => new()
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
            Paths = new OpenApiPaths(),
        };

    private static OpenApiDocument CreateOpenApiDocumentWithPaths(
        params string[] paths)
    {
        var doc = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
            Paths = new OpenApiPaths(),
        };

        foreach (var path in paths)
        {
            doc.Paths.Add(path, new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Get] = new OpenApiOperation
                    {
                        OperationId = "test",
                        Responses = new OpenApiResponses
                        {
                            ["200"] = new OpenApiResponse { Description = "OK" },
                        },
                    },
                },
            });
        }

        return doc;
    }

    private static OpenApiDocument CreateOpenApiDocumentWithParameters(
        params string[] paths)
    {
        var doc = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test API", Version = "1.0.0" },
            Paths = new OpenApiPaths(),
        };

        foreach (var path in paths)
        {
            doc.Paths.Add(path + "/{id}", new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Get] = new OpenApiOperation
                    {
                        OperationId = "getItem",
                        Parameters = [
                            new OpenApiParameter
                            {
                                Name = "id",
                                In = ParameterLocation.Path,
                                Required = true,
                                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                            },
                        ],
                        Responses = new OpenApiResponses
                        {
                            ["200"] = new OpenApiResponse { Description = "OK" },
                        },
                    },
                },
            });
        }

        return doc;
    }

    // ========== InjectLogger Tests ==========

    [Fact]
    public void BuildRequiredUsings_InjectLoggerTrue_IncludesLoggingUsing()
    {
        // Arrange
        var doc = CreateOpenApiDocumentWithPaths("Pets");

        // Act
        var result = DomainGlobalUsingsHelper.BuildRequiredUsings(
            [],
            "TestApi",
            ["Pets"],
            doc,
            injectLogger: true);

        // Assert
        Assert.Contains("global using Microsoft.Extensions.Logging;", result);
    }

    [Fact]
    public void BuildRequiredUsings_InjectLoggerFalse_ExcludesLoggingUsing()
    {
        // Arrange
        var doc = CreateOpenApiDocumentWithPaths("Pets");

        // Act
        var result = DomainGlobalUsingsHelper.BuildRequiredUsings(
            [],
            "TestApi",
            ["Pets"],
            doc,
            injectLogger: false);

        // Assert
        Assert.DoesNotContain("global using Microsoft.Extensions.Logging;", result);
    }

    // ========== InjectTracing Tests ==========
    [Fact]
    public void BuildRequiredUsings_InjectTracingTrue_IncludesDiagnosticsUsing()
    {
        // Arrange
        var doc = CreateOpenApiDocumentWithPaths("Pets");

        // Act
        var result = DomainGlobalUsingsHelper.BuildRequiredUsings(
            [],
            "TestApi",
            ["Pets"],
            doc,
            injectTracing: true);

        // Assert
        Assert.Contains("global using System.Diagnostics;", result);
    }

    [Fact]
    public void BuildRequiredUsings_InjectTracingFalse_ExcludesDiagnosticsUsing()
    {
        // Arrange
        var doc = CreateOpenApiDocumentWithPaths("Pets");

        // Act
        var result = DomainGlobalUsingsHelper.BuildRequiredUsings(
            [],
            "TestApi",
            ["Pets"],
            doc,
            injectTracing: false);

        // Assert
        Assert.DoesNotContain("global using System.Diagnostics;", result);
    }
}