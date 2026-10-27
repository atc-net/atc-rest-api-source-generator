namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A client harness that does not run - a non-zero exit, no results file - is one column of the matrix, not the end
/// of it: every case of that client gets the verdict <c>harness</c> with the tail of the harness output, the other
/// columns are judged as usual, and the report is still written (with the column red and the run not green).
/// </summary>
public sealed partial class ErrorResponseBodyRuntimeTests
{
    /// <summary>
    /// The verdict of a cell whose client harness did not run.
    /// </summary>
    internal const string VerdictHarness = "harness";

    private const string HarnessKey = "harness";

    /// <summary>
    /// The harness's results for its client: the records it wrote when it ran, or one record per case saying that
    /// it did not run (the exit code and the tail of its output) when it exited with an error or wrote no results.
    /// </summary>
    /// <param name="work">The harness's working directory.</param>
    /// <param name="client">The harness's client name (the results file is <c>results-{client}.json</c>).</param>
    /// <param name="exitCode">The harness's exit code.</param>
    /// <param name="output">Its output, both streams.</param>
    /// <param name="caseNames">The names of the cases it was given.</param>
    /// <returns>The records by case name.</returns>
    internal static Dictionary<string, JsonObject> ReadHarnessResults(
        string work,
        string client,
        int exitCode,
        string output,
        IEnumerable<string> caseNames)
    {
        var resultsFile = Path.Combine(work, $"results-{client}.json");
        if (exitCode == 0 && File.Exists(resultsFile))
        {
            return JsonNode.Parse(File.ReadAllText(resultsFile))!.AsArray()
                .Select(n => n!.AsObject())
                .ToDictionary(o => (string)o["name"]!, o => o, StringComparer.Ordinal);
        }

        var reason = exitCode == 0
            ? $"the {client} harness wrote no results"
            : $"the {client} harness did not run (exit {exitCode})";
        var tail = Tail(output);
        return caseNames.ToDictionary(
            name => name,
            name => new JsonObject
            {
                ["name"] = name,
                [HarnessKey] = tail.Length == 0 ? reason : $"{reason}: {tail}",
            },
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The <c>harness</c> cell for a record that says the harness did not run, else <see langword="null"/>.
    /// </summary>
    /// <param name="record">The harness's record for the case.</param>
    /// <returns>The cell, or null when the record is a real result.</returns>
    internal static JsonObject? HarnessCell(JsonObject? record)
        => record?[HarnessKey] is { } why
            ? Cell(VerdictHarness, (string)why!, "harness did not run")
            : null;

    private static string Tail(
        string output,
        int lines = 15)
    {
        var all = (output ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(l => l.Trim().Length > 0).ToList();
        return string.Join(" | ", all.Skip(System.Math.Max(0, all.Count - lines)));
    }
}