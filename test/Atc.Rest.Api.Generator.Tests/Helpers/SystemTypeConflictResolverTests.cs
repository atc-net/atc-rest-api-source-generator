namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class SystemTypeConflictResolverTests
{
    private static readonly Lazy<CSharpCompilation> PlatformCompilation = new(() => CSharpCompilation.Create(
        "Platform",
        references: ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(p))));

    // ========== EnsureFullNamespaceIfNeeded Tests ==========
    [Fact]
    public void EnsureFullNamespaceIfNeeded_ConflictingType_ReturnsFullyQualified()
    {
        var resolver = new SystemTypeConflictResolver(["Task", "Pet"]);
        var result = resolver.EnsureFullNamespaceIfNeeded("Task");
        Assert.Equal("System.Threading.Tasks.Task", result);
    }

    [Fact]
    public void EnsureFullNamespaceIfNeeded_NonConflictingType_ReturnsOriginal()
    {
        var resolver = new SystemTypeConflictResolver(["Pet", "Order"]);
        var result = resolver.EnsureFullNamespaceIfNeeded("Task");
        Assert.Equal("Task", result);
    }

    [Fact]
    public void EnsureFullNamespaceIfNeeded_NonReservedModel_ReturnsOriginal()
    {
        var resolver = new SystemTypeConflictResolver(["Pet"]);
        var result = resolver.EnsureFullNamespaceIfNeeded("Pet");
        Assert.Equal("Pet", result);
    }

    [Theory]
    [InlineData("Action", "System.Action")]
    [InlineData("Exception", "System.Exception")]
    [InlineData("Guid", "System.Guid")]
    [InlineData("DateTime", "System.DateTime")]
    [InlineData("Uri", "System.Uri")]
    [InlineData("Version", "System.Version")]
    [InlineData("Stream", "System.IO.Stream")]
    [InlineData("Path", "System.IO.Path")]
    public void EnsureFullNamespaceIfNeeded_VariousConflicts_ReturnsExpectedFullName(
        string modelName,
        string expectedFullName)
    {
        var resolver = new SystemTypeConflictResolver([modelName]);
        var result = resolver.EnsureFullNamespaceIfNeeded(modelName);
        Assert.Equal(expectedFullName, result);
    }

    // ========== HasConflict Tests ==========
    [Fact]
    public void HasConflict_ConflictingModel_ReturnsTrue()
    {
        var resolver = new SystemTypeConflictResolver(["Task"]);
        Assert.True(resolver.HasConflict("Task"));
    }

    [Fact]
    public void HasConflict_NonConflictingModel_ReturnsFalse()
    {
        var resolver = new SystemTypeConflictResolver(["Pet"]);
        Assert.False(resolver.HasConflict("Pet"));
    }

    [Fact]
    public void HasConflict_ModelNotInResolver_ReturnsFalse()
    {
        var resolver = new SystemTypeConflictResolver(["Task"]);
        Assert.False(resolver.HasConflict("Order"));
    }

    // ========== ConflictCount Tests ==========
    [Fact]
    public void ConflictCount_NoConflicts_ReturnsZero()
    {
        var resolver = new SystemTypeConflictResolver(["Pet", "Order"]);
        Assert.Equal(0, resolver.ConflictCount);
    }

    [Fact]
    public void ConflictCount_MultipleConflicts_ReturnsCorrectCount()
    {
        var resolver = new SystemTypeConflictResolver(["Task", "Action", "Pet", "Exception"]);
        Assert.Equal(3, resolver.ConflictCount);
    }

    [Fact]
    public void ConflictCount_EmptyModelList_ReturnsZero()
    {
        var resolver = new SystemTypeConflictResolver([]);
        Assert.Equal(0, resolver.ConflictCount);
    }

    // ========== Case Sensitivity Tests ==========
    [Fact]
    public void EnsureFullNamespaceIfNeeded_CaseSensitive_DoesNotMatchDifferentCase()
    {
        var resolver = new SystemTypeConflictResolver(["task"]); // lowercase
        var result = resolver.EnsureFullNamespaceIfNeeded("task");
        Assert.Equal("task", result); // no conflict since "task" != "Task"
    }

    // ========== Every protected name resolves to a real .NET type of that name ==========
    public static TheoryData<string> ProtectedNames
        =>
        [
            "Task", "Action", "Func", "Type", "Exception", "Object", "String", "Guid", "DateTime", "TimeSpan", "Uri",
            "Version", "File", "Directory", "Path", "Stream", "Thread", "Timer", "Environment", "Console", "Math",
            "Random", "Array", "Attribute", "Buffer", "Convert", "Delegate", "Enum", "GC", "Monitor", "Mutex",
            "EventArgs",
        ];

    [Theory]
    [MemberData(nameof(ProtectedNames))]
    public void EnsureFullNamespaceIfNeeded_AProtectedName_IsTheFullNameOfARealTypeOfThatName(
        string name)
    {
        var resolver = new SystemTypeConflictResolver([name]);

        var fullName = resolver.EnsureFullNamespaceIfNeeded(name);

        Assert.True(resolver.HasConflict(name));
        Assert.EndsWith("." + name, fullName, StringComparison.Ordinal);

        // The framework the generated code compiles against: an open generic (Func) is named with its arity
        var types = PlatformCompilation.Value.GetTypesByMetadataName(fullName)
            .Concat(PlatformCompilation.Value.GetTypesByMetadataName(fullName + "`1"));
        Assert.Contains(types, t => t.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public);
    }
}