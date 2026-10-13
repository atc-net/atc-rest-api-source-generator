namespace Showcase.BlazorApp.Pages;

public partial class LiveStreams : IDisposable
{
    private const int MaxArrivals = 12;

    private static readonly PriceStreamFormat[] Formats = Enum.GetValues<PriceStreamFormat>();

    private readonly Dictionary<string, PriceQuote> quotes = new(StringComparer.Ordinal);

    private readonly List<PriceArrival> arrivals = [];

    private PriceStreamFormat format = PriceStreamFormat.ServerSentEvents;

    private int count = 40;

    private bool isStreaming;

    private string status = "Idle";

    private int itemCount;

    private double elapsedSeconds;

    private string? streamFormatHeader;

    private StreamFailure? failure;

    private CancellationTokenSource? cts;

    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
    }

    private static string Describe(PriceStreamFormat format)
        => format switch
        {
            PriceStreamFormat.JsonLines => "JSON Lines",
            PriceStreamFormat.JsonArray => "JSON array",
            PriceStreamFormat.JsonSequence => "JSON Text Sequence",
            PriceStreamFormat.MultipartMixed => "Multipart",
            _ => "Server-Sent Events",
        };

    private static string Key(PriceStreamFormat format)
        => format switch
        {
            PriceStreamFormat.JsonLines => "lines",
            PriceStreamFormat.JsonArray => "array",
            PriceStreamFormat.JsonSequence => "seq",
            PriceStreamFormat.MultipartMixed => "mixed",
            _ => "sse",
        };

    private static string OperationName(PriceStreamFormat format)
        => format switch
        {
            PriceStreamFormat.JsonLines => "ListPriceLines",
            PriceStreamFormat.JsonArray => "ListPriceElements",
            PriceStreamFormat.JsonSequence => "ListPriceRecords",
            PriceStreamFormat.MultipartMixed => "ListPriceParts",
            _ => "ListPriceEvents",
        };

    private static string CallSnippet(PriceStreamFormat format)
    {
        var operation = OperationName(format);
        var endpoint = char.ToLowerInvariant(operation[0]) + operation.Substring(1) + "Endpoint";
        return $$"""
            using var result = await {{endpoint}}.ExecuteAsync(
                new {{operation}}Parameters(count, closed),
                cancellationToken: cancellationToken);

            if (result.IsServiceUnavailable)
            {
                Show(result.ServiceUnavailableContent, result.RetryAfterHeader);
                return;
            }

            await foreach (var tick in result.Content!)
            {
                Show(tick);
            }
            """;
    }

    private async Task StartAsync()
    {
        quotes.Clear();
        arrivals.Clear();
        itemCount = 0;
        elapsedSeconds = 0;
        streamFormatHeader = null;
        failure = null;
        isStreaming = true;
        status = "Opening…";
        cts = new CancellationTokenSource();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var call = await Gateway.OpenPriceStreamAsync(format, count, closed: false, cts.Token);
            streamFormatHeader = call.Format;

            if (!call.Response.IsSuccess || call.Response.Content is null)
            {
                status = $"Failed: {(int)call.Response.StatusCode}";
                return;
            }

            status = "Streaming…";
            await foreach (var tick in call.Response.Content.WithCancellation(cts.Token))
            {
                if (tick is null)
                {
                    continue;
                }

                Show(tick, stopwatch.ElapsedMilliseconds);
                StateHasChanged();
            }

            status = $"Completed: {itemCount} prices";
        }
        catch (OperationCanceledException)
        {
            status = $"Stopped after {itemCount} prices";
        }
        catch (Exception ex)
        {
            status = "Failed";
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            isStreaming = false;
            cts?.Dispose();
            cts = null;
        }
    }

    private void Stop()
        => cts?.Cancel();

    private void Show(
        PriceTick tick,
        long elapsedMilliseconds)
    {
        itemCount++;
        elapsedSeconds = elapsedMilliseconds / 1000d;
        quotes[tick.Symbol] = new PriceQuote(
            tick.Symbol,
            tick.Price,
            tick.Change,
            quotes.TryGetValue(tick.Symbol, out var quote) ? quote.Ticks + 1 : 1);

        arrivals.Insert(0, new PriceArrival(elapsedMilliseconds, tick));
        if (arrivals.Count > MaxArrivals)
        {
            arrivals.RemoveAt(arrivals.Count - 1);
        }
    }

    private async Task CallClosedAsync()
    {
        failure = null;

        try
        {
            using var call = await Gateway.OpenPriceStreamAsync(format, count, closed: true, CancellationToken.None);
            failure = new StreamFailure(
                (int)call.Response.StatusCode,
                call.Problem?.Title,
                call.Problem?.Detail,
                call.RetryAfterSeconds);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
    }

    private sealed record StreamFailure(
        int StatusCode,
        string? Title,
        string? Detail,
        int? RetryAfterSeconds);
}