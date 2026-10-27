namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// A JSONPath query that RFC 9535 does not allow: a syntax error, or a filter that is not well-typed (a function
/// that does not exist, an argument of the wrong type, a comparison with a query that can select several nodes).
/// </summary>
public sealed class JsonPathException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPathException"/> class.
    /// </summary>
    public JsonPathException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPathException"/> class.
    /// </summary>
    /// <param name="message">What is wrong, and where.</param>
    public JsonPathException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPathException"/> class.
    /// </summary>
    /// <param name="message">What is wrong, and where.</param>
    /// <param name="innerException">The cause.</param>
    public JsonPathException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}