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

    // ========== EnsureUpdated Tests ==========

    [Fact]
    public void EnsureUpdated_RemovesStaleGeneratedUsingsFromOtherRoot()
    {
        // Arrange — file has stale `OldRoot.Generated.*` entries from a previous (buggy)
        // generator run, plus a hand-written using that must be preserved.
        var tempDir = Path.Combine(Path.GetTempPath(), "EnsureUpdatedStale_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var globalUsingsPath = Path.Combine(tempDir, "GlobalUsings.cs");
            const string initialContent = "global using System;\n" +
                                          "global using OldRoot.Generated.Pets.Handlers;\n" +
                                          "global using OldRoot.Generated.Pets.Results;\n" +
                                          "global using NewRoot.Generated.Pets.Handlers;\n" +
                                          "global using MyCompany.HandWritten.Helpers;\n";

            File.WriteAllText(globalUsingsPath, initialContent);

            var discoveredNamespaces = new HashSet<string>(StringComparer.Ordinal)
            {
                "NewRoot.Generated.Pets.Handlers",
            };
            var doc = CreateOpenApiDocumentWithPaths("/pets");
            var config = new ServerDomainConfig { Namespace = "MyApp.Api.Domain" };

            // Act
            DomainGlobalUsingsHelper.EnsureUpdated(
                tempDir,
                discoveredNamespaces,
                "NewRoot",
                ["Pets"],
                doc,
                config);

            // Assert — stale OldRoot.Generated.* entries are pruned; hand-written usings preserved.
            var rewritten = File.ReadAllText(globalUsingsPath);
            Assert.DoesNotContain("OldRoot.Generated.Pets.Handlers", rewritten, StringComparison.Ordinal);
            Assert.DoesNotContain("OldRoot.Generated.Pets.Results", rewritten, StringComparison.Ordinal);
            Assert.Contains("NewRoot.Generated.Pets.Handlers", rewritten, StringComparison.Ordinal);
            Assert.Contains("MyCompany.HandWritten.Helpers", rewritten, StringComparison.Ordinal);
            Assert.Contains("global using System;", rewritten, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureUpdated_PreservesThirdPartyGeneratedNamespaces()
    {
        // Arrange — file has hand-written global usings that point into third-party API
        // client packages whose namespaces contain `.Generated.` (e.g. NSwag/Refit-style
        // generated clients). These differ from the project's `rootNamespace` but are not
        // stale generator output and must be preserved. Regression for the case where
        // `KL.IoT.Insights.ApiClient.Generated.Endpoints.Accounts` was being pruned even
        // though it never originated from this generator.
        var tempDir = Path.Combine(Path.GetTempPath(), "EnsureUpdatedThirdParty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var globalUsingsPath = Path.Combine(tempDir, "GlobalUsings.cs");
            const string initialContent = "global using System;\n" +
                                          "global using ThirdParty.Client.Generated.Endpoints.Accounts;\n" +
                                          "global using ThirdParty.Client.Generated.Endpoints.Accounts.Interfaces;\n" +
                                          "global using ThirdParty.Client.Generated.Devices.Endpoints.Interfaces;\n" +
                                          "global using ThirdParty.Client.Generated;\n" +

                                          // Deeper third-party namespaces whose terminal segment matches one of the
                                          // suffixes this helper owns. Without the single-segment constraint between
                                          // `.Generated.` and the suffix, these would be incorrectly pruned.
                                          "global using Vendor.Pkg.Generated.Foo.Bar.Handlers;\n" +
                                          "global using Vendor.Pkg.Generated.Foo.Bar.Models;\n";
            File.WriteAllText(globalUsingsPath, initialContent);

            var doc = CreateOpenApiDocumentWithPaths("/pets");
            var config = new ServerDomainConfig { Namespace = "MyApp.Api.Domain" };

            // Act — rootNamespace differs from `ThirdParty.Client`, but none of these
            // entries match the {root}.Generated.{Segment}.{Handlers|Parameters|Results|Models}
            // shape the generator owns, so they must survive.
            DomainGlobalUsingsHelper.EnsureUpdated(
                tempDir,
                [],
                "MyApp",
                ["Pets"],
                doc,
                config);

            // Assert — every third-party using preserved
            var rewritten = File.ReadAllText(globalUsingsPath);
            Assert.Contains("ThirdParty.Client.Generated.Endpoints.Accounts;", rewritten, StringComparison.Ordinal);
            Assert.Contains("ThirdParty.Client.Generated.Endpoints.Accounts.Interfaces;", rewritten, StringComparison.Ordinal);
            Assert.Contains("ThirdParty.Client.Generated.Devices.Endpoints.Interfaces;", rewritten, StringComparison.Ordinal);
            Assert.Contains("ThirdParty.Client.Generated;", rewritten, StringComparison.Ordinal);
            Assert.Contains("Vendor.Pkg.Generated.Foo.Bar.Handlers;", rewritten, StringComparison.Ordinal);
            Assert.Contains("Vendor.Pkg.Generated.Foo.Bar.Models;", rewritten, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureUpdated_PreservesAliasAndStaticUsings()
    {
        // Arrange — alias and static usings reference {rootNamespace}.Generated.* but
        // are intentional user constructs and must NEVER be pruned.
        var tempDir = Path.Combine(Path.GetTempPath(), "EnsureUpdatedAlias_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var globalUsingsPath = Path.Combine(tempDir, "GlobalUsings.cs");
            var initialContent =
                "global using System;\n" +
                "global using TaskModel = MultipartDemo.Generated.Tasks.Models.Task;\n" +
                "global using static MultipartDemo.Generated.Constants;\n";
            File.WriteAllText(globalUsingsPath, initialContent);

            var doc = CreateOpenApiDocumentWithPaths("/pets");
            var config = new ServerDomainConfig { Namespace = "MultipartDemo.Api.Domain" };

            // Act — rootNamespace differs from the alias's RHS root, but aliases must survive.
            DomainGlobalUsingsHelper.EnsureUpdated(
                tempDir,
                [],
                "OtherRoot",
                ["Pets"],
                doc,
                config);

            // Assert — both alias and static usings preserved
            var rewritten = File.ReadAllText(globalUsingsPath);
            Assert.Contains("TaskModel = MultipartDemo.Generated.Tasks.Models.Task", rewritten, StringComparison.Ordinal);
            Assert.Contains("static MultipartDemo.Generated.Constants", rewritten, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureUpdated_PreservesThirdPartyGeneratedNamespace_WithSingleSegmentMiddle()
    {
        // Arrange — third-party API client (itself produced by `atc-rest-api-source-generator`
        // and consumed via NuGet) emits namespaces shaped identically to this generator's
        // output: `{ThirdPartyRoot}.Generated.{SingleSegment}.{Models|Handlers|Parameters|Results}`.
        // Regression for `My.Api.Insights.Api.Client.Generated.Monta.Models` — the
        // single-segment middle (`Monta`) does NOT appear in this project's spec, so it
        // cannot be stale generator output for this project.
        var tempDir = Path.Combine(Path.GetTempPath(), "EnsureUpdatedThirdPartySingleSeg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var globalUsingsPath = Path.Combine(tempDir, "GlobalUsings.cs");
            var initialContent =
                "global using System;\n" +
                "global using My.Api.Insights.Api.Client.Generated.Monta.Client;\n" +
                "global using My.Api.Insights.Api.Client.Generated.Monta.Endpoints;\n" +
                "global using My.Api.Insights.Api.Client.Generated.Monta.Endpoints.Interfaces;\n" +
                "global using My.Api.Insights.Api.Client.Generated.Monta.Models;\n";
            File.WriteAllText(globalUsingsPath, initialContent);

            var doc = CreateOpenApiDocumentWithPaths("/accounts", "/command");
            var config = new ServerDomainConfig { Namespace = "My.Api.D365.Api.Domain" };

            // Act — rootNamespace differs from `My.Api.Insights.Api.Client`, but `Monta`
            // is not a path segment in this project's spec (Accounts/Command), so the
            // entry must not be pruned regardless of suffix.
            DomainGlobalUsingsHelper.EnsureUpdated(
                tempDir,
                [],
                "My.Api.D365.Api.Contracts",
                ["Accounts", "Command"],
                doc,
                config);

            // Assert — every Insights API client using preserved
            var rewritten = File.ReadAllText(globalUsingsPath);
            Assert.Contains("My.Api.Insights.Api.Client.Generated.Monta.Client;", rewritten, StringComparison.Ordinal);
            Assert.Contains("My.Api.Insights.Api.Client.Generated.Monta.Endpoints;", rewritten, StringComparison.Ordinal);
            Assert.Contains("My.Api.Insights.Api.Client.Generated.Monta.Endpoints.Interfaces;", rewritten, StringComparison.Ordinal);
            Assert.Contains("My.Api.Insights.Api.Client.Generated.Monta.Models;", rewritten, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureUpdated_PreservesEntry_WhenMiddleSegmentNotInCurrentPathSegments()
    {
        // Arrange — entries shaped like `{OtherRoot}.Generated.{X}.{Suffix}` where `{X}`
        // doesn't exist in the current OpenAPI spec. These cannot be stale output from
        // this project's generator — the spec doesn't have `{X}` to begin with — so they
        // must survive even though the root differs from `rootNamespace`.
        var tempDir = Path.Combine(Path.GetTempPath(), "EnsureUpdatedAlienSegment_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var globalUsingsPath = Path.Combine(tempDir, "GlobalUsings.cs");
            var initialContent =
                "global using System;\n" +
                "global using SomeOther.Project.Generated.Widgets.Models;\n" +
                "global using SomeOther.Project.Generated.Widgets.Handlers;\n";
            File.WriteAllText(globalUsingsPath, initialContent);

            var doc = CreateOpenApiDocumentWithPaths("/pets");
            var config = new ServerDomainConfig { Namespace = "MyApp.Api.Domain" };

            // Act — `Widgets` is not a path segment in this spec, so neither line can be
            // stale generator output for this project.
            DomainGlobalUsingsHelper.EnsureUpdated(
                tempDir,
                [],
                "MyApp",
                ["Pets"],
                doc,
                config);

            // Assert — both third-party usings preserved
            var rewritten = File.ReadAllText(globalUsingsPath);
            Assert.Contains("SomeOther.Project.Generated.Widgets.Models;", rewritten, StringComparison.Ordinal);
            Assert.Contains("SomeOther.Project.Generated.Widgets.Handlers;", rewritten, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureUpdated_StillPrunesStaleEntry_WhenMiddleSegmentIsInCurrentPathSegments()
    {
        // Arrange — the legitimate stale-rename scenario: the project previously generated
        // `OldRoot.Generated.Pets.Handlers`, the user renamed the contracts namespace to
        // `NewRoot`, but the OpenAPI spec still has `/pets`. The old entry now points at
        // a non-existent namespace and SHOULD be pruned.
        var tempDir = Path.Combine(Path.GetTempPath(), "EnsureUpdatedStaleSingleSeg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var globalUsingsPath = Path.Combine(tempDir, "GlobalUsings.cs");
            var initialContent =
                "global using System;\n" +
                "global using OldRoot.Generated.Pets.Handlers;\n" +
                "global using OldRoot.Generated.Pets.Models;\n" +
                "global using NewRoot.Generated.Pets.Handlers;\n";
            File.WriteAllText(globalUsingsPath, initialContent);

            var discoveredNamespaces = new HashSet<string>(StringComparer.Ordinal)
            {
                "NewRoot.Generated.Pets.Handlers",
            };
            var doc = CreateOpenApiDocumentWithPaths("/pets");
            var config = new ServerDomainConfig { Namespace = "MyApp.Api.Domain" };

            // Act
            DomainGlobalUsingsHelper.EnsureUpdated(
                tempDir,
                discoveredNamespaces,
                "NewRoot",
                ["Pets"],
                doc,
                config);

            // Assert — old root pruned, new root kept
            var rewritten = File.ReadAllText(globalUsingsPath);
            Assert.DoesNotContain("OldRoot.Generated.Pets.Handlers", rewritten, StringComparison.Ordinal);
            Assert.DoesNotContain("OldRoot.Generated.Pets.Models", rewritten, StringComparison.Ordinal);
            Assert.Contains("NewRoot.Generated.Pets.Handlers", rewritten, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
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