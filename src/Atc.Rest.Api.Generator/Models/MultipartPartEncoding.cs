namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// How a field of a multipart form is sent (see <see cref="Helpers.FormBodyHelper.PartOf"/>).
/// </summary>
public enum MultipartPartEncoding
{
    /// <summary>
    /// A value with a text form (a string, a number, a boolean, an enum, a file, or a list of them): a text part, or a
    /// file part.
    /// </summary>
    Text,

    /// <summary>
    /// An object, a map or a composition (or a list of one, a part per item) as an <c>application/json</c> part - what
    /// OpenAPI sends such a field as, unless the form's <c>encoding</c> names another media type.
    /// </summary>
    Json,

    /// <summary>
    /// A model the form's <c>encoding</c> sends as XML (<c>application/xml</c>, <c>text/xml</c>, <c>*+xml</c>), mapped
    /// as an XML body of the model is.
    /// </summary>
    Xml,

    /// <summary>
    /// A flat model (each property one value, as an object in the query) the form's <c>encoding</c> sends as
    /// <c>application/x-www-form-urlencoded</c>: <c>name=value&amp;…</c> by the properties' names.
    /// </summary>
    UrlEncoded,

    /// <summary>
    /// A value with no text form whose <c>encoding</c> names a media type that does not carry it (<c>text/plain</c>,
    /// <c>application/octet-stream</c>, XML for a map or a composition, URL-encoded for a nested model):
    /// ATC_API_OPR037, and the field is left out.
    /// </summary>
    None,
}