namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

public sealed class SerialRunnerTests
{
    [Fact]
    public async Task Trigger_WhileRunning_RunsOnceMoreAfterwards_NeverConcurrently()
    {
        // --watch: a change during a regeneration must not start a second, overlapping one - it runs once more after
        using var firstRunHeld = new ManualResetEventSlim(false);
        using var firstRunStarted = new ManualResetEventSlim(false);
        var running = 0;
        var maxRunning = 0;
        var runs = 0;

        var runner = new SerialRunner(() =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref maxRunning, now);
            if (Interlocked.Increment(ref runs) == 1)
            {
                firstRunStarted.Set();
                firstRunHeld.Wait(TestContext.Current.CancellationToken);
            }

            Interlocked.Decrement(ref running);
        });

        runner.Trigger();
        Assert.True(firstRunStarted.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        runner.Trigger();
        runner.Trigger();
        runner.Trigger();
        firstRunHeld.Set();

        await runner.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(2, runs);
        Assert.Equal(1, maxRunning);
    }

    [Fact]
    public async Task Trigger_WhenIdle_RunsTheAction()
    {
        var runs = 0;
        var runner = new SerialRunner(() => Interlocked.Increment(ref runs));

        runner.Trigger();
        await runner.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(1, runs);
    }

    private static void InterlockedMax(
        ref int target,
        int value)
    {
        var current = Volatile.Read(ref target);
        while (value > current)
        {
            var previous = Interlocked.CompareExchange(ref target, value, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }
}