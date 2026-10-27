namespace Showcase.Api.Domain.ApiHandlers.Documents;

/// <summary>
/// Writes a one-page PDF of a title and lines of text (Helvetica) - enough for a printable summary, without a PDF
/// library.
/// </summary>
internal static class SimplePdf
{
    public static byte[] Create(
        string title,
        IReadOnlyList<string> lines)
    {
        var text = new StringBuilder();
        text.Append("BT /F1 18 Tf 72 760 Td (").Append(Escape(title)).Append(") Tj ET\n");
        for (var i = 0; i < lines.Count; i++)
        {
            text.Append("BT /F1 11 Tf 72 ")
                .Append((730 - (i * 18)).ToString(CultureInfo.InvariantCulture))
                .Append(" Td (")
                .Append(Escape(lines[i]))
                .Append(") Tj ET\n");
        }

        var stream = text.ToString();
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            $"<< /Length {Latin1(stream).Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n{stream}endstream",
        ];

        using var pdf = new MemoryStream();
        Write(pdf, "%PDF-1.4\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Position);
            Write(pdf, $"{(i + 1).ToString(CultureInfo.InvariantCulture)} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = pdf.Position;
        var table = new StringBuilder();
        table.Append("xref\n0 ").Append((objects.Length + 1).ToString(CultureInfo.InvariantCulture)).Append('\n');
        table.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            table.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        table.Append("trailer\n<< /Size ")
            .Append((objects.Length + 1).ToString(CultureInfo.InvariantCulture))
            .Append(" /Root 1 0 R >>\nstartxref\n")
            .Append(xref.ToString(CultureInfo.InvariantCulture))
            .Append("\n%%EOF\n");
        Write(pdf, table.ToString());
        return pdf.ToArray();
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    private static byte[] Latin1(string value)
        => Encoding.Latin1.GetBytes(value);

    private static void Write(
        Stream stream,
        string value)
    {
        var bytes = Latin1(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}