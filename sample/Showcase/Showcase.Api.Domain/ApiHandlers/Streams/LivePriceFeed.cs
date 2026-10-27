namespace Showcase.Api.Domain.ApiHandlers.Streams;

/// <summary>
/// The one live price feed every subscriber of listLivePriceEvents shares: a tick now and then (a market has quiet
/// stretches - the heartbeat is for those), the last <see cref="ReplayCapacity"/> ticks kept for a client that resumes,
/// and a bounded queue per subscriber, so a reader that falls behind is cut off - it resumes after its last event, from
/// the replay buffer - instead of holding up the feed and every other reader.
/// </summary>
public sealed class LivePriceFeed : IDisposable
{
    /// <summary>
    /// How many of the latest ticks a resuming client can get again.
    /// </summary>
    public const int ReplayCapacity = 20;

    /// <summary>
    /// How many ticks a subscriber may be behind before it is cut off.
    /// </summary>
    public const int SubscriberCapacity = 32;

    private static readonly string[] Symbols = ["ATC", "NET", "SSE", "API"];

    private readonly Lock gate = new();
    private readonly Queue<PriceTick> replay = new();
    private readonly List<ChannelWriter<PriceTick>> subscribers = [];
    private readonly Dictionary<string, double> prices = Symbols.ToDictionary(s => s, _ => 100d, StringComparer.Ordinal);
    private readonly CancellationTokenSource stopping = new();
    private long sequence;

    public LivePriceFeed()
        => _ = RunAsync(stopping.Token);

    /// <summary>
    /// Subscribes to the feed: what the replay buffer has after <paramref name="lastSequence"/>, how many ticks after it
    /// it no longer has, and every tick from then on.
    /// </summary>
    /// <param name="lastSequence">The sequence of the last tick the client read, or <see langword="null"/> for a new client.</param>
    public LivePriceSubscription Subscribe(long? lastSequence)
    {
        var channel = Channel.CreateBounded<PriceTick>(new BoundedChannelOptions(SubscriberCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
        });

        // Under the lock that publishes: the replay ends where the live ticks start, nothing twice, nothing lost
        lock (gate)
        {
            var replayed = lastSequence is { } last
                ? replay.Where(t => t.Sequence > last).ToList()
                : [];
            var oldest = replay.Count > 0 ? replay.Peek().Sequence : sequence + 1;
            var missed = lastSequence is { } after ? Math.Max(0, oldest - 1 - after) : 0;
            subscribers.Add(channel.Writer);
            return new LivePriceSubscription(replayed, missed, channel.Reader, () => Unsubscribe(channel.Writer));
        }
    }

    public void Dispose()
    {
        stopping.Cancel();
        stopping.Dispose();
    }

    private void Unsubscribe(ChannelWriter<PriceTick> subscriber)
    {
        lock (gate)
        {
            subscribers.Remove(subscriber);
        }
    }

    private void Publish(PriceTick tick)
    {
        lock (gate)
        {
            replay.Enqueue(tick);
            if (replay.Count > ReplayCapacity)
            {
                replay.Dequeue();
            }

            for (var i = subscribers.Count - 1; i >= 0; i--)
            {
                if (subscribers[i].TryWrite(tick))
                {
                    continue;
                }

                // A whole queue behind: the reader's stream ends as a dropped connection does, and it resumes after its
                // last event - the feed never waits for it
                subscribers[i].TryComplete(new IOException($"The subscriber fell {SubscriberCapacity} prices behind the live feed."));
                subscribers.RemoveAt(i);
            }
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Between a fifth of a second and three seconds: a market is busy, then quiet
                await Task.Delay(TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(200, 3000)), cancellationToken);

                var symbol = Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)];
                var change = (RandomNumberGenerator.GetInt32(201) - 100) / 100d;
                var price = Math.Max(1, Math.Round(prices[symbol] + change, 2));
                prices[symbol] = price;

                Publish(new PriceTick(symbol, price, change, Interlocked.Increment(ref sequence)));
            }
        }
        catch (OperationCanceledException)
        {
            // The application is stopping
        }
    }
}