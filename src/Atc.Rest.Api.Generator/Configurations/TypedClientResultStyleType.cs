namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Defines how generated typed client operations report non-success HTTP responses.
/// </summary>
public enum TypedClientResultStyleType
{
    /// <summary>
    /// Non-success status codes throw an <c>HttpRequestException</c> carrying the response body.
    /// This is the default and matches the historical behaviour.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Operations return <c>EndpointResponse</c>/<c>EndpointResponse&lt;T&gt;</c> from Atc.Rest.Client,
    /// so callers can inspect the status without exception handling.
    /// Transport-level failures still throw.
    /// </summary>
    Result = 1,
}