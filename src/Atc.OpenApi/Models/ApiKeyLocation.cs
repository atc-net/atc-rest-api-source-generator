namespace Atc.OpenApi.Models;

/// <summary>
/// Location of API key in request.
/// </summary>
public enum ApiKeyLocation
{
    /// <summary>
    /// API key in header.
    /// </summary>
    Header,

    /// <summary>
    /// API key in query parameter.
    /// </summary>
    Query,

    /// <summary>
    /// API key in cookie.
    /// </summary>
    Cookie,
}