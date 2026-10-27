namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class ValidatorDependencyRegistrationExtractorTests
{
    [Fact]
    public void Extract_WithValidators_RegistersSingletonsSortedByValidatorName()
    {
        var validators = new List<(string ValidatorName, string ValidatorNamespace, string ModelType)>
        {
            ("UserValidator", "My.Api.Domain.Validators", "User"),
            ("AddressValidator", "My.Api.Domain.Validators.Shared", "Address"),
        };

        var result = ValidatorDependencyRegistrationExtractor.Extract("My.Api.Domain", "My.Api.Domain", validators);

        Assert.NotNull(result);
        Assert.Equal("My.Api.Domain", result.Namespace);
        Assert.Equal("ApiValidatorServiceCollectionExtensions", result.ClassTypeName);
        Assert.Equal(DeclarationModifiers.PublicStaticClass, result.DeclarationModifier);
        Assert.Contains("using FluentValidation;", result.HeaderContent, StringComparison.Ordinal);
        Assert.Contains("using Microsoft.Extensions.DependencyInjection;", result.HeaderContent, StringComparison.Ordinal);
        Assert.Contains(result.Attributes!, a => a.Name == "GeneratedCode");

        var method = Assert.Single(result.Methods!);
        Assert.Equal("AddApiValidatorsFromDomain", method.Name);
        Assert.Equal("IServiceCollection", method.ReturnTypeName);
        Assert.Equal("this IServiceCollection", Assert.Single(method.Parameters!).TypeName);
        Assert.Equal("Registers all FluentValidation validators from this assembly.", method.DocumentationTags!.Summary);

        var content = method.Content!;
        Assert.Contains("services.AddSingleton<IValidator<Address>, My.Api.Domain.Validators.Shared.AddressValidator>();", content, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<IValidator<User>, My.Api.Domain.Validators.UserValidator>();", content, StringComparison.Ordinal);
        Assert.True(
            content.IndexOf("AddressValidator", StringComparison.Ordinal) < content.IndexOf("UserValidator", StringComparison.Ordinal),
            "Registrations are sorted by validator name");
        Assert.EndsWith("return services;", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_AssemblyNameWithoutDots_UsesWholeNameAsMethodSuffix()
    {
        var validators = new List<(string ValidatorName, string ValidatorNamespace, string ModelType)>
        {
            ("PetValidator", "PetStore.Validators", "Pet"),
        };

        var result = ValidatorDependencyRegistrationExtractor.Extract("PetStore", "PetStore", validators);

        Assert.Equal("AddApiValidatorsFromPetStore", result!.Methods![0].Name);
    }

    [Fact]
    public void Extract_NoValidators_ReturnsNull()
    {
        Assert.Null(ValidatorDependencyRegistrationExtractor.Extract("My.Api.Domain", "My.Api.Domain", []));
        Assert.Null(ValidatorDependencyRegistrationExtractor.Extract("My.Api.Domain", "My.Api.Domain", null!));
    }
}