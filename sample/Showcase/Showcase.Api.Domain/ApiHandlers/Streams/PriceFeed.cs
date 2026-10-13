namespace Showcase.Api.Domain.ApiHandlers.Streams;

/// <summary>
/// The prices every stream operation yields: one tick about seven times a second, for as many ticks as asked for
/// or until the client stops reading.
/// </summary>
public static class PriceFeed
{
    public const string ClosedMessage = "The exchange is closed.";
    public const int RetryAfterSeconds = 30;
    public const int DefaultCount = 40;

    private static readonly TimeSpan Pace = TimeSpan.FromMilliseconds(150);
    private static readonly string[] Symbols = ["ATC", "NET", "SSE", "API"];

    public static async IAsyncEnumerable<PriceTick> TicksAsync(
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var prices = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var symbol in Symbols)
        {
            prices[symbol] = 100;
        }

        for (var sequence = 1L; sequence <= count; sequence++)
        {
            var symbol = Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)];
            var change = (RandomNumberGenerator.GetInt32(201) - 100) / 100d;
            var price = Math.Max(1, Math.Round(prices[symbol] + change, 2));
            prices[symbol] = price;

            yield return new PriceTick(symbol, price, change, sequence);

            await Task.Delay(Pace, cancellationToken);
        }
    }
}