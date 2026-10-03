namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// How a generated client adds one part of a multipart form (<see cref="Helpers.FormBodyHelper.AppendMultipartFields"/>): a
/// file (the expression reads an <c>IFileContent</c>) or a text field (the expression is the field's wire text). Each
/// call writes complete statements at the given indent.
/// </summary>
/// <param name="File">Writes the statements that add the file read by the expression (indent, field name, expression).</param>
/// <param name="Text">Writes the statement that adds the text given by the expression (indent, field name, expression).</param>
public sealed record MultipartPartWriter(
    Action<StringBuilder, int, string, string> File,
    Action<StringBuilder, int, string, string> Text);