namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The report's summary tells a known failure (a cell the matrix's ratchet lists, as <c>case [client]</c> or
/// <c>case [wire]</c>) from a regression (a fail no ratchet lists): the first makes the run <c>known-failures</c>,
/// the second <c>regressions</c>; a run without a fail is <c>green</c>.
/// </summary>
public class ReportSummaryTests
{
    [Fact]
    public void Summary_AWireFailTheRatchetLists_IsKnownNotARegression()
    {
        var matrix = Matrix(
            wireVerdict: "fail",
            clientVerdict: "pass",
            ratchet: ["server: the required parameter is missing [wire]"]);

        var summary = ErrorResponseBodyRuntimeTests.Summary(matrix);

        Assert.Equal("known-failures", (string?)summary["state"]);
        Assert.Empty(summary["unexpectedFailures"]!.AsArray());
        Assert.Equal(1, (int?)summary["matrices"]!["requestBindingPlain"]!["knownFailures"]);
    }

    [Fact]
    public void Summary_AWireFailNoRatchetLists_IsARegression()
    {
        var matrix = Matrix(wireVerdict: "fail", clientVerdict: "pass", ratchet: []);

        var summary = ErrorResponseBodyRuntimeTests.Summary(matrix);

        Assert.Equal("regressions", (string?)summary["state"]);
        var unexpected = Assert.Single(summary["unexpectedFailures"]!.AsArray());
        Assert.Equal("requestBindingPlain|server: the required parameter is missing|wire", (string?)unexpected);
    }

    [Fact]
    public void Summary_AClientFailTheRatchetLists_IsKnown_AndNoFailIsGreen()
    {
        var known = ErrorResponseBodyRuntimeTests.Summary(Matrix("pass", "fail", ["server: the required parameter is missing [cs-epo]"]));
        Assert.Equal("known-failures", (string?)known["state"]);

        var green = ErrorResponseBodyRuntimeTests.Summary(Matrix("pass", "pass", []));
        Assert.Equal("green", (string?)green["state"]);
        Assert.Equal(2, (int?)green["matrices"]!["requestBindingPlain"]!["verdicts"]!["pass"]);
    }

    private static JsonObject Matrix(
        string wireVerdict,
        string clientVerdict,
        string[] ratchet)
        => new()
        {
            ["cases"] = new JsonArray(),
            ["requestBindingPlain"] = new JsonObject
            {
                ["cases"] = new JsonArray(new JsonObject
                {
                    ["id"] = "server: the required parameter is missing",
                    ["wire"] = new JsonObject { ["verdict"] = wireVerdict, ["why"] = "-" },
                    ["clients"] = new JsonObject
                    {
                        ["cs-epo"] = new JsonObject { ["verdict"] = clientVerdict, ["why"] = "-", ["summary"] = "-" },
                    },
                }),
            },
            ["ratchet"] = new JsonObject
            {
                ["requestBindingPlain"] = new JsonArray([.. ratchet.Select(r => (JsonNode)r)]),
            },
        };
}