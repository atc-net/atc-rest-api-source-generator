namespace Atc.CodeGeneration.CSharp.Tests.Content.Generators;

public class GenerateContentForClassTests
{
    [Fact]
    public void Generate_SimpleClass_ProducesClassDeclaration()
    {
        var parameters = new ClassParameters(
            HeaderContent: null,
            Namespace: "MyApp.Models",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicClass,
            ClassTypeName: "Pet",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: null,
            GenerateToStringMethod: false);

        var generator = new GenerateContentForClass(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("public class Pet", result, StringComparison.Ordinal);
        Assert.Contains("namespace MyApp.Models;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithInheritedClass_IncludesBaseClass()
    {
        var parameters = new ClassParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicClass,
            ClassTypeName: "Dog",
            GenericTypeName: null,
            InheritedClassTypeName: "Animal",
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: null,
            GenerateToStringMethod: false);

        var generator = new GenerateContentForClass(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("public class Dog : Animal", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithInterface_IncludesInterface()
    {
        var parameters = new ClassParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicClass,
            ClassTypeName: "PetService",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: "IPetService",
            Constructors: null,
            Properties: null,
            Methods: null,
            GenerateToStringMethod: false);

        var generator = new GenerateContentForClass(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("public class PetService : IPetService", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithBothBaseAndInterface_IncludesBoth()
    {
        var parameters = new ClassParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicClass,
            ClassTypeName: "Dog",
            GenericTypeName: null,
            InheritedClassTypeName: "Animal",
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: "IPet",
            Constructors: null,
            Properties: null,
            Methods: null,
            GenerateToStringMethod: false);

        var generator = new GenerateContentForClass(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("public class Dog : Animal, IPet", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithGenericType_IncludesGenericParameter()
    {
        var parameters = new ClassParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicClass,
            ClassTypeName: "Repository",
            GenericTypeName: "T",
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: null,
            GenerateToStringMethod: false);

        var generator = new GenerateContentForClass(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("public class Repository<T>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithStringConstant_WrapsValueInQuotes()
    {
        var parameters = new ClassParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicSealedClass,
            ClassTypeName: "Endpoints",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: null,
            GenerateToStringMethod: false,
            Constants:
            [
                new ConstantFieldParameters("internal", "string", "ApiRouteBase", "/api/v1/pets"),
            ]);

        var generator = new GenerateContentForClass(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("internal const string ApiRouteBase = \"/api/v1/pets\";", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithIntConstant_DoesNotWrapInQuotes()
    {
        var parameters = new ClassParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicClass,
            ClassTypeName: "Config",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: null,
            GenerateToStringMethod: false,
            Constants:
            [
                new ConstantFieldParameters("public", "int", "MaxRetries", "3"),
            ]);

        var generator = new GenerateContentForClass(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("public const int MaxRetries = 3;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\"3\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithBoolConstant_DoesNotWrapInQuotes()
    {
        var parameters = new ClassParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicClass,
            ClassTypeName: "Config",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: null,
            GenerateToStringMethod: false,
            Constants:
            [
                new ConstantFieldParameters("public", "bool", "IsEnabled", "true"),
            ]);

        var generator = new GenerateContentForClass(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("public const bool IsEnabled = true;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\"true\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithDocumentation_IncludesXmlDocs()
    {
        var parameters = new ClassParameters(
            HeaderContent: null,
            Namespace: "MyApp",
            DocumentationTags: new CodeDocumentationTags("A pet entity."),
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicClass,
            ClassTypeName: "Pet",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: null,
            GenerateToStringMethod: false);

        var generator = new GenerateContentForClass(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.Contains("/// <summary>", result, StringComparison.Ordinal);
        Assert.Contains("A pet entity.", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithHeaderContent_IncludesHeader()
    {
        var parameters = new ClassParameters(
            HeaderContent: "// Auto-generated code",
            Namespace: "MyApp",
            DocumentationTags: null,
            Attributes: null,
            DeclarationModifier: DeclarationModifiers.PublicClass,
            ClassTypeName: "Pet",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties: null,
            Methods: null,
            GenerateToStringMethod: false);

        var generator = new GenerateContentForClass(
            new CodeDocumentationTagsGenerator(),
            parameters);

        var result = generator.Generate();

        Assert.StartsWith("// Auto-generated", result, StringComparison.Ordinal);
    }
}