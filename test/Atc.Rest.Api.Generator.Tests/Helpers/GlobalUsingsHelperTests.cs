namespace Atc.Rest.Api.Generator.Tests.Helpers;

[SuppressMessage("Design", "MA0051:Method is too long", Justification = "Test methods")]
public sealed class GlobalUsingsHelperTests : IDisposable
{
    private readonly DirectoryInfo workingDirectory;

    public GlobalUsingsHelperTests()
    {
        workingDirectory = new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), $"atc-global-usings-test-{Guid.NewGuid()}"));

        if (Directory.Exists(workingDirectory.FullName))
        {
            Directory.Delete(workingDirectory.FullName, recursive: true);
        }

        Directory.CreateDirectory(workingDirectory.FullName);
    }

    public void Dispose()
    {
        if (Directory.Exists(workingDirectory.FullName))
        {
            Directory.Delete(workingDirectory.FullName, recursive: true);
        }
    }

    [Fact]
    public void GenerateContent_EmptyNamespaces_ReturnsEmptyString()
    {
        // Arrange
        var namespaces = new List<string>();

        // Act
        var result = GlobalUsingsHelper.GenerateContent(namespaces);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GenerateContent_SingleNamespace_ReturnsGlobalUsing()
    {
        // Arrange
        var namespaces = new List<string> { "System.Text" };

        // Act
        var result = GlobalUsingsHelper.GenerateContent(namespaces);

        // Assert
        Assert.Contains("global using System.Text;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateContent_MultipleNamespaces_SortsAlphabetically()
    {
        // Arrange
        var namespaces = new List<string> { "Zebra", "Apple", "Monkey" };

        // Act
        var result = GlobalUsingsHelper.GenerateContent(namespaces, setSystemFirst: false, addNamespaceSeparator: false);

        // Assert
        var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Equal("global using Apple;", lines[0]);
        Assert.Equal("global using Monkey;", lines[1]);
        Assert.Equal("global using Zebra;", lines[2]);
    }

    [Fact]
    public void GenerateContent_WithSetSystemFirst_SystemNamespacesFirst()
    {
        // Arrange
        var namespaces = new List<string> { "Atc.Helpers", "System.Text", "System", "Microsoft.Extensions" };

        // Act
        var result = GlobalUsingsHelper.GenerateContent(namespaces, setSystemFirst: true, addNamespaceSeparator: false);

        // Assert
        var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);
        Assert.Equal("global using System;", lines[0]);
        Assert.Equal("global using System.Text;", lines[1]);
        Assert.Equal("global using Atc.Helpers;", lines[2]);
        Assert.Equal("global using Microsoft.Extensions;", lines[3]);
    }

    [Fact]
    public void GenerateContent_WithNamespaceSeparator_AddsBlankLinesBetweenGroups()
    {
        // Arrange
        var namespaces = new List<string>
        {
            "System.Text",
            "Atc.Helpers",
            "Microsoft.Extensions",
        };

        // Act
        var result = GlobalUsingsHelper.GenerateContent(namespaces, setSystemFirst: true, addNamespaceSeparator: true);

        // Assert
        Assert.Contains("global using System.Text;", result, StringComparison.Ordinal);
        Assert.Contains("global using Atc.Helpers;", result, StringComparison.Ordinal);
        Assert.Contains("global using Microsoft.Extensions;", result, StringComparison.Ordinal);

        // Verify blank lines exist between groups
        var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var blankLineCount = 0;
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                blankLineCount++;
            }
        }

        // Should have at least 2 blank lines (between System/Atc and Atc/Microsoft)
        Assert.True(blankLineCount >= 2, $"Expected at least 2 blank lines but found {blankLineCount}");
    }

    [Fact]
    public void GenerateContent_WithoutNamespaceSeparator_NoBlankLines()
    {
        // Arrange
        var namespaces = new List<string>
        {
            "System.Text",
            "Atc.Helpers",
            "Microsoft.Extensions",
        };

        // Act
        var result = GlobalUsingsHelper.GenerateContent(namespaces, setSystemFirst: true, addNamespaceSeparator: false);

        // Assert
        var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Should have exactly 3 non-empty lines (no blank lines between groups)
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void ExtractNamespacesFromContent_String_ExtractsNamespaces()
    {
        // Arrange
        var content = @"global using System;
global using System.Text;
global using Atc.Helpers;";

        // Act
        var result = GlobalUsingsHelper.ExtractNamespacesFromContent(content);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("System", result);
        Assert.Contains("System.Text", result);
        Assert.Contains("Atc.Helpers", result);
    }

    [Fact]
    public void ExtractNamespacesFromContent_IgnoresComments()
    {
        // Arrange
        var content = @"// This is a comment
global using System;
// Another comment
global using System.Text;";

        // Act
        var result = GlobalUsingsHelper.ExtractNamespacesFromContent(content);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("System", result);
        Assert.Contains("System.Text", result);
    }

    [Fact]
    public void ExtractNamespacesFromContent_IgnoresBlankLines()
    {
        // Arrange
        var content = @"global using System;

global using System.Text;

global using Atc.Helpers;";

        // Act
        var result = GlobalUsingsHelper.ExtractNamespacesFromContent(content);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ExtractNamespacesFromContent_HandlesDuplicates()
    {
        // Arrange
        var content = @"global using System;
global using System;
global using System.Text;
global using System.Text;";

        // Act
        var result = GlobalUsingsHelper.ExtractNamespacesFromContent(content);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("System", result);
        Assert.Contains("System.Text", result);
    }

    [Fact]
    public void MergeNamespaces_MergesAndDeduplicates()
    {
        // Arrange
        var required = new List<string> { "System", "System.Text", "Atc.Helpers" };
        var existing = new List<string> { "System.Text", "Microsoft.Extensions" };

        // Act
        var result = GlobalUsingsHelper.MergeNamespaces(required, existing);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Contains("System", result);
        Assert.Contains("System.Text", result);
        Assert.Contains("Atc.Helpers", result);
        Assert.Contains("Microsoft.Extensions", result);
    }

    [Fact]
    public void MergeNamespaces_RequiredTakesPrecedence()
    {
        // Arrange
        var required = new List<string> { "NewNamespace" };
        var existing = new List<string> { "OldNamespace" };

        // Act
        var result = GlobalUsingsHelper.MergeNamespaces(required, existing);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("NewNamespace", result[0]);
        Assert.Equal("OldNamespace", result[1]);
    }

    [Fact]
    public void CreateOrUpdate_CreatesNewFile()
    {
        // Arrange
        var namespaces = new List<string> { "System", "System.Text" };

        // Act
        GlobalUsingsHelper.CreateOrUpdate(workingDirectory, namespaces);

        // Assert
        var filePath = Path.Combine(workingDirectory.FullName, "GlobalUsings.cs");
        Assert.True(File.Exists(filePath));

        var content = File.ReadAllText(filePath);
        Assert.Contains("global using System;", content, StringComparison.Ordinal);
        Assert.Contains("global using System.Text;", content, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateOrUpdate_UpdatesExistingFile()
    {
        // Arrange
        var initialContent = "global using OldNamespace;";
        var filePath = Path.Combine(workingDirectory.FullName, "GlobalUsings.cs");
        File.WriteAllText(filePath, initialContent);

        var namespaces = new List<string> { "System", "NewNamespace" };

        // Act
        GlobalUsingsHelper.CreateOrUpdate(workingDirectory, namespaces);

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains("global using System;", content, StringComparison.Ordinal);
        Assert.Contains("global using NewNamespace;", content, StringComparison.Ordinal);
        Assert.Contains("global using OldNamespace;", content, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateOrUpdate_EmptyNamespaces_DoesNotCreateFile()
    {
        // Arrange
        var namespaces = new List<string>();

        // Act
        GlobalUsingsHelper.CreateOrUpdate(workingDirectory, namespaces);

        // Assert
        var filePath = Path.Combine(workingDirectory.FullName, "GlobalUsings.cs");
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void CreateOrUpdate_StringOverload_Works()
    {
        // Arrange
        var namespaces = new List<string> { "System", "System.Text" };

        // Act
        GlobalUsingsHelper.CreateOrUpdate(workingDirectory.FullName, namespaces);

        // Assert
        var filePath = Path.Combine(workingDirectory.FullName, "GlobalUsings.cs");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void ReadNamespaces_FileExists_ReturnsNamespaces()
    {
        // Arrange
        var content = @"global using System;
global using System.Text;
global using Atc.Helpers;";
        var filePath = Path.Combine(workingDirectory.FullName, "GlobalUsings.cs");
        File.WriteAllText(filePath, content);

        // Act
        var result = GlobalUsingsHelper.ReadNamespaces(workingDirectory);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("System", result);
        Assert.Contains("System.Text", result);
        Assert.Contains("Atc.Helpers", result);
    }

    [Fact]
    public void ReadNamespaces_FileDoesNotExist_ReturnsEmptyList()
    {
        // Act
        var result = GlobalUsingsHelper.ReadNamespaces(workingDirectory);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetMergedContent_MergesExistingWithRequired()
    {
        // Arrange
        var existingContent = @"global using OldNamespace;
global using System.Collections;";
        var filePath = Path.Combine(workingDirectory.FullName, "GlobalUsings.cs");
        File.WriteAllText(filePath, existingContent);

        var requiredNamespaces = new List<string> { "System", "NewNamespace" };

        // Act
        var result = GlobalUsingsHelper.GetMergedContent(
            workingDirectory,
            requiredNamespaces,
            setSystemFirst: true,
            addNamespaceSeparator: true);

        // Assert
        Assert.Contains("global using System;", result, StringComparison.Ordinal);
        Assert.Contains("global using System.Collections;", result, StringComparison.Ordinal);
        Assert.Contains("global using NewNamespace;", result, StringComparison.Ordinal);
        Assert.Contains("global using OldNamespace;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GetMergedContent_NoExistingFile_GeneratesFromRequired()
    {
        // Arrange
        var requiredNamespaces = new List<string> { "System", "System.Text" };

        // Act
        var result = GlobalUsingsHelper.GetMergedContent(
            workingDirectory,
            requiredNamespaces);

        // Assert
        Assert.Contains("global using System;", result, StringComparison.Ordinal);
        Assert.Contains("global using System.Text;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateContent_StyleCop1210_SortsCorrectly()
    {
        // Arrange - Test case from original DotnetGlobalUsingsHelper
        var namespaces = new List<string>
        {
            "Contracts.PrintPlans",
            "Contracts.Printers",
            "Contracts.Orders",
            "Contracts.PrintLines",
            "Contracts.PrintServer",
            "Contracts.Products",
            "Contracts.PrintServerTypes",
        };

        // Act
        var result = GlobalUsingsHelper.GenerateContent(namespaces, setSystemFirst: true, addNamespaceSeparator: true);

        // Assert - All Contracts.* should be in one group, sorted alphabetically
        var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(7, lines.Length);
        Assert.Equal("global using Contracts.Orders;", lines[0]);
        Assert.Equal("global using Contracts.Printers;", lines[1]);
        Assert.Equal("global using Contracts.PrintLines;", lines[2]);
        Assert.Equal("global using Contracts.PrintPlans;", lines[3]);
        Assert.Equal("global using Contracts.PrintServer;", lines[4]);
        Assert.Equal("global using Contracts.PrintServerTypes;", lines[5]);
        Assert.Equal("global using Contracts.Products;", lines[6]);
    }

    [Fact]
    public void GenerateContent_MultipleGroups_SortsAndSeparatesCorrectly()
    {
        // Arrange - Test case from original DotnetGlobalUsingsHelper
        var namespaces = new List<string>
        {
            "System.CodeDom.Compiler",
            "System.ComponentModel.DataAnnotations",
            "System.Net",
            "Atc.Rest.Results",
            "Microsoft.AspNetCore.Authorization",
            "Microsoft.AspNetCore.Http",
            "Microsoft.AspNetCore.Mvc",
            "MyProject.Api.Generated.Contracts",
            "MyProject.Api.Generated.Contracts.Users",
        };

        // Act
        var result = GlobalUsingsHelper.GenerateContent(namespaces, setSystemFirst: true, addNamespaceSeparator: true);

        // Assert - System first, then others grouped by first segment
        Assert.Contains("global using System.CodeDom.Compiler;", result, StringComparison.Ordinal);
        Assert.Contains("global using System.ComponentModel.DataAnnotations;", result, StringComparison.Ordinal);
        Assert.Contains("global using System.Net;", result, StringComparison.Ordinal);
        Assert.Contains("global using Atc.Rest.Results;", result, StringComparison.Ordinal);
        Assert.Contains("global using Microsoft.AspNetCore.Authorization;", result, StringComparison.Ordinal);
        Assert.Contains("global using MyProject.Api.Generated.Contracts;", result, StringComparison.Ordinal);

        // Verify order: System namespaces should come before Atc
        var systemIndex = result.IndexOf("global using System.CodeDom.Compiler;", StringComparison.Ordinal);
        var atcIndex = result.IndexOf("global using Atc.Rest.Results;", StringComparison.Ordinal);
        Assert.True(systemIndex < atcIndex, "System namespaces should come before Atc namespaces");
    }

    [Fact]
    public void CreateOrUpdate_WritesUtf8WithoutBom()
    {
        // Arrange
        var namespaces = new List<string> { "System" };

        // Act
        GlobalUsingsHelper.CreateOrUpdate(workingDirectory, namespaces);

        // Assert
        var filePath = Path.Combine(workingDirectory.FullName, "GlobalUsings.cs");
        var bytes = File.ReadAllBytes(filePath);

        // UTF-8 BOM is EF BB BF - verify it's NOT present
        Assert.False(
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "File should not have UTF-8 BOM");
    }
}