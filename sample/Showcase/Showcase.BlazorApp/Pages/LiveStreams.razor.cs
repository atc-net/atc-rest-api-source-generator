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

    private string resumeStatus = "Idle";

    private string importStatus = "Idle";

    private bool isImporting;

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

    // The resume loop a caller writes: read until the stream ends; when it drops, open it again after the last event
    private async Task StreamWithADropAsync()
    {
        const int total = 12;
        const int dropAfter = 5;
        quotes.Clear();
        arrivals.Clear();
        itemCount = 0;
        isStreaming = true;
        resumeStatus = "Streaming…";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        string? lastEventId = null;
        string? resumedAfter = null;

        try
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                using var result = await Gateway.OpenPriceEventsAsync(total, dropAfter, lastEventId, CancellationToken.None);
                if (!result.IsOk || result.Content is null)
                {
                    resumeStatus = $"Failed: {(int)result.StatusCode}";
                    return;
                }

                try
                {
                    await foreach (var tick in result.Content)
                    {
                        if (tick is null)
                        {
                            continue;
                        }

                        Show(tick, stopwatch.ElapsedMilliseconds);
                        StateHasChanged();
                    }

                    resumeStatus = resumedAfter is null
                        ? $"Completed: {itemCount} prices"
                        : $"Completed: {itemCount} prices, resumed after event {resumedAfter}";
                    return;
                }
                catch (Exception) when (result.LastEventId is { Length: > 0 } id && id != lastEventId)
                {
                    // The connection dropped: go on after the last event that was read
                    lastEventId = id;
                    resumedAfter = id;
                    resumeStatus = $"Dropped after event {id} - resuming…";
                    StateHasChanged();
                }
            }

            resumeStatus = "Failed: the stream kept dropping";
        }
        catch (Exception ex)
        {
            resumeStatus = "Failed";
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            isStreaming = false;
        }
    }

    // The prices an import sends: made one at a time, as a source that is still producing them would
    private static async IAsyncEnumerable<PriceTick> PricesToImport(
        bool withAnInvalidPrice,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string[] symbols = ["ATC", "MSFT", "NOVO", "MAERSK"];
        for (var i = 1; i <= 25; i++)
        {
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            var price = withAnInvalidPrice && i == 4 ? -5 : 100 + i;
            yield return new PriceTick(symbols[i % symbols.Length], price, 1, i);
        }
    }

    private async Task ImportAsync(bool withAnInvalidPrice)
    {
        isImporting = true;
        importStatus = "Importing...";
        try
        {
            var result = await Gateway.ImportPricesAsync(PricesToImport(withAnInvalidPrice, CancellationToken.None), CancellationToken.None).ConfigureAwait(true);
            importStatus = result.IsOk
                ? $"Imported {result.OkContent.Count} prices: {string.Join(", ", result.OkContent.Symbols)}, from {result.OkContent.Lowest.ToString("0.00", CultureInfo.InvariantCulture)} to {result.OkContent.Highest.ToString("0.00", CultureInfo.InvariantCulture)}"
                : result.IsBadRequest
                    ? $"400: {string.Join(" ", (result.BadRequestContent.Errors ?? new Dictionary<string, string[]>(StringComparer.Ordinal)).Select(e => $"{e.Key} {string.Join(" ", e.Value)}"))}"
                    : $"{(int)result.StatusCode}";
        }
        catch (HttpRequestException ex)
        {
            importStatus = "Failed: " + ex.Message;
        }
        finally
        {
            isImporting = false;
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