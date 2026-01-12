namespace Atc.Rest.Api.Generator.Tests.Validators;

public class CasingHelperTests
{
    // ========== IsCamelCase Tests ==========
    [Theory]
    [InlineData("listPets", true)]
    [InlineData("getPetById", true)]
    [InlineData("createNewUser", true)]
    [InlineData("a", true)]
    [InlineData("abc123", true)]
    [InlineData("ListPets", false)] // PascalCase
    [InlineData("list-pets", false)] // kebab-case
    [InlineData("list_pets", false)] // snake_case
    [InlineData("LIST_PETS", false)] // UPPER_SNAKE_CASE
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsCamelCase_ReturnsExpectedResult(
        string? value,
        bool expected)
    {
        var result = CasingHelper.IsCamelCase(value);
        Assert.Equal(expected, result);
    }

    // ========== IsPascalCase Tests ==========
    [Theory]
    [InlineData("ListPets", true)]
    [InlineData("GetPetById", true)]
    [InlineData("CreateNewUser", true)]
    [InlineData("A", true)]
    [InlineData("Abc123", true)]
    [InlineData("listPets", false)] // camelCase
    [InlineData("list-pets", false)] // kebab-case
    [InlineData("list_pets", false)] // snake_case
    [InlineData("LIST_PETS", false)] // UPPER_SNAKE_CASE
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPascalCase_ReturnsExpectedResult(
        string? value,
        bool expected)
    {
        var result = CasingHelper.IsPascalCase(value);
        Assert.Equal(expected, result);
    }

    // ========== IsKebabCase Tests ==========
    [Theory]
    [InlineData("list-pets", true)]
    [InlineData("get-pet-by-id", true)]
    [InlineData("create-new-user", true)]
    [InlineData("a", true)]
    [InlineData("abc-123", true)]
    [InlineData("listPets", false)] // camelCase
    [InlineData("ListPets", false)] // PascalCase
    [InlineData("list_pets", false)] // snake_case
    [InlineData("LIST_PETS", false)] // UPPER_SNAKE_CASE
    [InlineData("list--pets", false)] // consecutive hyphens
    [InlineData("list-pets-", false)] // trailing hyphen
    [InlineData("-list-pets", false)] // leading hyphen
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKebabCase_ReturnsExpectedResult(
        string? value,
        bool expected)
    {
        var result = CasingHelper.IsKebabCase(value);
        Assert.Equal(expected, result);
    }

    // ========== IsSnakeCase Tests ==========
    [Theory]
    [InlineData("list_pets", true)]
    [InlineData("get_pet_by_id", true)]
    [InlineData("create_new_user", true)]
    [InlineData("a", true)]
    [InlineData("abc_123", true)]
    [InlineData("listPets", false)] // camelCase
    [InlineData("ListPets", false)] // PascalCase
    [InlineData("list-pets", false)] // kebab-case
    [InlineData("LIST_PETS", false)] // UPPER_SNAKE_CASE
    [InlineData("list__pets", false)] // consecutive underscores
    [InlineData("list_pets_", false)] // trailing underscore
    [InlineData("_list_pets", false)] // leading underscore
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSnakeCase_ReturnsExpectedResult(
        string? value,
        bool expected)
    {
        var result = CasingHelper.IsSnakeCase(value);
        Assert.Equal(expected, result);
    }

    // ========== IsUpperSnakeCase Tests ==========
    [Theory]
    [InlineData("LIST_PETS", true)]
    [InlineData("GET_PET_BY_ID", true)]
    [InlineData("CREATE_NEW_USER", true)]
    [InlineData("A", true)]
    [InlineData("ABC_123", true)]
    [InlineData("listPets", false)] // camelCase
    [InlineData("ListPets", false)] // PascalCase
    [InlineData("list-pets", false)] // kebab-case
    [InlineData("list_pets", false)] // snake_case
    [InlineData("LIST__PETS", false)] // consecutive underscores
    [InlineData("LIST_PETS_", false)] // trailing underscore
    [InlineData("_LIST_PETS", false)] // leading underscore
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsUpperSnakeCase_ReturnsExpectedResult(
        string? value,
        bool expected)
    {
        var result = CasingHelper.IsUpperSnakeCase(value);
        Assert.Equal(expected, result);
    }

    // ========== IsValidOperationIdCasing Tests ==========
    [Theory]
    [InlineData("listPets", true)] // camelCase is valid
    [InlineData("list-pets", true)] // kebab-case is valid
    [InlineData("ListPets", false)] // PascalCase is NOT valid
    [InlineData("list_pets", false)] // snake_case is NOT valid
    [InlineData("LIST_PETS", false)] // UPPER_SNAKE_CASE is NOT valid
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidOperationIdCasing_ReturnsExpectedResult(
        string? value,
        bool expected)
    {
        var result = CasingHelper.IsValidOperationIdCasing(value);
        Assert.Equal(expected, result);
    }

    // ========== GetDetectedCasingStyle Tests ==========
    [Theory]
    [InlineData("listPets", "camelCase")]
    [InlineData("list-pets", "kebab-case")]
    [InlineData("ListPets", "PascalCase")]
    [InlineData("list_pets", "snake_case")]
    [InlineData("LIST_PETS", "UPPER_SNAKE_CASE")]
    [InlineData("", "empty")]
    [InlineData(null, "empty")]
    public void GetDetectedCasingStyle_ReturnsExpectedStyle(
        string? value,
        string expected)
    {
        var result = CasingHelper.GetDetectedCasingStyle(value);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetDetectedCasingStyle_MixedWithHyphens_ReturnsMixed()
    {
        var result = CasingHelper.GetDetectedCasingStyle("List-Pets");
        Assert.Equal("mixed (contains hyphens)", result);
    }

    [Fact]
    public void GetDetectedCasingStyle_MixedWithUnderscores_ReturnsMixed()
    {
        var result = CasingHelper.GetDetectedCasingStyle("List_Pets");
        Assert.Equal("mixed (contains underscores)", result);
    }

    // ========== SuggestCamelCase Tests ==========
    [Theory]
    [InlineData("ListPets", "listPets")] // PascalCase to camelCase
    [InlineData("list-pets", "listPets")] // kebab-case to camelCase
    [InlineData("list_pets", "listPets")] // snake_case to camelCase
    [InlineData("LIST_PETS", "lISTPETS")] // Note: UPPER_SNAKE keeps case after first
    [InlineData("", "")]
    [InlineData(null, null)]
    public void SuggestCamelCase_ReturnsExpectedResult(
        string? value,
        string? expected)
    {
        var result = CasingHelper.SuggestCamelCase(value!);
        Assert.Equal(expected, result);
    }

    // ========== SuggestPascalCase Tests ==========
    [Theory]
    [InlineData("listPets", "ListPets")] // camelCase to PascalCase
    [InlineData("list-pets", "ListPets")] // kebab-case to PascalCase
    [InlineData("list_pets", "ListPets")] // snake_case to PascalCase
    [InlineData("", "")]
    [InlineData(null, null)]
    public void SuggestPascalCase_ReturnsExpectedResult(
        string? value,
        string? expected)
    {
        var result = CasingHelper.SuggestPascalCase(value!);
        Assert.Equal(expected, result);
    }

    // ========== SuggestKebabCase Tests ==========
    [Theory]
    [InlineData("listPets", "list-pets")] // camelCase to kebab-case
    [InlineData("ListPets", "list-pets")] // PascalCase to kebab-case
    [InlineData("list_pets", "list-pets")] // snake_case to kebab-case
    [InlineData("LIST_PETS", "list-pets")] // Consecutive uppercase treated as single word
    [InlineData("XMLParser", "xml-parser")] // Acronym followed by word
    [InlineData("QW IoT Nexus", "qw-iot-nexus")] // Consecutive uppercase with space and acronym
    [InlineData("Contoso IoT Nexus", "contoso-iot-nexus")] // IoT acronym preserved
    [InlineData("IoT Device", "iot-device")] // IoT at start
    [InlineData("MyAPI", "my-api")] // API acronym preserved
    [InlineData("OAuth Token", "oauth-token")] // OAuth acronym preserved
    [InlineData("OpenID Connect", "openid-connect")] // OpenID acronym preserved
    [InlineData("GraphQL Server", "graphql-server")] // GraphQL acronym preserved
    [InlineData("", "")]
    [InlineData(null, null)]
    public void SuggestKebabCase_ReturnsExpectedResult(
        string? value,
        string? expected)
    {
        var result = CasingHelper.SuggestKebabCase(value!);
        Assert.Equal(expected, result);
    }

    // ========== ToPascalCase Tests ==========
    [Theory]
    [InlineData("listPets", "ListPets")]
    [InlineData("list-pets", "ListPets")]
    [InlineData("list_pets", "ListPets")]
    [InlineData("my.pet.store", "MyPetStore")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void ToPascalCase_ReturnsExpectedResult(
        string? value,
        string? expected)
    {
        var result = CasingHelper.ToPascalCase(value!);
        Assert.Equal(expected, result);
    }

    // ========== GetLastNameSegment Tests ==========
    [Theory]
    [InlineData("MyCompany.PowerController.HostAgent", "HostAgent")]
    [InlineData("Linksoft.PowerController.HostAgent", "HostAgent")]
    [InlineData("MyProject", "MyProject")]
    [InlineData("Company.Product", "Product")]
    [InlineData("A.B.C.D.E", "E")]
    [InlineData("", "Assembly")]
    [InlineData(null, "Assembly")]
    public void GetLastNameSegment_ReturnsExpectedResult(
        string? value,
        string expected)
    {
        var result = CasingHelper.GetLastNameSegment(value);
        Assert.Equal(expected, result);
    }
}