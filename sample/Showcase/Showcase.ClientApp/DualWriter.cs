namespace Showcase.ClientApp;

/// <summary>
/// TextWriter that writes to both Console and Debug output (VS Output window).
/// </summary>
internal sealed class DualWriter(TextWriter consoleWriter)
    : TextWriter
{
    public override Encoding Encoding
        => consoleWriter.Encoding;

    public override void Write(char value)
    {
        consoleWriter.Write(value);
        Debug.Write(value);
    }

    public override void Write(string? value)
    {
        consoleWriter.Write(value);
        Debug.Write(value);
    }

    public override void WriteLine(string? value)
    {
        consoleWriter.WriteLine(value);
        Debug.WriteLine(value);
    }

    public override void WriteLine()
    {
        consoleWriter.WriteLine();
        Debug.WriteLine(string.Empty);
    }
}