namespace Showcase.BlazorApp.Models;

/// <summary>
/// The latest price of one symbol, and how many ticks it has had.
/// </summary>
public sealed record PriceQuote(
    string Symbol,
    double Price,
    double Change,
    int Ticks);