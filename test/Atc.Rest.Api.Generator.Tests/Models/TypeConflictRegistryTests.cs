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
        var conflicts = TypeConflictRegistry.ScanForConflicts(doc, "KL.IoT.Device.Management");

        // Assert
        Assert.Contains("Device", conflicts);
    }

    [Fact]
    public void ScanForConflicts_SchemaNotMatchingNamespaceSegment_NoConflict()
    {
        // Arrange: "Pet" schema does not match any segment of "KL.IoT.Device.Management"
        var doc = OpenApiDocumentHelper.ParseYaml(MinimalYamlWithDeviceSchema);

        // Act
        var conflicts = TypeConflictRegistry.ScanForConflicts(doc, "KL.IoT.Device.Management");

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
        var registry = TypeConflictRegistry.Build(doc, "KL.IoT.Device.Management", "Devices");

        // Act
        var resolved = registry.GetFullyQualifiedName("Device");

        // Assert
        Assert.Equal("KL.IoT.Device.Management.Generated.Devices.Models.Device", resolved);
    }

    [Fact]
    public void ResolveTypeName_NonConflicting_ReturnsShortName()
    {
        // Arrange
        var doc = OpenApiDocumentHelper.ParseYaml(MinimalYamlWithDeviceSchema);
        var registry = TypeConflictRegistry.Build(doc, "KL.IoT.Device.Management", "Devices");

        // Act
        var resolved = registry.ResolveTypeName("Pet");

        // Assert
        Assert.Equal("Pet", resolved);
    }
}