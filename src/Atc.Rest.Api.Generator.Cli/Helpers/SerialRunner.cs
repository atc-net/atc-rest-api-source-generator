namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Runs an action on the thread pool one run at a time: a trigger while a run is in progress does not start a second,
/// overlapping run - the action runs once more when the current run ends (however many triggers came in between).
/// Used by <c>--watch</c>, where a specification saved during a regeneration must regenerate again, not concurrently.
/// </summary>
public sealed class SerialRunner
{
    private readonly Action action;
    private readonly Lock gate = new();
    private bool running;
    private bool pending;
    private TaskCompletionSource idle = CompletedSource();

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialRunner"/> class.
    /// </summary>
    /// <param name="action">The action to run; it should handle its own exceptions.</param>
    public SerialRunner(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        this.action = action;
    }

    /// <summary>
    /// Starts a run, or - when one is in progress - asks for one more run after it.
    /// </summary>
    public void Trigger()
    {
        lock (gate)
        {
            if (running)
            {
                pending = true;
                return;
            }

            running = true;
            idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        // A regeneration is long-running CPU work: a thread of its own, not a pool thread it would hold (and wait for,
        // under a starved pool) for the whole run
        _ = Task.Factory.StartNew(RunLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    /// <summary>
    /// A task that completes when no run is in progress or asked for.
    /// </summary>
    /// <returns>The idle task.</returns>
    public Task WhenIdleAsync()
    {
        lock (gate)
        {
            return idle.Task;
        }
    }

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource();
        source.SetResult();
        return source;
    }

    private void RunLoop()
    {
        while (true)
        {
            action();

            lock (gate)
            {
                if (pending)
                {
                    pending = false;
                    continue;
                }

                running = false;
                idle.TrySetResult();
                return;
            }
        }
    }
}