namespace Atc.Rest.Api.Generator.Tests.Helpers;

public sealed class TemplateTextTests
{
    [Fact]
    public void Lines_EndsEveryLineWithThePlatformsNewline_WhateverTheChunksHave()
    {
        var nl = Environment.NewLine;

        var text = TemplateText.Lines(["first", "second\r\nthird", string.Empty, "last"]);

        Assert.Equal($"first{nl}second{nl}third{nl}{nl}last{nl}", text);
    }
}