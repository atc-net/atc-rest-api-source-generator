namespace Atc.CodeGeneration.TypeScript.Tests.Content.Factories;

public class TypeScriptFactoryTests
{
    [Fact]
    public void ClassFactory_Create_DefaultModifiers_SetsExport()
    {
        // Arrange & Act
        var result = TypeScriptClassParametersFactory.Create(
            headerContent: null,
            documentationTags: null,
            typeName: "PetService");

        // Assert
        Assert.Equal("PetService", result.TypeName);
        Assert.Equal(TypeScriptModifiers.Export, result.Modifiers);
        Assert.Null(result.ExtendsTypeName);
    }

    [Fact]
    public void ClassFactory_CreateAbstract_SetsExportAndAbstract()
    {
        // Arrange & Act
        var result = TypeScriptClassParametersFactory.CreateAbstract(
            headerContent: null,
            documentationTags: null,
            typeName: "BaseService");

        // Assert
        Assert.Equal("BaseService", result.TypeName);
        Assert.Equal(TypeScriptModifiers.Export | TypeScriptModifiers.Abstract, result.Modifiers);
    }

    [Fact]
    public void EnumFactory_CreateFromNames_ProducesValuesWithNullValue()
    {
        // Arrange
        var names = new List<string> { "Active", "Inactive", "Pending" };

        // Act
        var result = TypeScriptEnumParametersFactory.CreateFromNames(
            headerContent: null,
            documentationTags: null,
            typeName: "Status",
            names: names);

        // Assert
        Assert.Equal("Status", result.TypeName);
        Assert.Equal(TypeScriptModifiers.Export, result.Modifiers);
        Assert.Equal(3, result.Values.Count);
        Assert.All(result.Values, v => Assert.Null(v.Value));
        Assert.Equal("Active", result.Values[0].Name);
    }

    [Fact]
    public void EnumFactory_CreateFromNameValuePairs_PreservesValues()
    {
        // Arrange
        var pairs = new List<KeyValuePair<string, string>>
        {
            new("Small", "'S'"),
            new("Medium", "'M'"),
            new("Large", "'L'"),
        };

        // Act
        var result = TypeScriptEnumParametersFactory.CreateFromNameValuePairs(
            headerContent: null,
            documentationTags: null,
            typeName: "Size",
            nameValuePairs: pairs);

        // Assert
        Assert.Equal("Size", result.TypeName);
        Assert.Equal(3, result.Values.Count);
        Assert.Equal("'S'", result.Values[0].Value);
        Assert.Equal("'L'", result.Values[2].Value);
    }

    [Fact]
    public void InterfaceFactory_Create_DefaultModifiers_SetsExport()
    {
        // Arrange
        var properties = new List<TypeScriptPropertyParameters>
        {
            new(null, false, "string", false, "name", null),
            new(null, false, "number", true, "age", null),
        };

        // Act
        var result = TypeScriptInterfaceParametersFactory.Create(
            headerContent: null,
            documentationTags: null,
            typeName: "IPet",
            properties: properties);

        // Assert
        Assert.Equal("IPet", result.TypeName);
        Assert.Equal(TypeScriptModifiers.Export, result.Modifiers);
        Assert.NotNull(result.Properties);
        Assert.Equal(2, result.Properties.Count);
        Assert.True(result.Properties[1].IsOptional);
    }

    [Fact]
    public void InterfaceFactory_CreateWithExtends_SetsExtendsTypeName()
    {
        // Arrange & Act
        var result = TypeScriptInterfaceParametersFactory.Create(
            headerContent: null,
            documentationTags: null,
            typeName: "IDog",
            extendsTypeName: "IAnimal");

        // Assert
        Assert.Equal("IDog", result.TypeName);
        Assert.Equal("IAnimal", result.ExtendsTypeName);
    }
}