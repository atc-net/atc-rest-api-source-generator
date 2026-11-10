namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Runs a command that writes into a project (<c>generate server</c>, <c>generate client</c>, <c>spec split</c>) in a
/// <see cref="FileSystemTransaction"/>: what it writes is kept when it returns 0, and put back - saying so - when it fails.
/// What an external tool writes (<c>dotnet</c>) is not tracked.
/// </summary>
public static class CommandFileTransaction
{
    /// <summary>
    /// Runs a command in a transaction.
    /// </summary>
    /// <param name="command">The command; 0 is success.</param>
    /// <returns>The command's result.</returns>
    public static int Run(Func<int> command)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var transaction = FileSystemTransaction.Begin();
        var result = command();
        return Complete(transaction, result);
    }

    /// <summary>
    /// Runs an asynchronous command in a transaction.
    /// </summary>
    /// <param name="command">The command; 0 is success.</param>
    /// <returns>The command's result.</returns>
    public static async Task<int> RunAsync(Func<Task<int>> command)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var transaction = FileSystemTransaction.Begin();
        var result = await command().ConfigureAwait(false);
        return Complete(transaction, result);
    }

    private static int Complete(
        FileSystemTransaction transaction,
        int result)
    {
        if (result == 0)
        {
            transaction.Commit();
        }
        else if (transaction.HasChanges)
        {
            AnsiConsole.MarkupLine("[yellow]The files written before the failure were put back.[/]");
        }

        return result;
    }
}