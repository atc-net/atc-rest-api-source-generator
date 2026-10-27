namespace Atc.Rest.Api.Generator.Tests.Extensions;

public sealed class MinimalApiPackageModeExtensionsTests
{
    [Theory]
    [InlineData(MinimalApiPackageMode.Enabled, true, true, false)]
    [InlineData(MinimalApiPackageMode.Enabled, false, true, true)]
    [InlineData(MinimalApiPackageMode.Disabled, true, false, false)]
    [InlineData(MinimalApiPackageMode.Disabled, false, false, false)]
    [InlineData(MinimalApiPackageMode.Auto, true, true, false)]
    [InlineData(MinimalApiPackageMode.Auto, false, false, false)]
    public void UsesPackage_And_MissesPackage_FollowTheModeAndTheReference(
        MinimalApiPackageMode mode,
        bool packageReferenced,
        bool usesPackage,
        bool missesPackage)
    {
        Assert.Equal(usesPackage, mode.UsesPackage(packageReferenced));
        Assert.Equal(missesPackage, mode.MissesPackage(packageReferenced));
    }
}