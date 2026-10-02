namespace Atc.CodeGeneration.TypeScript.Tests.Helpers;

public class TypeScriptLiteralHelperTests
{
    [Theory]
    [InlineData("plain", "'plain'")]
    [InlineData("it's", @"'it\'s'")]
    [InlineData("say \"hi\"", "'say \"hi\"'")]
    [InlineData(@"back\slash", @"'back\\slash'")]
    [InlineData("line1\nline2", @"'line1\nline2'")]
    [InlineData("", "''")]
    public void ToStringLiteral_EscapesWhatASingleQuotedStringCannotHold(
        string value,
        string expected)
        => Assert.Equal(expected, TypeScriptLiteralHelper.ToStringLiteral(value));

    [Theory]
    [InlineData("ends */ here", @"ends *\/ here")]
    [InlineData("*/ export const injected = 1; /*", @"*\/ export const injected = 1; /*")]
    [InlineData("plain <b> & \"text\"", "plain <b> & \"text\"")]
    public void EscapeComment_NeverEndsTheComment(
        string value,
        string expected)
        => Assert.Equal(expected, TypeScriptLiteralHelper.EscapeComment(value));

    [Theory]
    [InlineData("Plain", "Plain")]
    [InlineData("It's", "Its")]
    [InlineData("Say \"hi\"", "SayHi")]
    [InlineData(@"Back\slash", "BackSlash")]
    [InlineData("1st", "Value1st")]
    [InlineData("1.0", "Value1_0")]
    [InlineData("+", "Value")]
    public void ToEnumMemberName_GivesAValidIdentifier(
        string value,
        string expected)
        => Assert.Equal(expected, TypeScriptLiteralHelper.ToEnumMemberName(value));
}