namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class MethodParameterBuilderTests
{
    // ========== BuildServiceCollectionExtensionParameters Tests ==========
    [Fact]
    public void BuildServiceCollectionExtensionParameters_ReturnsSingleParameter()
    {
        var (parameters, documentation) = MethodParameterBuilder.BuildServiceCollectionExtensionParameters();

        Assert.Single(parameters);
        Assert.Single(documentation);
    }

    [Fact]
    public void BuildServiceCollectionExtensionParameters_ParameterHasCorrectTypeName()
    {
        var (parameters, _) = MethodParameterBuilder.BuildServiceCollectionExtensionParameters();

        Assert.Equal("this IServiceCollection", parameters[0].TypeName);
    }

    [Fact]
    public void BuildServiceCollectionExtensionParameters_ParameterNameIsServices()
    {
        var (parameters, documentation) = MethodParameterBuilder.BuildServiceCollectionExtensionParameters();

        Assert.Equal("services", parameters[0].Name);
        Assert.True(documentation.ContainsKey("services"));
    }

    // ========== BuildWebApplicationExtensionParameters Tests ==========
    [Fact]
    public void BuildWebApplicationExtensionParameters_ReturnsSingleParameter()
    {
        var (parameters, documentation) = MethodParameterBuilder.BuildWebApplicationExtensionParameters();

        Assert.Single(parameters);
        Assert.Single(documentation);
    }

    [Fact]
    public void BuildWebApplicationExtensionParameters_ParameterHasCorrectTypeName()
    {
        var (parameters, _) = MethodParameterBuilder.BuildWebApplicationExtensionParameters();

        Assert.Equal("this WebApplication", parameters[0].TypeName);
        Assert.Equal("app", parameters[0].Name);
    }

    // ========== BuildEndpointRouteBuilderExtensionParameters Tests ==========
    [Fact]
    public void BuildEndpointRouteBuilderExtensionParameters_ReturnsSingleParameter()
    {
        var (parameters, documentation) = MethodParameterBuilder.BuildEndpointRouteBuilderExtensionParameters();

        Assert.Single(parameters);
        Assert.Single(documentation);
    }

    [Fact]
    public void BuildEndpointRouteBuilderExtensionParameters_ParameterHasCorrectTypeName()
    {
        var (parameters, _) = MethodParameterBuilder.BuildEndpointRouteBuilderExtensionParameters();

        Assert.Equal("this IEndpointRouteBuilder", parameters[0].TypeName);
        Assert.Equal("endpoints", parameters[0].Name);
    }

    // ========== CreateParameter Tests ==========
    [Fact]
    public void CreateParameter_BasicType_ReturnsCorrectParameter()
    {
        var result = MethodParameterBuilder.CreateParameter("string", "name");

        Assert.Equal("string", result.TypeName);
        Assert.Equal("name", result.Name);
        Assert.False(result.IsNullableType);
        Assert.Null(result.DefaultValue);
    }

    [Fact]
    public void CreateParameter_WithExtensionFlag_PrependsThis()
    {
        var result = MethodParameterBuilder.CreateParameter("IServiceCollection", "services", isExtensionParameter: true);

        Assert.Equal("this IServiceCollection", result.TypeName);
    }

    [Fact]
    public void CreateParameter_WithoutExtensionFlag_NoThisPrefix()
    {
        var result = MethodParameterBuilder.CreateParameter("IServiceCollection", "services", isExtensionParameter: false);

        Assert.Equal("IServiceCollection", result.TypeName);
    }

    [Fact]
    public void CreateParameter_NullableParameter_SetsFlag()
    {
        var result = MethodParameterBuilder.CreateParameter("string", "name", isNullable: true);

        Assert.True(result.IsNullableType);
    }

    [Fact]
    public void CreateParameter_WithDefaultValue_SetsDefaultValue()
    {
        var result = MethodParameterBuilder.CreateParameter("int", "count", defaultValue: "0");

        Assert.Equal("0", result.DefaultValue);
    }

    [Fact]
    public void CreateParameter_ReferenceType_SetsIsReferenceType()
    {
        var result = MethodParameterBuilder.CreateParameter("MyClass", "instance");

        Assert.True(result.IsReferenceType);
    }

    // ========== All Builders Share Common Properties ==========
    [Fact]
    public void AllBuilders_ParametersHaveNullAttributes()
    {
        var (sc, _) = MethodParameterBuilder.BuildServiceCollectionExtensionParameters();
        var (wa, _) = MethodParameterBuilder.BuildWebApplicationExtensionParameters();
        var (erb, _) = MethodParameterBuilder.BuildEndpointRouteBuilderExtensionParameters();

        Assert.Null(sc[0].Attributes);
        Assert.Null(wa[0].Attributes);
        Assert.Null(erb[0].Attributes);
    }

    [Fact]
    public void AllBuilders_ParametersAreReferenceTypes()
    {
        var (sc, _) = MethodParameterBuilder.BuildServiceCollectionExtensionParameters();
        var (wa, _) = MethodParameterBuilder.BuildWebApplicationExtensionParameters();
        var (erb, _) = MethodParameterBuilder.BuildEndpointRouteBuilderExtensionParameters();

        Assert.True(sc[0].IsReferenceType);
        Assert.True(wa[0].IsReferenceType);
        Assert.True(erb[0].IsReferenceType);
    }
}