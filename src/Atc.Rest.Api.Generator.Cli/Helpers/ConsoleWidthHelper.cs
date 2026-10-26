namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// The width the tool writes in when its output is redirected (a pipe, a CI log): a console without a window is 80
/// columns wide, which breaks a long path or message over lines that a log reader or a script then cannot find.
/// </summary>
public static class ConsoleWidthHelper
{
    /// <summary>
    /// The width of redirected output when <c>COLUMNS</c> does not give one.
    /// </summary>
    public const int RedirectedWidth = 240;

    /// <summary>
    /// The width for redirected output: <c>COLUMNS</c> when it holds a positive number, else <see cref="RedirectedWidth"/>.
    /// </summary>
    /// <param name="columns">The value of the <c>COLUMNS</c> environment variable, if any.</param>
    /// <returns>The width in columns.</returns>
    public static int ForRedirectedOutput(string? columns)
        => int.TryParse(columns?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var width) && width > 0
            ? width
            : RedirectedWidth;
}