namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Verifies that the TypedClient generation mode emits an <c>I{ClientName}</c> interface
/// alongside the client, that the client implements it, and that the pair compiles.
/// </summary>
public class ClientInterfaceGenerationTests
{
    [Theory]
    [InlineData("PetStoreSimple", "PetStoreSimple.yaml", "PetStoreSimpleClient")]
    [InlineData("Demo", "Demo.yaml", "AccountsClient")]
    [InlineData("Demo", "Demo.yaml", "TasksClient")]
    public void TypedClient_GeneratesInterfaceFile(
        string scenarioName,
        string yamlFileName,
        string expectedClientName)
    {
        // Arrange & Act
        var generatedSources = CompilationVerificationHarness.RunClient(scenarioName, yamlFileName);

        // Assert
        var interfaceSource = generatedSources
            .FirstOrDefault(x => x.HintName.EndsWith($"I{expectedClientName}.g.cs", StringComparison.Ordinal));

        Assert.False(
            string.IsNullOrEmpty(interfaceSource.Source),
            $"Expected an I{expectedClientName}.g.cs source file. Got:\n" +
            string.Join("\n", generatedSources.Select(x => x.HintName)));

        Assert.Contains($"public interface I{expectedClientName}", interfaceSource.Source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PetStoreSimple", "PetStoreSimple.yaml", "PetStoreSimpleClient")]
    [InlineData("Demo", "Demo.yaml", "AccountsClient")]
    [InlineData("Demo", "Demo.yaml", "TasksClient")]
    public void TypedClient_ImplementsGeneratedInterface(
        string scenarioName,
        string yamlFileName,
        string expectedClientName)
    {
        // Arrange & Act
        var generatedSources = CompilationVerificationHarness.RunClient(scenarioName, yamlFileName);

        // Assert
        var clientSource = generatedSources
            .First(x => x.HintName.EndsWith($"{expectedClientName}.g.cs", StringComparison.Ordinal) &&
                        !x.HintName.EndsWith($"I{expectedClientName}.g.cs", StringComparison.Ordinal));

        Assert.Contains(
            $"public sealed class {expectedClientName} : I{expectedClientName}",
            clientSource.Source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TypedClient_Interface_DeclaresEveryPublicOperation()
    {
        // Arrange & Act
        var generatedSources = CompilationVerificationHarness.RunClient("PetStoreSimple", "PetStoreSimple.yaml");

        var interfaceSource = generatedSources
            .First(x => x.HintName.EndsWith("IPetStoreSimpleClient.g.cs", StringComparison.Ordinal))
            .Source;

        // Assert - one member per operation, signature only (no body).
        Assert.Contains("ListPetsAsync(", interfaceSource, StringComparison.Ordinal);
        Assert.Contains("CreatePetsAsync(", interfaceSource, StringComparison.Ordinal);
        Assert.Contains("ShowPetByIdAsync(", interfaceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("httpClient", interfaceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("{", interfaceSource[interfaceSource.IndexOf("ListPetsAsync(", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    [Fact]
    public void TypedClient_Interface_HasGeneratedCodeAttributeAndNullableEnabled()
    {
        // Arrange & Act
        var generatedSources = CompilationVerificationHarness.RunClient("PetStoreSimple", "PetStoreSimple.yaml");

        var interfaceSource = generatedSources
            .First(x => x.HintName.EndsWith("IPetStoreSimpleClient.g.cs", StringComparison.Ordinal))
            .Source;

        // Assert
        Assert.Contains("#nullable enable", interfaceSource, StringComparison.Ordinal);
        Assert.Contains("[GeneratedCode(", interfaceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedClient_Interface_DoesNotLeakImplementationOnlyMembers()
    {
        // Arrange & Act
        var generatedSources = CompilationVerificationHarness.RunClient("StreamingItemSchema", "StreamingItemSchema.yaml");

        var interfaceSource = generatedSources
            .First(x => x.HintName.EndsWith("Client.g.cs", StringComparison.Ordinal) &&
                        x.HintName.Contains(".I", StringComparison.Ordinal))
            .Source;

        // Assert - [EnumeratorCancellation] is only valid on the implementation, and the
        // private EnsureSuccessAsync helper must stay an implementation detail.
        Assert.DoesNotContain("EnumeratorCancellation", interfaceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureSuccessAsync", interfaceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public ", interfaceSource[interfaceSource.IndexOf('{', StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PetStoreSimple", "PetStoreSimple.yaml")]
    [InlineData("Demo", "Demo.yaml")]
    [InlineData("HttpMethods", "HttpMethods.yaml")]
    [InlineData("StreamingItemSchema", "StreamingItemSchema.yaml")]
    [InlineData("ParameterSerialization", "ParameterSerialization.yaml")]
    [InlineData("ComponentsReuse", "ComponentsReuse.yaml")]
    public void TypedClient_WithInterface_CompilesWithoutErrors(
        string scenarioName,
        string yamlFileName)
    {
        // Arrange & Act
        var generatedSources = CompilationVerificationHarness.RunClient(scenarioName, yamlFileName);

        Assert.NotEmpty(generatedSources);

        // Assert
        var errors = CompilationVerificationHarness.CompileGeneratedSources(generatedSources);

        Assert.True(
            errors.Count == 0,
            $"Generated client + interface for {scenarioName} did not compile:\n" +
            string.Join('\n', errors));
    }

    [Theory]
    [InlineData("PetStoreSimple", "PetStoreSimple.yaml", "PetStoreSimple.Generated.PetStoreSimpleClient")]
    [InlineData("Demo", "Demo.yaml", "Demo.Generated.Accounts.Client.AccountsClient")]
    public void TypedClient_IsAssignableToGeneratedInterface(
        string scenarioName,
        string yamlFileName,
        string clientFullName)
    {
        // Arrange & Act - emit and load the generated assembly so the check is against real metadata.
        var generatedSources = CompilationVerificationHarness.RunClient(scenarioName, yamlFileName);
        var assembly = CompilationVerificationHarness.EmitAndLoad(generatedSources);

        var clientType = assembly.GetType(clientFullName);
        Assert.NotNull(clientType);

        var lastDot = clientFullName.LastIndexOf('.');
        var interfaceFullName = $"{clientFullName[..lastDot]}.I{clientFullName[(lastDot + 1)..]}";
        var interfaceType = assembly.GetType(interfaceFullName);

        // Assert
        Assert.NotNull(interfaceType);
        Assert.True(interfaceType.IsInterface, $"{interfaceFullName} is not an interface");
        Assert.True(
            interfaceType.IsAssignableFrom(clientType),
            $"{clientFullName} does not implement {interfaceFullName}");

        // Every public operation on the client is reachable through the interface.
        var interfaceMethodNames = interfaceType.GetMethods().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var clientOperationNames = clientType!
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(x => x.Name.EndsWith("Async", StringComparison.Ordinal))
            .Select(x => x.Name);

        Assert.All(
            clientOperationNames,
            name => Assert.Contains(name, interfaceMethodNames));
    }

    // ========== DI registration extension ==========
    [Fact]
    public void TypedClient_WithHttpClientFactoryReferenced_GeneratesDiExtension()
    {
        // Arrange & Act - full references make Microsoft.Extensions.Http available.
        var (_, generatedSources) = CompilationVerificationHarness.RunGenerator(
            new ApiClientGenerator(),
            "PetStoreSimple",
            "PetStoreSimple.yaml",
            ".atc-rest-api-client",
            "Client-Typed",
            useFullReferences: true);

        // Assert
        var diSource = generatedSources
            .FirstOrDefault(x => x.HintName.EndsWith("ServiceCollectionExtensions.g.cs", StringComparison.Ordinal));

        Assert.False(
            string.IsNullOrEmpty(diSource.Source),
            "Expected a ServiceCollectionExtensions.g.cs source file. Got:\n" +
            string.Join("\n", generatedSources.Select(x => x.HintName)));

        // Registers through the interface, so consumer code can depend on the contract.
        // An explicit factory is required because the client has two public constructors.
        Assert.Contains(
            "AddHttpClient<IPetStoreSimpleClient>",
            diSource.Source,
            StringComparison.Ordinal);

        Assert.Contains(
            "new PetStoreSimpleClient(httpClient)",
            diSource.Source,
            StringComparison.Ordinal);

        Assert.Contains("AddPetStoreSimpleClient", diSource.Source, StringComparison.Ordinal);
        Assert.Contains("[GeneratedCode(", diSource.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedClient_WithoutHttpClientFactoryReferenced_SkipsDiExtension()
    {
        // Arrange & Act - minimal references, so Microsoft.Extensions.Http is absent.
        // Emitting AddHttpClient<,> here would not compile in the consumer project.
        var generatedSources = CompilationVerificationHarness.RunClient("PetStoreSimple", "PetStoreSimple.yaml");

        // Assert - the client and interface are still generated.
        Assert.Contains(generatedSources, x => x.HintName.EndsWith("IPetStoreSimpleClient.g.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(generatedSources, x => x.HintName.EndsWith("ServiceCollectionExtensions.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void TypedClient_DiExtension_CompilesWithoutErrors()
    {
        // Arrange & Act
        var (_, generatedSources) = CompilationVerificationHarness.RunGenerator(
            new ApiClientGenerator(),
            "PetStoreSimple",
            "PetStoreSimple.yaml",
            ".atc-rest-api-client",
            "Client-Typed",
            useFullReferences: true);

        // Assert
        var errors = CompilationVerificationHarness.CompileGeneratedSources(generatedSources);

        Assert.True(
            errors.Count == 0,
            "Generated DI extension did not compile:\n" + string.Join("\n", errors));
    }

    [Fact]
    public void TypedClient_PerArea_GeneratesDiExtensionPerClient()
    {
        // Arrange & Act
        var (_, generatedSources) = CompilationVerificationHarness.RunGenerator(
            new ApiClientGenerator(),
            "Demo",
            "Demo.yaml",
            ".atc-rest-api-client",
            "Client-Typed",
            useFullReferences: true);

        var diSources = generatedSources
            .Where(x => x.HintName.EndsWith("ServiceCollectionExtensions.g.cs", StringComparison.Ordinal))
            .Select(x => x.Source)
            .ToList();

        // Assert - one registration per area client.
        Assert.NotEmpty(diSources);
        var combined = string.Join("\n", diSources);
        Assert.Contains("AddHttpClient<IAccountsClient>", combined, StringComparison.Ordinal);
        Assert.Contains("new AccountsClient(httpClient)", combined, StringComparison.Ordinal);
        Assert.Contains("AddHttpClient<ITasksClient>", combined, StringComparison.Ordinal);
        Assert.Contains("new TasksClient(httpClient)", combined, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PetStoreSimple", "PetStoreSimple.yaml", "PetStoreSimple.Generated", "PetStoreSimpleClient")]
    [InlineData("Demo", "Demo.yaml", "Demo.Generated.Accounts.Client", "AccountsClient")]
    public void TypedClient_DiExtension_ResolvesInterfaceFromContainer(
        string scenarioName,
        string yamlFileName,
        string clientNamespace,
        string clientTypeName)
    {
        // Arrange - emit and load, then drive the generated extension for real.
        // String-matching the DI source is not enough: registering via AddHttpClient<TInterface, TClient>
        // compiles fine but throws at *resolve* time, because the client has two public constructors
        // ((HttpClient) and (HttpClient, JsonSerializerOptions)) that ActivatorUtilities cannot
        // disambiguate. Only an actual GetRequiredService call catches that regression.
        var (_, generatedSources) = CompilationVerificationHarness.RunGenerator(
            new ApiClientGenerator(),
            scenarioName,
            yamlFileName,
            ".atc-rest-api-client",
            "Client-Typed",
            useFullReferences: true);

        var assembly = CompilationVerificationHarness.EmitAndLoad(generatedSources);

        var interfaceType = assembly.GetType($"{clientNamespace}.I{clientTypeName}");
        Assert.NotNull(interfaceType);

        var extensionsType = assembly.GetType($"{clientNamespace}.{clientTypeName}ServiceCollectionExtensions");
        Assert.NotNull(extensionsType);

        var addMethod = extensionsType!.GetMethod(
            $"Add{clientTypeName}",
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(addMethod);

        // Act
        var services = new ServiceCollection();
        addMethod!.Invoke(null, [services, null]);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        // Assert
        var resolved = provider.GetService(interfaceType!);

        Assert.True(
            resolved is not null,
            $"Could not resolve I{clientTypeName} from the container built by Add{clientTypeName}().");

        Assert.IsAssignableFrom(interfaceType!, resolved!);
    }
}