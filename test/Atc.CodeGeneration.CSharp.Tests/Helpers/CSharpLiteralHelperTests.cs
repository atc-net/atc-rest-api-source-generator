namespace Atc.CodeGeneration.CSharp.Tests.Helpers;

public class CSharpLiteralHelperTests
{
    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("C:\\temp\\notes", "\"C:\\\\temp\\\\notes\"")]
    [InlineData("line1\nline2", "\"line1\\nline2\"")]
    [InlineData("line1\r\nline2", "\"line1\\r\\nline2\"")]
    [InlineData("tab\there", "\"tab\\there\"")]
    [InlineData("nul\0", "\"nul\\0\"")]
    [InlineData("bell\u0007", "\"bell\\u0007\"")]
    [InlineData("sep\u2028", "\"sep\\u2028\"")]
    [InlineData("", "\"\"")]
    public void ToStringLiteral_EscapesEverythingAStringLiteralCannotHold(
        string value,
        string expected)
        => Assert.Equal(expected, CSharpLiteralHelper.ToStringLiteral(value));

    [Theory]
    [InlineData("a < b && c > d", "a &lt; b &amp;&amp; c > d")]
    [InlineData("<b>bold</b>", "&lt;b>bold&lt;/b>")]
    [InlineData("<c>code</c> and <see cref=\"X\"/> stay", "<c>code</c> and <see cref=\"X\"/> stay")]
    [InlineData("already &lt; and &amp; escaped", "already &lt; and &amp; escaped")]
    [InlineData("plain \"text\" \\ */", "plain \"text\" \\ */")]
    public void EscapeXmlDoc_EscapesWhatIsNotDocumentationMarkup(
        string value,
        string expected)
        => Assert.Equal(expected, CSharpLiteralHelper.EscapeXmlDoc(value));

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("it's", "Its")]
    [InlineData("say \"hi\"", "SayHi")]
    [InlineData("back\\slash", "BackSlash")]
    [InlineData("in-progress", "InProgress")]
    [InlineData("a/b+c", "ABC")]
    [InlineData("1.0", "Value1_0")]
    [InlineData("3d", "Value3d")]
    [InlineData("+", "Value")]
    [InlineData("class", "Class")]
    public void ToEnumMemberName_GivesAValidIdentifier(
        string value,
        string expected)
        => Assert.Equal(expected, CSharpLiteralHelper.ToEnumMemberName(value));
}