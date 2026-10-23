namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// The request argument of a field of a form bound field by field: as bound when optional, with a fallback when
/// required (the wire-form-binding step answered a problem when it was missing).
/// </summary>
public sealed class FormArgumentExpressionTests
{
    [Theory]
    [InlineData("name", "string?", false, false, "name")]
    [InlineData("name", "string?", true, false, "name ?? string.Empty")]
    [InlineData("count", "int?", true, true, "count!.Value")]
    [InlineData("photo", "IFormFile?", true, false, "photo!")]
    [InlineData("photos", "IFormFileCollection?", false, false, "photos?.GetFiles(\"photos\").ToList()")]
    [InlineData("photos", "IFormFileCollection?", true, false, "photos?.GetFiles(\"photos\").ToList() ?? new List<IFormFile>()")]
    [InlineData("tags", "List<string>?", true, false, "tags ?? new List<string>()")]
    [InlineData("tags", "ParsableList<string>?", true, false, "tags ?? new List<string>()")]
    public void FormArgumentExpression_IsTheBoundValue_WithAFallbackWhenRequired(
        string name,
        string typeName,
        bool isRequired,
        bool isValueType,
        string expected)
        => Assert.Equal(
            expected,
            EndpointDefinitionExtractor.FormArgumentExpression(new FormField(name, "Prop", typeName, isRequired, isValueType, name, JsonValue: null)));

    [Theory]
    [InlineData(false, "WireBindingExtensions.FormPart<Meta>(httpContext, \"meta\")")]
    [InlineData(true, "WireBindingExtensions.FormPart<Meta>(httpContext, \"meta\")!")]
    public void FormArgumentExpression_AnObjectPart_IsAsTheBindingStepReadIt(
        bool isRequired,
        string expected)
        => Assert.Equal(
            expected,
            EndpointDefinitionExtractor.FormArgumentExpression(new FormField(string.Empty, "Meta", "Meta", isRequired, IsValueType: false, "meta", "WireBindingExtensions.FormPart<Meta>(httpContext, \"meta\")")));
}