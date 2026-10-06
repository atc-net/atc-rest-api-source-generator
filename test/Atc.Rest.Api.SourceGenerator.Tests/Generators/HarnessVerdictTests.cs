namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A client harness that crashes or writes no results is one red column of the matrix (the verdict <c>harness</c>
/// on each of its cases, with the tail of its output), never an exception that loses the whole matrix and the
/// report; the records a harness did write are read as they always were.
/// </summary>
public class HarnessVerdictTests
{
    [Fact]
    public void ReadHarnessResults_AHarnessThatCrashed_IsAHarnessRecordPerCase()
    {
        var work = Path.Combine(Path.GetTempPath(), "atc-harness-verdict", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var records = ErrorResponseBodyRuntimeTests.ReadHarnessResults(work, "fetch", 1, "TypeError: fetch failed\n    at node:internal\nError: boom", ["a/one", "b/two"]);

            Assert.Equal(["a/one", "b/two"], records.Keys.Order(StringComparer.Ordinal));
            var cell = ErrorResponseBodyRuntimeTests.HarnessCell(records["a/one"]);
            Assert.NotNull(cell);
            Assert.Equal(ErrorResponseBodyRuntimeTests.VerdictHarness, (string?)cell["verdict"]);
            Assert.Contains("the fetch harness did not run (exit 1)", (string?)cell["why"], StringComparison.Ordinal);
            Assert.Contains("Error: boom", (string?)cell["why"], StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [Fact]
    public void ReadHarnessResults_AHarnessThatWroteNoResults_IsAHarnessRecordPerCase()
    {
        var work = Path.Combine(Path.GetTempPath(), "atc-harness-verdict", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var records = ErrorResponseBodyRuntimeTests.ReadHarnessResults(work, "axios", 0, string.Empty, ["only"]);

            var cell = ErrorResponseBodyRuntimeTests.HarnessCell(records["only"]);
            Assert.NotNull(cell);
            Assert.Equal("the axios harness wrote no results", (string?)cell["why"]);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [Fact]
    public void ReadHarnessResults_AHarnessThatRan_IsItsRecords_AndNoHarnessCell()
    {
        var work = Path.Combine(Path.GetTempPath(), "atc-harness-verdict", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            File.WriteAllText(Path.Combine(work, "results-fetch.json"), """[{"name":"a/one","status":"ok"}]""");

            var records = ErrorResponseBodyRuntimeTests.ReadHarnessResults(work, "fetch", 0, "ALL DONE", ["a/one"]);

            Assert.Equal("ok", (string?)records["a/one"]["status"]);
            Assert.Null(ErrorResponseBodyRuntimeTests.HarnessCell(records["a/one"]));
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }
}