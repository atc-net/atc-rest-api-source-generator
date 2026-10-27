namespace Atc.Rest.Api.Generator.Cli.Tests;

/// <summary>
/// Settings for the whole test assembly.
/// </summary>
public static class TestAssemblySetup
{
    /// <summary>
    /// The thread pool starts with enough threads for the tests that run in parallel: the TypeScript gates block a
    /// thread each for minutes (npm, tsc, a Node harness), and the pool adds threads slowly, so the tests that wait on
    /// a thread-pool callback - a file watcher's event, a process's output - could wait for a thread long enough to
    /// time out (a spec watcher, spec split and an external tool did, only in a full run).
    /// </summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void Init()
    {
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
        ThreadPool.SetMinThreads(System.Math.Max(workerThreads, 64), System.Math.Max(completionPortThreads, 64));
    }
}