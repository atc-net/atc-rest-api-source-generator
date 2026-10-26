namespace Atc.CodeGeneration.TypeScript.Tests.Helpers;

/// <summary>
/// Case mapping follows the platform's Unicode data (ICU on Linux), which can be newer than the runtime's: a letter can
/// map to one the runtime does not know as a letter yet. U+0378 is unassigned, so no runtime knows it as a letter - it
/// stands in for such a newer letter here, on any platform.
/// </summary>
public class TypeScriptCasingHelperTests
{
    private const char Unassigned = '͸';

    [Fact]
    public void Keep_ALetterCasedToNoLetterHere_KeepsTheLetter()
        => Assert.Equal('a', TypeScriptCasingHelper.Keep('a', Unassigned));

    [Theory]
    [InlineData('a', 'A', 'A')]
    [InlineData('1', '1', '1')]
    public void Keep_ACasedLetterOrANonLetter_TakesTheCasedOne(
        char original,
        char cased,
        char expected)
        => Assert.Equal(expected, TypeScriptCasingHelper.Keep(original, cased));

    [Fact]
    public void Keep_AStringWithALetterCasedToNoLetterHere_KeepsThatLetterOnly()
        => Assert.Equal("Ab_C", TypeScriptCasingHelper.Keep("ab_c", "A" + Unassigned + "_C"));

    [Theory]
    [InlineData("pet", "Pet", "pet")]
    [InlineData("Pet", "Pet", "pet")]
    [InlineData("", "", "")]
    public void ToUpperFirstAndToLowerFirst_CaseTheFirstCharacterOnly(
        string value,
        string upperFirst,
        string lowerFirst)
    {
        Assert.Equal(upperFirst, TypeScriptCasingHelper.ToUpperFirst(value));
        Assert.Equal(lowerFirst, TypeScriptCasingHelper.ToLowerFirst(value));
    }

    [Fact]
    public void ToLower_CasesTheWholeText()
        => Assert.Equal("petstore", TypeScriptCasingHelper.ToLower("PetStore"));
}