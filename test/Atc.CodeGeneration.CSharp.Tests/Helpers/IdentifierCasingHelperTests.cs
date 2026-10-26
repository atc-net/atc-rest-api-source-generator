namespace Atc.CodeGeneration.CSharp.Tests.Helpers;

/// <summary>
/// Case mapping follows the platform's Unicode data (ICU on Linux), which can be newer than the runtime's: a letter can
/// map to one the runtime does not know as a letter yet. U+0378 is unassigned, so no runtime knows it as a letter - it
/// stands in for such a newer letter here, on any platform.
/// </summary>
public class IdentifierCasingHelperTests
{
    private const char Unassigned = '͸';

    [Fact]
    public void Keep_ALetterCasedToNoLetterHere_KeepsTheLetter()
        => Assert.Equal('a', IdentifierCasingHelper.Keep('a', Unassigned));

    [Theory]
    [InlineData('a', 'A', 'A')]
    [InlineData('Ä', 'ä', 'ä')]
    [InlineData('1', '1', '1')]
    [InlineData('_', '_', '_')]
    public void Keep_ACasedLetterOrANonLetter_TakesTheCasedOne(
        char original,
        char cased,
        char expected)
        => Assert.Equal(expected, IdentifierCasingHelper.Keep(original, cased));

    [Fact]
    public void Keep_AStringWithALetterCasedToNoLetterHere_KeepsThatLetterOnly()
        => Assert.Equal("Ab_C", IdentifierCasingHelper.Keep("ab_c", "A" + Unassigned + "_C"));

    // Deseret: a letter outside the Basic Multilingual Plane is two code units, cased together
    [Fact]
    public void Keep_AStringWithASurrogatePair_CasesThePairAsOneLetter()
        => Assert.Equal("\U00010428x", IdentifierCasingHelper.Keep("\U00010400X", "\U00010428x"));

    [Theory]
    [InlineData("pet", "Pet", "pet")]
    [InlineData("Pet", "Pet", "pet")]
    [InlineData("ä", "Ä", "ä")]
    [InlineData("", "", "")]
    [InlineData("1st", "1st", "1st")]
    public void ToUpperFirstAndToLowerFirst_CaseTheFirstCharacterOnly(
        string value,
        string upperFirst,
        string lowerFirst)
    {
        Assert.Equal(upperFirst, IdentifierCasingHelper.ToUpperFirst(value));
        Assert.Equal(lowerFirst, IdentifierCasingHelper.ToLowerFirst(value));
    }

    [Theory]
    [InlineData("PetStore", "PETSTORE", "petstore")]
    [InlineData("\U00010400", "\U00010400", "\U00010428")]
    public void ToUpperAndToLower_CaseTheWholeText(
        string value,
        string upper,
        string lower)
    {
        Assert.Equal(upper, IdentifierCasingHelper.ToUpper(value));
        Assert.Equal(lower, IdentifierCasingHelper.ToLower(value));
    }

    [Fact]
    public void ToUpperAndToLower_OfAChar_CaseIt()
    {
        Assert.Equal('P', IdentifierCasingHelper.ToUpper('p'));
        Assert.Equal('p', IdentifierCasingHelper.ToLower('P'));
    }
}