namespace Showcase.Api.Domain.ApiHandlers.Streams;

/// <summary>
/// Handler business logic for the ListLivePriceEvents operation: the shared live price feed as Server-Sent Events -
/// what a long-lived event stream needs besides its items.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Fan-out: every subscriber reads the one <see cref="LivePriceFeed"/>; a reader that falls behind is cut off
/// instead of holding up the others.</item>
/// <item>Replay: each event's id is the tick's sequence; a client that sends the last id it read as Last-Event-ID gets
/// the ticks after it from the replay buffer, and the X-Events-Missed header says how many the buffer no longer had.</item>
/// <item>Heartbeat: when no price came for a while, a named event <c>ping</c> - the event writer has no comment line,
/// and every generated client reads a named event past. It keeps a proxy that closes an idle connection from closing
/// this one.</item>
/// </list>
/// </remarks>
public sealed class ListLivePriceEventsHandler(LivePriceFeed feed) : IListLivePriceEventsHandler
{
    private const string Heartbeat = "ping";

    private static readonly ActivitySource ActivitySource = new("Showcase.Api.Domain.ApiHandlers.Streams.ListLivePriceEvents");

    /// <summary>
    /// Executes the ListLivePriceEvents operation.
    /// </summary>
    public Task<ListLivePriceEventsResult> ExecuteAsync(
        ListLivePriceEventsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ListLivePriceEvents");

        long? lastSequence = long.TryParse(parameters.LastEventId, NumberStyles.None, CultureInfo.InvariantCulture, out var last)
            ? last
            : null;
#pragma warning disable CA2000 // The events own the subscription: they leave the feed when the stream ends
        var subscription = feed.Subscribe(lastSequence);
#pragma warning restore CA2000
        var events = EventsAsync(
            subscription,
            TimeSpan.FromSeconds(parameters.HeartbeatSeconds ?? 15),
            parameters.Count,
            cancellationToken);

        return Task.FromResult(ListLivePriceEventsResult.Ok(events, xEventsMissed: subscription.Missed));
    }

    private static async IAsyncEnumerable<SseItem<PriceTick>> EventsAsync(
        LivePriceSubscription subscription,
        TimeSpan heartbeat,
        int? count,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var owned = subscription;
        var sent = 0;
        foreach (var tick in subscription.Replayed)
        {
            if (sent == count)
            {
                yield break;
            }

            sent++;
            yield return Event(tick);
        }

        // The next tick, or the heartbeat when it takes longer than the interval: the wait for the tick goes on across
        // heartbeats, so none is missed
        Task<bool>? waiting = null;
        while (sent != count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            waiting ??= subscription.Live.WaitToReadAsync(cancellationToken).AsTask();

            bool quiet;
            using (var beat = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                quiet = await Task.WhenAny(waiting, Task.Delay(heartbeat, beat.Token)) != waiting;
                await beat.CancelAsync();
            }

            if (quiet)
            {
                yield return new SseItem<PriceTick>(default!, Heartbeat);
                continue;
            }

            // False: the feed stopped; a reader cut off for falling behind gets the feed's exception here
            if (!await waiting)
            {
                yield break;
            }

            waiting = null;
            while (sent != count && subscription.Live.TryRead(out var tick))
            {
                sent++;
                yield return Event(tick);
            }
        }
    }

    private static SseItem<PriceTick> Event(PriceTick tick)
        => new(tick) { EventId = tick.Sequence.ToString(CultureInfo.InvariantCulture) };
}