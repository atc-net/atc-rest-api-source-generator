namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The media type a C# client sends a raw file body with (the body is the file itself: octet-stream, PDF, PNG, ...).
/// </summary>
public static class RawFileBodyHelper
{
    /// <summary>
    /// A C# expression for the Content-Type of a raw file body. An operation that declares exactly one concrete media
    /// type accepts only that one: the body is sent as it, whatever type the file has (a text file posted to an
    /// <c>application/octet-stream</c> operation as <c>text/plain</c> is a 415). An operation that declares several, or a
    /// range (<c>image/*</c>), is sent the file's own media type - which of them it is - else the first declared.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="fileExpression">The expression that reads the file, e.g. <c>parameters.File</c>.</param>
    /// <param name="fallbackMediaType">The media type when the operation declares none.</param>
    /// <returns>An expression of type <c>string</c>.</returns>
    public static string ContentTypeExpression(
        OpenApiOperation operation,
        string fileExpression,
        string fallbackMediaType)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        var declared = operation.RequestBody?.Content?.Keys
            .Where(OpenApiOperationExtensions.IsRawFileUploadContentType)
            .ToList() ?? [];

        if (declared.Count == 1 && declared[0].IndexOf('*') < 0)
        {
            return $"\"{declared[0]}\"";
        }

        return $"{fileExpression}.ContentType ?? \"{(declared.Count > 0 ? declared[0] : fallbackMediaType)}\"";
    }
}