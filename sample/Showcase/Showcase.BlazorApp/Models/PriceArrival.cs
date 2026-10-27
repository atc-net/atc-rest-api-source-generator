namespace Showcase.BlazorApp.Models;

/// <summary>
/// One tick and when it arrived, counted from the moment the stream opened.
/// </summary>
public sealed record PriceArrival(
    long ElapsedMilliseconds,
    PriceTick Tick);