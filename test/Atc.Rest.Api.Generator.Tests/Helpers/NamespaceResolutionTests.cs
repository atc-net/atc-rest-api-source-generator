namespace Atc.Rest.Api.Generator.Tests.Helpers;

/// <summary>
/// Tests for namespace resolution scenarios in handler generation.
/// These tests verify the namespace discovery and resolution logic used by ApiServerDomainGenerator.
/// </summary>
public sealed class NamespaceResolutionTests : IDisposable
{
    private readonly string testRootDirectory;

    public NamespaceResolutionTests()
    {
        // Create a unique test directory for each test run
        testRootDirectory = Path.Combine(Path.GetTempPath(), $"atc-ns-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRootDirectory);
    }

    public void Dispose()
    {
        // Clean up test directories
        if (Directory.Exists(testRootDirectory))
        {
            try
            {
                Directory.Delete(testRootDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    [Fact]
    public void SingleLayerProject_ServerMarkerInSameDirectory_ReturnsServerNamespace()
    {
        // Arrange: Create a single-layer project with both markers in same directory
        var projectDir = Path.Combine(testRootDirectory, "MyApi");
        Directory.CreateDirectory(projectDir);

        // Server marker with explicit namespace
        var serverMarkerContent = """
            {
                "namespace": "MyApi.Contracts",
                "validateSpecificationStrategy": "Standard"
            }
            """;
        File.WriteAllText(Path.Combine(projectDir, ".atc-rest-api-server"), serverMarkerContent);

        // Domain marker
        var domainMarkerContent = """
            {
                "namespace": "MyApi.Domain",
                "generateHandlersOutput": "Handlers"
            }
            """;
        File.WriteAllText(Path.Combine(projectDir, ".atc-rest-api-server-handlers"), domainMarkerContent);

        // Act: Read server namespace from same directory
        var serverNamespace = TryGetServerNamespace(projectDir);

        // Assert: Should find namespace from same directory
        Assert.Equal("MyApi.Contracts", serverNamespace);
    }

    [Fact]
    public void SingleLayerProject_NoServerMarker_ReturnsNull()
    {
        // Arrange: Create project with only domain marker
        var projectDir = Path.Combine(testRootDirectory, "MyApi");
        Directory.CreateDirectory(projectDir);

        var domainMarkerContent = """
            {
                "namespace": "MyApi.Domain",
                "generateHandlersOutput": "Handlers"
            }
            """;
        File.WriteAllText(Path.Combine(projectDir, ".atc-rest-api-server-handlers"), domainMarkerContent);

        // Act
        var serverNamespace = TryGetServerNamespace(projectDir);

        // Assert: No server marker, should return null
        Assert.Null(serverNamespace);
    }

    [Fact]
    public void SingleLayerProject_ServerMarkerWithoutNamespace_ReturnsNull()
    {
        // Arrange: Server marker without explicit namespace
        var projectDir = Path.Combine(testRootDirectory, "MyApi");
        Directory.CreateDirectory(projectDir);

        var serverMarkerContent = """
            {
                "validateSpecificationStrategy": "Standard"
            }
            """;
        File.WriteAllText(Path.Combine(projectDir, ".atc-rest-api-server"), serverMarkerContent);

        // Act
        var serverNamespace = TryGetServerNamespace(projectDir);

        // Assert: Server marker exists but no namespace property
        Assert.Null(serverNamespace);
    }

    [Fact]
    public void SiblingProjects_ContractsInSiblingDirectory_ReturnsContractsNamespace()
    {
        // Arrange: Create NexusSample-like structure
        // Root/
        //   Api.Contracts/.atc-rest-api-server (namespace: "Contoso.IoT.Api.Contracts")
        //   Api.Domain/.atc-rest-api-server-handlers (namespace: "MyApp.Api.Domain")
        var rootDir = Path.Combine(testRootDirectory, "NexusSample");
        var contractsDir = Path.Combine(rootDir, "NexusSample.Api.Contracts");
        var domainDir = Path.Combine(rootDir, "NexusSample.Api.Domain");

        Directory.CreateDirectory(contractsDir);
        Directory.CreateDirectory(domainDir);

        // Server marker in Contracts project
        var serverMarkerContent = """
            {
                "namespace": "Contoso.IoT.Nexus.Api.Contracts",
                "validateSpecificationStrategy": "Strict"
            }
            """;
        File.WriteAllText(Path.Combine(contractsDir, ".atc-rest-api-server"), serverMarkerContent);

        // Domain marker in Domain project
        var domainMarkerContent = """
            {
                "namespace": "NexusSample.Api.Domain",
                "generateHandlersOutput": "ApiHandlers"
            }
            """;
        File.WriteAllText(Path.Combine(domainDir, ".atc-rest-api-server-handlers"), domainMarkerContent);

        // Act: From Domain directory, should find Contracts namespace in sibling
        var serverNamespace = TryGetServerNamespace(domainDir);

        // Assert: Should discover namespace from sibling Contracts project
        Assert.Equal("Contoso.IoT.Nexus.Api.Contracts", serverNamespace);
    }

    [Fact]
    public void SiblingProjects_MultipleSiblings_FindsFirstServerMarker()
    {
        // Arrange: Multiple sibling directories, only one has server marker
        var rootDir = Path.Combine(testRootDirectory, "MultiProject");
        var dir1 = Path.Combine(rootDir, "Project.Api");
        var dir2 = Path.Combine(rootDir, "Project.Api.Contracts");
        var dir3 = Path.Combine(rootDir, "Project.Api.Domain");
        var dir4 = Path.Combine(rootDir, "Project.Tests");

        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);
        Directory.CreateDirectory(dir3);
        Directory.CreateDirectory(dir4);

        // Only dir2 (Contracts) has server marker
        var serverMarkerContent = """
            {
                "namespace": "MyCompany.Project.Api.Contracts",
                "validateSpecificationStrategy": "Standard"
            }
            """;
        File.WriteAllText(Path.Combine(dir2, ".atc-rest-api-server"), serverMarkerContent);

        // Domain marker in dir3
        var domainMarkerContent = """
            {
                "namespace": "MyCompany.Project.Api.Domain"
            }
            """;
        File.WriteAllText(Path.Combine(dir3, ".atc-rest-api-server-handlers"), domainMarkerContent);

        // Act: From Domain directory, should find server marker in sibling
        var serverNamespace = TryGetServerNamespace(dir3);

        // Assert
        Assert.Equal("MyCompany.Project.Api.Contracts", serverNamespace);
    }

    [Fact]
    public void SiblingProjects_NoSiblingWithServerMarker_ReturnsNull()
    {
        // Arrange: Siblings exist but none have server marker
        var rootDir = Path.Combine(testRootDirectory, "NoServerMarker");
        var dir1 = Path.Combine(rootDir, "Project.Api");
        var dir2 = Path.Combine(rootDir, "Project.Api.Domain");

        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        // Domain marker only
        var domainMarkerContent = """
            {
                "namespace": "MyCompany.Project.Api.Domain"
            }
            """;
        File.WriteAllText(Path.Combine(dir2, ".atc-rest-api-server-handlers"), domainMarkerContent);

        // Act
        var serverNamespace = TryGetServerNamespace(dir2);

        // Assert: No server marker in any sibling
        Assert.Null(serverNamespace);
    }

    [Fact]
    public void ServerMarker_JsonExtension_IsRecognized()
    {
        // Arrange: Server marker with .json extension
        var projectDir = Path.Combine(testRootDirectory, "JsonExtension");
        Directory.CreateDirectory(projectDir);

        var serverMarkerContent = """
            {
                "namespace": "MyApi.Contracts.Json"
            }
            """;
        File.WriteAllText(Path.Combine(projectDir, ".atc-rest-api-server.json"), serverMarkerContent);

        // Act
        var serverNamespace = TryGetServerNamespace(projectDir);

        // Assert
        Assert.Equal("MyApi.Contracts.Json", serverNamespace);
    }

    [Fact]
    public void SiblingProjects_JsonExtension_IsRecognized()
    {
        // Arrange: Sibling has server marker with .json extension
        var rootDir = Path.Combine(testRootDirectory, "SiblingJson");
        var contractsDir = Path.Combine(rootDir, "Api.Contracts");
        var domainDir = Path.Combine(rootDir, "Api.Domain");

        Directory.CreateDirectory(contractsDir);
        Directory.CreateDirectory(domainDir);

        var serverMarkerContent = """
            {
                "namespace": "MyCompany.Api.Contracts.Json"
            }
            """;
        File.WriteAllText(Path.Combine(contractsDir, ".atc-rest-api-server.json"), serverMarkerContent);

        // Act: From Domain directory
        var serverNamespace = TryGetServerNamespace(domainDir);

        // Assert
        Assert.Equal("MyCompany.Api.Contracts.Json", serverNamespace);
    }

    [Fact]
    public void NamespaceResolution_ExplicitContractsNamespace_TakesPriority()
    {
        // Arrange: Both explicit config and discovered server namespace exist
        var rootDir = Path.Combine(testRootDirectory, "Priority");
        var contractsDir = Path.Combine(rootDir, "Api.Contracts");
        var domainDir = Path.Combine(rootDir, "Api.Domain");

        Directory.CreateDirectory(contractsDir);
        Directory.CreateDirectory(domainDir);

        // Server marker with one namespace
        var serverMarkerContent = """
            {
                "namespace": "Discovered.Namespace"
            }
            """;
        File.WriteAllText(Path.Combine(contractsDir, ".atc-rest-api-server"), serverMarkerContent);

        // Domain config with explicit contractsNamespace
        var domainConfig = new ServerDomainConfig
        {
            Namespace = "MyApp.Api.Domain",
            ContractsNamespace = "Explicit.Contracts.Namespace",
        };

        // Simulate discovery
        var discoveredNamespace = TryGetServerNamespace(domainDir);

        // Act: Apply resolution priority
        var resolvedNamespace = domainConfig.ContractsNamespace ?? discoveredNamespace;

        // Assert: Explicit config takes priority
        Assert.Equal("Explicit.Contracts.Namespace", resolvedNamespace);
    }

    [Fact]
    public void NamespaceResolution_NoExplicitConfig_UsesDiscoveredNamespace()
    {
        // Arrange
        var rootDir = Path.Combine(testRootDirectory, "DiscoveredPriority");
        var contractsDir = Path.Combine(rootDir, "Api.Contracts");
        var domainDir = Path.Combine(rootDir, "Api.Domain");

        Directory.CreateDirectory(contractsDir);
        Directory.CreateDirectory(domainDir);

        var serverMarkerContent = """
            {
                "namespace": "Discovered.Namespace"
            }
            """;
        File.WriteAllText(Path.Combine(contractsDir, ".atc-rest-api-server"), serverMarkerContent);

        var domainConfig = new ServerDomainConfig
        {
            Namespace = "MyApp.Api.Domain",
            ContractsNamespace = null, // Not explicitly set
        };

        var discoveredNamespace = TryGetServerNamespace(domainDir);

        // Act
        var resolvedNamespace = domainConfig.ContractsNamespace ?? discoveredNamespace;

        // Assert: Uses discovered namespace
        Assert.Equal("Discovered.Namespace", resolvedNamespace);
    }

    [Fact]
    public void NamespaceResolution_NoConfigOrDiscovery_FallsBackToDerivedNamespace()
    {
        // Arrange: No server marker, no explicit config
        var domainDir = Path.Combine(testRootDirectory, "FallbackOnly");
        Directory.CreateDirectory(domainDir);

        var domainConfig = new ServerDomainConfig
        {
            Namespace = "MyApp.Api.Domain",
            ContractsNamespace = null,
        };

        var discoveredNamespace = TryGetServerNamespace(domainDir);

        // Act: Simulate fallback logic
        var contractsNamespace = domainConfig.ContractsNamespace ?? discoveredNamespace;
        var rootNamespace = contractsNamespace is not null
            ? contractsNamespace
            : (domainConfig.Namespace ?? "Default")
                .Replace(".Api.Domain", string.Empty, StringComparison.Ordinal)
                .Replace(".Domain", string.Empty, StringComparison.Ordinal);

        // Assert: Falls back to derived namespace
        Assert.Equal("MyApp", rootNamespace);
    }

    [Fact]
    public void EmptyDirectory_ReturnsNull()
    {
        // Arrange
        var emptyDir = Path.Combine(testRootDirectory, "Empty");
        Directory.CreateDirectory(emptyDir);

        // Act
        var serverNamespace = TryGetServerNamespace(emptyDir);

        // Assert
        Assert.Null(serverNamespace);
    }

    [Fact]
    public void InvalidJson_ReturnsNull()
    {
        // Arrange: Malformed JSON in server marker
        var projectDir = Path.Combine(testRootDirectory, "InvalidJson");
        Directory.CreateDirectory(projectDir);

        File.WriteAllText(Path.Combine(projectDir, ".atc-rest-api-server"), "{ invalid json }");

        // Act
        var serverNamespace = TryGetServerNamespace(projectDir);

        // Assert: Should handle gracefully
        Assert.Null(serverNamespace);
    }

    [Fact]
    public void NullOrEmptyDirectory_ReturnsNull()
    {
        // Act & Assert
        Assert.Null(TryGetServerNamespace(string.Empty));
        Assert.Null(TryGetServerNamespace(null!));
    }

    /// <summary>
    /// Mimics the TryGetServerNamespace logic from ApiServerDomainGenerator.
    /// This allows testing the namespace resolution without needing the full generator.
    /// </summary>
    private static string? TryGetServerNamespace(string? markerDirectory)
    {
        if (string.IsNullOrEmpty(markerDirectory))
        {
            return null;
        }

        // First, check same directory
        var serverMarkerPath = Path.Combine(markerDirectory, ".atc-rest-api-server");
        var serverMarkerJsonPath = Path.Combine(markerDirectory, ".atc-rest-api-server.json");

        var markerPath = File.Exists(serverMarkerPath) ? serverMarkerPath :
                         File.Exists(serverMarkerJsonPath) ? serverMarkerJsonPath : null;

        // If not in same directory, search sibling directories
        if (markerPath == null)
        {
            var parentDirectory = Path.GetDirectoryName(markerDirectory);
            if (!string.IsNullOrEmpty(parentDirectory) && Directory.Exists(parentDirectory))
            {
                foreach (var siblingDir in Directory.GetDirectories(parentDirectory))
                {
                    if (siblingDir == markerDirectory)
                    {
                        continue;
                    }

                    var siblingMarkerPath = Path.Combine(siblingDir, ".atc-rest-api-server");
                    var siblingMarkerJsonPath = Path.Combine(siblingDir, ".atc-rest-api-server.json");

                    if (File.Exists(siblingMarkerPath))
                    {
                        markerPath = siblingMarkerPath;
                        break;
                    }

                    if (File.Exists(siblingMarkerJsonPath))
                    {
                        markerPath = siblingMarkerJsonPath;
                        break;
                    }
                }
            }
        }

        if (markerPath == null)
        {
            return null;
        }

        try
        {
            var content = File.ReadAllText(markerPath);
            var serverConfig = JsonSerializer.Deserialize<ServerConfig>(content, JsonHelper.ConfigOptions);
            return serverConfig?.Namespace;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

