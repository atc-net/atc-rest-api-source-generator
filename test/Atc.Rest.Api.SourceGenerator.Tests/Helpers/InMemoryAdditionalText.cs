namespace Atc.Rest.Api.SourceGenerator.Tests.Helpers;

/// <summary>
/// Mock implementation of <see cref="AdditionalText"/> for testing source generators.
/// Allows feeding in-memory content as additional files to the generator.
/// </summary>
public sealed class InMemoryAdditionalText : AdditionalText
{
    private readonly string content;

    public InMemoryAdditionalText(
        string path,
        string content)
    {
        this.Path = path;
        this.content = content;
    }

    public override string Path { get; }

    public override SourceText? GetText(
        CancellationToken cancellationToken = default)
        => SourceText.From(content);
}