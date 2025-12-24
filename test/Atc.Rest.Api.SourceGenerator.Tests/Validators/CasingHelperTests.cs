namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
public class CasingHelperTests
{
    // ========== IsCamelCase Tests ==========

    [Theory]
    [InlineData("listPets", true)]
    [InlineData("getPetById", true)]
    [InlineData("createNewUser", true)]
    [InlineData("a", true)]
    [InlineData("aBC", true)]
    [InlineData("ListPets", false)]         // PascalCase
    [InlineData("list-pets", false)]        // kebab-case
    [InlineData("list_pets", false)]        // snake_case
    [InlineData("LIST_PETS", false)]        // UPPER_SNAKE_CASE
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
    [InlineData("ABC", true)]
    [InlineData("listPets", false)]          // camelCase
    [InlineData("list-pets", false)]         // kebab-case
    [InlineData("list_pets", false)]         // snake_case
    [InlineData("LIST_PETS", false)]         // UPPER_SNAKE_CASE
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
    [InlineData("pets", true)]
    [InlineData("a", true)]
    [InlineData("listPets", false)]          // camelCase
    [InlineData("ListPets", false)]          // PascalCase
    [InlineData("list_pets", false)]         // snake_case
    [InlineData("LIST-PETS", false)]         // Contains uppercase
    [InlineData("list--pets", false)]        // Consecutive hyphens
    [InlineData("list-pets-", false)]        // Trailing hyphen
    [InlineData("-list-pets", false)]        // Leading hyphen (starts with hyphen, not letter)
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
    [InlineData("pets", true)]
    [InlineData("a", true)]
    [InlineData("listPets", false)]          // camelCase
    [InlineData("ListPets", false)]          // PascalCase
    [InlineData("list-pets", false)]         // kebab-case
    [InlineData("LIST_PETS", false)]         // UPPER_SNAKE_CASE
    [InlineData("list__pets", false)]        // Consecutive underscores
    [InlineData("list_pets_", false)]        // Trailing underscore
    [InlineData("_list_pets", false)]        // Leading underscore (starts with _, not letter)
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
    [InlineData("PETS", true)]
    [InlineData("A", true)]
    [InlineData("listPets", false)]          // camelCase
    [InlineData("ListPets", false)]          // PascalCase
    [InlineData("list-pets", false)]         // kebab-case
    [InlineData("list_pets", false)]         // snake_case
    [InlineData("LIST__PETS", false)]        // Consecutive underscores
    [InlineData("LIST_PETS_", false)]        // Trailing underscore
    [InlineData("_LIST_PETS", false)]        // Leading underscore
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
    [InlineData("listPets", true)]           // camelCase is valid
    [InlineData("list-pets", true)]          // kebab-case is valid
    [InlineData("ListPets", false)]          // PascalCase is not valid
    [InlineData("list_pets", false)]         // snake_case is not valid
    [InlineData("LIST_PETS", false)]         // UPPER_SNAKE_CASE is not valid
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
    [InlineData("ListPets", "PascalCase")]
    [InlineData("list-pets", "kebab-case")]
    [InlineData("list_pets", "snake_case")]
    [InlineData("LIST_PETS", "UPPER_SNAKE_CASE")]
    [InlineData("", "empty")]
    [InlineData(null, "empty")]
    public void GetDetectedCasingStyle_ReturnsExpectedResult(
        string? value,
        string expected)
    {
        var result = CasingHelper.GetDetectedCasingStyle(value);
        Assert.Equal(expected, result);
    }

    // ========== SuggestCamelCase Tests ==========

    [Theory]
    [InlineData("ListPets", "listPets")]
    [InlineData("list-pets", "listPets")]
    [InlineData("list_pets", "listPets")]
    [InlineData("listPets", "listPets")]     // Already camelCase
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
    [InlineData("listPets", "ListPets")]
    [InlineData("list-pets", "ListPets")]
    [InlineData("list_pets", "ListPets")]
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
    [InlineData("ListPets", "list-pets")]
    [InlineData("listPets", "list-pets")]
    [InlineData("list_pets", "list-pets")]
    [InlineData("list-pets", "list-pets")]   // Already kebab-case
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
    [InlineData("MyPetStore", "MyPetStore")]         // Already PascalCase
    [InlineData("myPetStore", "MyPetStore")]         // camelCase
    [InlineData("my-pet-store", "MyPetStore")]       // kebab-case
    [InlineData("my.pet-store", "MyPetStore")]       // Mixed separators (dot and hyphen)
    [InlineData("my-PET.store", "MyPetStore")]       // Mixed separators with ALL CAPS segment
    [InlineData("my_pet_store", "MyPetStore")]       // snake_case
    [InlineData("MY_PET_STORE", "MyPetStore")]       // UPPER_SNAKE_CASE
    [InlineData("my pet store", "MyPetStore")]       // Space separated
    [InlineData("pet", "Pet")]                       // Single lowercase word
    [InlineData("PET", "Pet")]                       // Single uppercase word
    [InlineData("Pet", "Pet")]                       // Single PascalCase word
    [InlineData("myPETStore", "MyPetStore")]         // camelCase with ALL CAPS in middle
    [InlineData("XMLParser", "XmlParser")]           // Acronym at start
    [InlineData("parseXML", "ParseXml")]             // Acronym at end (camelCase)
    [InlineData("parseXMLFile", "ParseXmlFile")]     // Acronym in middle (camelCase)
    [InlineData("", "")]                             // Empty string
    [InlineData(null, null)]                         // Null
    public void ToPascalCase_ReturnsExpectedResult(
        string? value,
        string? expected)
    {
        var result = CasingHelper.ToPascalCase(value!);
        Assert.Equal(expected, result);
    }
}