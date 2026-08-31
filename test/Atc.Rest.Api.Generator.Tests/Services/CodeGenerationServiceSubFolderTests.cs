namespace Atc.Rest.Api.Generator.Tests.Services;

/// <summary>
/// Tests for granularity-aware sub-folder placement in <see cref="CodeGenerationService.GetSubFolder"/>.
/// </summary>
/// <remarks>
/// Under <see cref="ClientGranularityType.PerArea"/> generated client files are grouped into
/// <c>Contracts\{Area}</c> and <c>Endpoints\{Area}</c>. Under <see cref="ClientGranularityType.Single"/>
/// there is exactly one client covering every path segment, so the per-area folder segment is
/// meaningless and is dropped to give a flat layout matching the flat
/// <c>{root}.Generated.Models</c> namespace.
/// <para>
/// The PerArea cases are load-bearing for backward compatibility: they pin the layout that the
/// existing snapshot baseline depends on, so granularity can never silently alter it.
/// </para>
/// </remarks>
public class CodeGenerationServiceSubFolderTests
{
    // ========== PerArea: existing layout must be preserved ==========
    [Theory]
    [InlineData("Models", "Accounts", @"Contracts\Accounts")]
    [InlineData("Parameters", "Accounts", @"Contracts\Accounts/RequestParameters")]
    [InlineData("Client", "Accounts", @"Endpoints\Accounts")]
    public void GetSubFolder_PerArea_KeepsAreaSegment(
        string category,
        string groupName,
        string expected)
    {
        // Act
        var actual = CodeGenerationService.GetSubFolder(
            category,
            groupName,
            CodeGenerationService.GeneratorType.Client,
            ClientGranularityType.PerArea);

        // Assert
        Assert.Equal(expected, actual, StringComparer.Ordinal);
    }

    /// <summary>
    /// An absent group name falls back to <c>Common</c> under PerArea.
    /// </summary>
    [Fact]
    public void GetSubFolder_PerAreaWithoutGroupName_FallsBackToCommon()
    {
        // Act
        var actual = CodeGenerationService.GetSubFolder(
            "Client",
            groupName: null,
            CodeGenerationService.GeneratorType.Client,
            ClientGranularityType.PerArea);

        // Assert
        Assert.Equal(@"Endpoints\Common", actual, StringComparer.Ordinal);
    }

    /// <summary>
    /// PerArea is the default, so omitting the argument must not change behaviour.
    /// </summary>
    [Fact]
    public void GetSubFolder_GranularityOmitted_DefaultsToPerArea()
    {
        // Act
        var actual = CodeGenerationService.GetSubFolder(
            "Models",
            "Orders",
            CodeGenerationService.GeneratorType.Client);

        // Assert
        Assert.Equal(@"Contracts\Orders", actual, StringComparer.Ordinal);
    }

    // ========== Single: flat layout ==========
    [Theory]
    [InlineData("Models", "Contracts")]
    [InlineData("Parameters", "Contracts/RequestParameters")]
    [InlineData("Client", "Endpoints")]
    public void GetSubFolder_Single_DropsAreaSegment(
        string category,
        string expected)
    {
        // Act
        var actual = CodeGenerationService.GetSubFolder(
            category,
            "Accounts",
            CodeGenerationService.GeneratorType.Client,
            ClientGranularityType.Single);

        // Assert
        Assert.Equal(expected, actual, StringComparer.Ordinal);
    }

    /// <summary>
    /// In Single mode every area collapses to the same folder, which is the whole point of the mode.
    /// </summary>
    [Fact]
    public void GetSubFolder_Single_IsIndependentOfGroupName()
    {
        // Act
        var accounts = CodeGenerationService.GetSubFolder(
            "Models",
            "Accounts",
            CodeGenerationService.GeneratorType.Client,
            ClientGranularityType.Single);

        var orders = CodeGenerationService.GetSubFolder(
            "Models",
            "Orders",
            CodeGenerationService.GeneratorType.Client,
            ClientGranularityType.Single);

        // Assert
        Assert.Equal(accounts, orders, StringComparer.Ordinal);
    }

    // ========== Single must not leak into other generator types ==========

    /// <summary>
    /// Client granularity is a client-only concept; server output must be unaffected.
    /// </summary>
    [Fact]
    public void GetSubFolder_SingleForServer_IsUnchanged()
    {
        // Act
        var actual = CodeGenerationService.GetSubFolder(
            "Models",
            "Accounts",
            CodeGenerationService.GeneratorType.Server,
            ClientGranularityType.Single);

        // Assert
        Assert.Equal(@"Contracts\Accounts/Models", actual, StringComparer.Ordinal);
    }
}