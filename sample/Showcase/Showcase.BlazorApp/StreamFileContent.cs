namespace Showcase.BlazorApp;

/// <summary>
/// Simple IFileContent implementation wrapping a Stream for sample/testing purposes.
/// </summary>
internal sealed class StreamFileContent(Stream stream, string fileName, string contentType = "application/octet-stream") : IFileContent
{
    public string FileName => fileName;

    public string ContentType => contentType;

    public Stream OpenReadStream() => stream;
}