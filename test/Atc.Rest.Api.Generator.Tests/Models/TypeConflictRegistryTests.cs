namespace Atc.Rest.Api.Generator.Tests.Models;

public class TypeConflictRegistryTests
{
    private const string MinimalYamlWithDeviceSchema = """
        openapi: "3.1.1"
        info:
          title: Test API
          version: "1.0.0"
        paths:
          /devices:
            get:
              operationId: getDevices
              responses:
                200:
                  description: OK
        components:
          schemas:
            Device:
              type: object
              properties:
                id:
                  type: string
            Pet:
              type: object
              properties:
                name:
                  type: string
        """;

    private const string YamlWithTaskSchema = """
        openapi: "3.1.1"
        info:
          title: Test API
          version: "1.0.0"
        paths:
          /tasks:
            get:
              operationId: getTasks
              responses:
                200:
                  description: OK
        components:
          schemas:
            Task:
              type: object
              properties:
                id:
                  type: string
        """;

    [Fact]
    public void ScanForConflicts_SchemaMatchingNamespaceSegment_DetectsConflict()
    {
        // Arrange: "Device" schema + namespace containing "Device" segment
        var doc = OpenApiDocumentHelper.ParseYaml(MinimalYamlWithDeviceSchema);

        // Act
        var conflicts = TypeConflictRegistry.ScanForConflicts(doc, "Contoso.Data.Device.Management");

        // Assert
        Assert.Contains("Device", conflicts);
    }

    [Fact]
    public void ScanForConflicts_SchemaNotMatchingNamespaceSegment_NoConflict()
    {
        // Arrange: "Pet" schema does not match any segment of "Contoso.Data.Device.Management"
        var doc = OpenApiDocumentHelper.ParseYaml(MinimalYamlWithDeviceSchema);

        // Act
        var conflicts = TypeConflictRegistry.ScanForConflicts(doc, "Contoso.Data.Device.Management");

        // Assert
        Assert.DoesNotContain("Pet", conflicts);
    }

    [Fact]
    public void ScanForConflicts_NullNamespace_BackwardCompatible()
    {
        // Arrange: No namespace provided — should only detect system type conflicts
        var doc = OpenApiDocumentHelper.ParseYaml(MinimalYamlWithDeviceSchema);

        // Act
        var conflicts = TypeConflictRegistry.ScanForConflicts(doc);

        // Assert: "Device" is not a reserved system type, so no conflict
        Assert.DoesNotContain("Device", conflicts);
    }

    [Fact]
    public void ScanForConflicts_SystemTypeConflict_StillDetected()
    {
        // Arrange: "Task" schema conflicts with System.Threading.Tasks.Task
        var doc = OpenApiDocumentHelper.ParseYaml(YamlWithTaskSchema);

        // Act
        var conflicts = TypeConflictRegistry.ScanForConflicts(doc, "Some.Namespace");

        // Assert: System type conflict should always be detected
        Assert.Contains("Task", conflicts);
    }

    [Fact]
    public void GetFullyQualifiedName_NamespaceConflict_ReturnsQualifiedName()
    {
        // Arrange: Build registry with namespace that contains "Device" segment
        var doc = OpenApiDocumentHelper.ParseYaml(MinimalYamlWithDeviceSchema);
        var registry = TypeConflictRegistry.Build(doc, "Contoso.Data.Device.Management", "Devices");

        // Act
        var resolved = registry.GetFullyQualifiedName("Device");

        // Assert
        Assert.Equal("Contoso.Data.Device.Management.Generated.Devices.Models.Device", resolved);
    }

    [Fact]
    public void GetFullyQualifiedName_EmptyPathSegment_ReturnsSegmentLessNamespace()
    {
        // Arrange: callers normalize a redundant/collapsed path segment to string.Empty
        var doc = OpenApiDocumentHelper.ParseYaml(MinimalYamlWithDeviceSchema);
        var registry = TypeConflictRegistry.Build(doc, "Contoso.Data.Device.Management", string.Empty);

        // Act
        var resolved = registry.GetFullyQualifiedName("Device");

        // Assert: must not emit a double dot ("...Generated..Models.Device")
        Assert.Equal("Contoso.Data.Device.Management.Generated.Models.Device", resolved);
    }

    [Fact]
    public void ResolveTypeName_NonConflicting_ReturnsShortName()
    {
        // Arrange
        var doc = OpenApiDocumentHelper.ParseYaml(MinimalYamlWithDeviceSchema);
        var registry = TypeConflictRegistry.Build(doc, "Contoso.Data.Device.Management", "Devices");

        // Act
        var resolved = registry.ResolveTypeName("Pet");

        // Assert
        Assert.Equal("Pet", resolved);
    }

    [Fact]
    public void IsConflicting_NameInScannedConflicts_ReturnsTrue()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(YamlWithTaskSchema);
        var registry = TypeConflictRegistry.Build(doc, "MyApi");

        Assert.True(registry.IsConflicting("Task"));
    }

    [Fact]
    public void IsConflicting_NameNotInConflicts_ReturnsFalse()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(MinimalYamlWithDeviceSchema);
        var registry = TypeConflictRegistry.Build(doc, "MyApi");

        Assert.False(registry.IsConflicting("Pet"));
    }

    [Fact]
    public void ForSegment_SharedSchema_ResolvesToSharedModelsNamespace()
    {
        var conflicts = new HashSet<string>(StringComparer.Ordinal) { "Device" };
        var shared = new HashSet<string>(StringComparer.Ordinal) { "Device" };

        var registry = TypeConflictRegistry.ForSegment(conflicts, "MyApi", "Devices", shared);

        Assert.Equal("MyApi.Generated.Models.Device", registry.GetFullyQualifiedName("Device"));
    }

    [Fact]
    public void ForSegment_SegmentOwnedSchema_ResolvesToSegmentNamespace()
    {
        var conflicts = new HashSet<string>(StringComparer.Ordinal) { "Device" };

        var registry = TypeConflictRegistry.ForSegment(conflicts, "MyApi", "Devices");

        Assert.Equal("MyApi.Generated.Devices.Models.Device", registry.GetFullyQualifiedName("Device"));
        Assert.True(registry.IsConflicting("Device"));
    }

    [Theory]
    [InlineData("type: object", true)]
    [InlineData("type: [object, 'null']", true)]
    [InlineData("type: string\nenum: [a, b]", true)]
    [InlineData("allOf:\n  - type: object", true)]
    [InlineData("oneOf:\n  - type: string\n  - type: integer", true)]
    [InlineData("type: string", false)]
    [InlineData("type: array\nitems:\n  type: string", false)]
    [InlineData("type: integer\nformat: int64", false)]
    public void IsGeneratedType_DependsOnWhetherAliasOrModel(
        string schemaBody,
        bool expected)
    {
        // Every line of the body sits under "Candidate:" (6 spaces in the stripped literal).
        var doc = OpenApiDocumentHelper.ParseYaml(
            $$"""
            openapi: "3.1.1"
            info:
              title: Test API
              version: "1.0.0"
            paths: {}
            components:
              schemas:
                Candidate:
                  {{schemaBody.Replace("\n", "\n      ", StringComparison.Ordinal)}}
            """);

        var schema = doc.Components!.Schemas!["Candidate"];

        Assert.Equal(expected, TypeConflictRegistry.IsGeneratedType(schema));
    }

    [Fact]
    public void IsGeneratedType_UntypedSchema_ReturnsTrue()
        => Assert.True(TypeConflictRegistry.IsGeneratedType(new OpenApiSchema()));

    [Fact]
    public void IsGeneratedType_PrefixItemsTuple_ReturnsTrue()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(
            """
            openapi: "3.1.1"
            info:
              title: Test API
              version: "1.0.0"
            paths: {}
            components:
              schemas:
                Point:
                  type: array
                  prefixItems:
                    - type: number
                    - type: number
            """);

        var schema = doc.Components!.Schemas!["Point"];

        Assert.True(TypeConflictRegistry.IsGeneratedType(schema));
    }
}