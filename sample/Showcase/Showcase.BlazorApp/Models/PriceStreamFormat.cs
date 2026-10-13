namespace Showcase.BlazorApp.Models;

/// <summary>
/// The five formats the API streams prices in. Each is an operation of its own: the format of a response is its
/// media type in the specification.
/// </summary>
public enum PriceStreamFormat
{
    ServerSentEvents,
    JsonLines,
    JsonArray,
    JsonSequence,
    MultipartMixed,
}