namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The suggested assessment of every lossy cell in the end-to-end matrix: a judgement, not a measurement. The loss
/// itself (kind, type, fields) is measured by the verdict code; these rules only say how the team rates it, so the
/// report can point at what deserves another design iteration. A lossy cell no rule matches fails the matrix, so a
/// new kind of loss is rated on purpose. Change a rating here, not in the report.
/// </summary>
public partial class ErrorResponseBodyRuntimeTests
{
    // Green: the best a client can do with that body. Blue: a trade-off chosen by configuration or by the
    // specification. Yellow: acceptable, but worth another design iteration. Red: we think it has to change.
    private const string AssessLevelGood = "good";
    private const string AssessLevelDesign = "by-design";
    private const string AssessLevelImprove = "improve";
    private const string AssessLevelFix = "fix";

    private const string AssessForeignBody = "foreign-body";
    private const string AssessCustomFormat = "custom-format";
    private const string AssessPlainTextOnlyValidation = "plaintextonly-validation";
    private const string AssessModelDeclaredValidation = "model-declared-validation";
    private const string AssessStringDeclaredValidation = "string-declared-validation";
    private const string AssessEmptyCallDeclaredModel = "empty-call-declared-model";
    private const string AssessCustomOverDefault = "custom-over-default";

    private sealed record LossAssessment(
        string Id,
        string Level,
        string Title,
        string Why,
        string Proposal);

    /// <summary>
    /// The ratings, most severe first. <see cref="LossAssessment.Proposal"/> is what we would change, or why nothing.
    /// </summary>
    private static readonly LossAssessment[] LossAssessments =
    [
        new(
            AssessCustomOverDefault,
            AssessLevelFix,
            "errorResponseFormat: Custom overrides a declared default",
            "The specification declares default: ErrorResponse, and the other three formats read a 400/404/500/504 JSON body as that declared model. With Custom, the statuses the client adds itself (400/404/500/504) are always read as the custom model - so the declared default body is read into the wrong type and its code is lost.",
            "Split the client-added statuses by media type under Custom too, as the other formats do: application/problem+json into the custom model, any other body into the declared default."),
        new(
            AssessModelDeclaredValidation,
            AssessLevelImprove,
            "A declared 400 model without errors, but validation writes them",
            "The specification declares a 400 model (ApiProblem) with no errors property, while the generated ValidationFilter writes a ValidationProblemDetails. The body fits the schema (extra properties are allowed), but the field errors of our own validation are only in Content.",
            "Done where the client can: the C# EndpointPerOperation client gives the field errors next to the declared model (BadRequestValidationErrors), the TypeScript clients as ValidationError.errors. A cell stays lossy where the declared model is all a client exposes; declare the 400 as ValidationProblemDetails to keep the field errors typed everywhere."),
        new(
            AssessStringDeclaredValidation,
            AssessLevelImprove,
            "A declared string 400, but validation writes problem+json",
            "The specification declares 400 as a string (application/json or text/plain), while the generated ValidationFilter writes application/problem+json - a media type the operation does not declare. The client reads its title as the string; the field errors are only in Content.",
            "Done: the server generator notes it (ATC_API_SRV002, information) - declare the 400 as ValidationProblemDetails to keep the field errors typed. Still open: validation could honour the declared 400 and write the message as the declared string (Atc.Rest.MinimalApi's ValidationFilter)."),
        new(
            AssessEmptyCallDeclaredModel,
            AssessLevelImprove,
            "An empty factory call on a status with a declared model",
            "NotFound() / UnprocessableEntity() without a value on a status whose specification declares a model: the host writes its ProblemDetails, which is not the declared model, so the declared fields stay null and the ProblemDetails fields are only in Content.",
            "Done: the factory's remarks say what a call without the model writes, and to pass the model for the declared body. Still open (a breaking change): offer the call without the model only where the declared model can hold a ProblemDetails."),
        new(
            AssessCustomFormat,
            AssessLevelDesign,
            "errorResponseFormat: Custom reading our host's ProblemDetails",
            "Custom exists for a third-party API whose server writes its own error model (the Monta sample). Against our generated server, which writes ProblemDetails for undeclared statuses, the custom model cannot hold type/title/detail/instance; they stay in Content.",
            "None: the configuration chose the custom model. For an API served by this generator, ProblemDetails is the format to choose."),
        new(
            AssessPlainTextOnlyValidation,
            AssessLevelDesign,
            "errorResponseFormat: PlainTextOnly reading a validation 400",
            "PlainTextOnly reads every undeclared status as a string - also a validation 400, whose title becomes the string and whose field errors are only in Content.",
            "None: the configuration chose strings. PlainText (the default) keeps the validation 400 typed."),
        new(
            AssessForeignBody,
            AssessLevelGood,
            "A foreign body (a proxy, a gateway, another server)",
            "The body does not come from the generated server - an HTML error page, XML, a JSON string, invalid JSON or another vendor's JSON. No generated type can hold it: the typed fields stay null and the text is kept in Content (for every media type with the Atc.Rest.Client error-body fix).",
            "None: this is the best a client can do with that body."),
    ];

    /// <summary>
    /// The report lists the ratings that rate a cell of this run: a rating whose loss is fixed stays a rule (a
    /// regression is rated again), but the page does not show a "fix" nothing needs any more.
    /// </summary>
    [Fact]
    public void AssessLossyCells_ListsOnlyTheRatingsOfThisRunsCells()
    {
        var cases = new JsonArray(
            new JsonObject
            {
                ["group"] = "handler",
                ["operation"] = "setAllNone",
                ["clients"] = new JsonObject
                {
                    ["cs-epo-Custom"] = new JsonObject
                    {
                        ["verdict"] = VerdictLossy,
                        ["loss"] = new JsonObject { ["kind"] = LossFields, ["into"] = CustomModel, ["fields"] = new JsonArray("title") },
                    },
                },
            });

        var listed = AssessLossyCells(cases);

        Assert.Equal([AssessCustomFormat], listed.Select(a => (string)a!["id"]!).ToList(), StringComparer.Ordinal);
        Assert.Equal(AssessCustomFormat, (string?)cases[0]!["clients"]!["cs-epo-Custom"]!["assessment"]);
    }

    /// <summary>
    /// The assessment id for a lossy cell, from its case (group, operation, mode) and what it lost; <see langword="null"/>
    /// when no rule rates that loss yet.
    /// </summary>
    private static string? AssessLoss(
        JsonObject item,
        JsonObject loss)
    {
        var group = (string?)item["group"];
        var operation = (string?)item["operation"];
        var mode = (string?)item["mode"] ?? string.Empty;
        var kind = (string?)loss["kind"];
        var into = (string?)loss["into"];
        var fields = loss["fields"]?.AsArray().Select(f => (string)f!).ToList() ?? [];

        // The declared default body (not a problem+json the host writes for an exception) read as the custom model.
        var mediaType = (string?)item["wire"]?["mediaType"];
        if (group == "default" && into == CustomModel &&
            !string.Equals(mediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase))
        {
            return AssessCustomOverDefault;
        }

        if (group == "stub")
        {
            return AssessForeignBody;
        }

        if (into == CustomModel)
        {
            return AssessCustomFormat;
        }

        if (kind == LossErrorsAsString)
        {
            return operation == "setAllNone" ? AssessPlainTextOnlyValidation : AssessStringDeclaredValidation;
        }

        if (kind == LossFields && group == "validation" && fields is ["errors"])
        {
            return AssessModelDeclaredValidation;
        }

        if (kind == LossFields && group == "handler" && mode.EndsWith("-empty", StringComparison.Ordinal))
        {
            return AssessEmptyCallDeclaredModel;
        }

        return null;
    }

    /// <summary>
    /// Adds the assessment id to every lossy client cell (cells no rule rates are left without one - FailingCells
    /// reports them), and returns the ratings that rate at least one cell, for the report.
    /// </summary>
    private static JsonArray AssessLossyCells(JsonArray cases)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in cases)
        {
            var item = node!.AsObject();
            foreach (var cell in item["clients"]!.AsObject())
            {
                if ((string?)cell.Value!["verdict"] == VerdictLossy &&
                    cell.Value["loss"] is JsonObject loss &&
                    AssessLoss(item, loss) is { } assessment)
                {
                    cell.Value["assessment"] = assessment;
                    used.Add(assessment);
                }
            }
        }

        return new JsonArray([
            .. LossAssessments.Where(a => used.Contains(a.Id)).Select(a => (JsonNode)new JsonObject
            {
                ["id"] = a.Id,
                ["level"] = a.Level,
                ["title"] = a.Title,
                ["why"] = a.Why,
                ["proposal"] = a.Proposal,
            }),
        ]);
    }
}