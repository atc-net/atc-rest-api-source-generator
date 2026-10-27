namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Defines how an EndpointPerOperation client answers a request that got no answer from the server.
/// </summary>
public enum TransportFailureType
{
    /// <summary>
    /// The exception is thrown (<c>HttpRequestException</c>, a timeout, a resilience rejection). The default.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// The operation's result is returned: <c>IsTransportFailure</c>, a 503 with a problem that says the server could
    /// not be reached, and the exception in <c>TransportException</c>. The caller's own cancellation still throws.
    /// </summary>
    Result = 1,
}