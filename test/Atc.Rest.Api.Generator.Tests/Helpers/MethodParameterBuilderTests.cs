namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class MethodParameterBuilderTests
{
    // ========== BuildServiceCollectionExtensionParameters Tests ==========
    [Fact]
    public void BuildServiceCollectionExtensionParameters_ReturnsSingleParameter()
    {
        // Act
        var (parameters, documentation) = MethodParameterBuilder.BuildServiceCollectionExtensionParameters();

        // Assert
        Assert.Single(parameters);
        Assert.Single(documentation);
    }

    [Fact]
    public void BuildServiceCollectionExtensionParameters_ParameterHasCorrectTypeName()
    {
        // Act
        var (parameters, _) = MethodParameterBuilder.BuildServiceCollectionExtensionParameters();

        // Assert
        Assert.Equal("this IServiceCollection", parameters[0].TypeName);
    }

    [Fact]
    public void BuildServiceCollectionExtensionParameters_ParameterNameIsServices()
    {
        // Act
        var (parameters, documentation) = MethodParameterBuilder.BuildServiceCollectionExtensionParameters();

        // Assert
        Assert.Equal("services", parameters[0].Name);
        Assert.True(documentation.ContainsKey("services"));
    }

    // ========== BuildWebApplicationExtensionParameters Tests ==========
    [Fact]
    public void BuildWebApplicationExtensionParameters_ReturnsSingleParameter()
    {
        // Act
        var (parameters, documentation) = MethodParameterBuilder.BuildWebApplicationExtensionParameters();

        // Assert
        Assert.Single(parameters);
        Assert.Single(documentation);
    }

    [Fact]
    public void BuildWebApplicationExtensionParameters_ParameterHasCorrectTypeName()
    {
        // Act
        var (parameters, _) = MethodParameterBuilder.BuildWebApplicationExtensionParameters();

        // Assert
        Assert.Equal("this WebApplication", parameters[0].TypeName);
        Assert.Equal("app", parameters[0].Name);
    }

    // ========== BuildEndpointRouteBuilderExtensionParameters Tests ==========
    [Fact]
    public void BuildEndpointRouteBuilderExtensionParameters_ReturnsSingleParameter()
    {
        // Act
        var (parameters, documentation) = MethodParameterBuilder.BuildEndpointRouteBuilderExtensionParameters();

        // Assert
        Assert.Single(parameters);
        Assert.Single(documentation);
    }

    [Fact]
    public void BuildEndpointRouteBuilderExtensionParameters_ParameterHasCorrectTypeName()
    {
        // Act
        var (parameters, _) = MethodParameterBuilder.BuildEndpointRouteBuilderExtensionParameters();

        // Assert
        Assert.Equal("this IEndpointRouteBuilder", parameters[0].TypeName);
        Assert.Equal("endpoints", parameters[0].Name);
    }

    // ========== CreateParameter Tests ==========
    [Fact]
    public void CreateParameter_BasicType_ReturnsCorrectParameter()
    {
        // Act
        var result = MethodParameterBuilder.CreateParameter("string", "name");

        // Assert
        Assert.Equal("string", result.TypeName);
        Assert.Equal("name", result.Name);
        Assert.False(result.IsNullableType);
        Assert.Null(result.DefaultValue);
    }

    [Fact]
    public void CreateParameter_WithExtensionFlag_PrependsThis()
    {
        // Act
        var result = MethodParameterBuilder.CreateParameter("IServiceCollection", "services", isExtensionParameter: true);

        // Assert
        Assert.Equal("this IServiceCollection", result.TypeName);
    }

    [Fact]
    public void CreateParameter_WithoutExtensionFlag_NoThisPrefix()
    {
        // Act
        var result = MethodParameterBuilder.CreateParameter("IServiceCollection", "services", isExtensionParameter: false);

        // Assert
        Assert.Equal("IServiceCollection", result.TypeName);
    }

    [Fact]
    public void CreateParameter_NullableParameter_SetsFlag()
    {
        // Act
        var result = MethodParameterBuilder.CreateParameter("string", "name", isNullable: true);

        // Assert
        Assert.True(result.IsNullableType);
    }

    [Fact]
    public void CreateParameter_WithDefaultValue_SetsDefaultValue()
    {
        // Act
        var result = MethodParameterBuilder.CreateParameter("int", "count", defaultValue: "0");

        // Assert
        Assert.Equal("0", result.DefaultValue);
    }

    [Fact]
    public void CreateParameter_ReferenceType_SetsIsReferenceType()
    {
        // Act
        var result = MethodParameterBuilder.CreateParameter("MyClass", "instance");

        // Assert
        Assert.True(result.IsReferenceType);
    }

    // ========== All Builders Share Common Properties ==========
    [Fact]
    public void AllBuilders_ParametersHaveNullAttributes()
    {
        // Arrange
        var (sc, _) = MethodParameterBuilder.BuildServiceCollectionExtensionParameters();
        var (wa, _) = MethodParameterBuilder.BuildWebApplicationExtensionParameters();
        var (erb, _) = MethodParameterBuilder.BuildEndpointRouteBuilderExtensionParameters();

        // Assert
        Assert.Null(sc[0].Attributes);
        Assert.Null(wa[0].Attributes);
        Assert.Null(erb[0].Attributes);
    }

    [Fact]
    public void AllBuilders_ParametersAreReferenceTypes()
    {
        // Arrange
        var (sc, _) = MethodParameterBuilder.BuildServiceCollectionExtensionParameters();
        var (wa, _) = MethodParameterBuilder.BuildWebApplicationExtensionParameters();
        var (erb, _) = MethodParameterBuilder.BuildEndpointRouteBuilderExtensionParameters();

        // Assert
        Assert.True(sc[0].IsReferenceType);
        Assert.True(wa[0].IsReferenceType);
        Assert.True(erb[0].IsReferenceType);
    }
}