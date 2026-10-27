namespace Showcase.Api.Domain.ApiHandlers.Streams;

/// <summary>
/// One subscriber of the <see cref="LivePriceFeed"/>: the ticks it gets again, how many it missed, and the live ticks.
/// Dispose it to leave the feed.
/// </summary>
public sealed class LivePriceSubscription : IDisposable
{
    private readonly Action unsubscribe;

    public LivePriceSubscription(
        IReadOnlyList<PriceTick> replayed,
        long missed,
        ChannelReader<PriceTick> live,
        Action unsubscribe)
    {
        Replayed = replayed;
        Missed = missed;
        Live = live;
        this.unsubscribe = unsubscribe;
    }

    /// <summary>
    /// The ticks after the client's last one that the replay buffer still has, oldest first.
    /// </summary>
    public IReadOnlyList<PriceTick> Replayed { get; }

    /// <summary>
    /// How many ticks after the client's last one the buffer no longer has.
    /// </summary>
    public long Missed { get; }

    /// <summary>
    /// Every tick published after the replayed ones.
    /// </summary>
    public ChannelReader<PriceTick> Live { get; }

    public void Dispose()
        => unsubscribe();
}