namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// How a generated client adds one part of a multipart form (<see cref="Helpers.FormBodyHelper.AppendMultipartFields"/>): a
/// file (the expression reads an <c>IFileContent</c>), a text field (the expression is the field's wire text) or a JSON
/// field (the expression reads a value sent as an <c>application/json</c> part). Each call writes complete statements at
/// the given indent.
/// </summary>
/// <param name="File">Writes the statements that add the file read by the expression (indent, field name, expression).</param>
/// <param name="Text">Writes the statement that adds the text given by the expression (indent, field name, expression).</param>
/// <param name="Json">Writes the statements that add the value read by the expression as JSON (indent, field name, expression).</param>
public sealed record MultipartPartWriter(
    Action<StringBuilder, int, string, string> File,
    Action<StringBuilder, int, string, string> Text,
    Action<StringBuilder, int, string, string> Json);