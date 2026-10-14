namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// Every member of the document's SignalR hubs (<c>x-signalr-hubs</c>) can be read: a payload, an answer or an argument
/// the generator does not understand is an error (<c>ATC_API_HUB001</c>) naming the hub, the member and the field, under
/// every strategy - the hub would otherwise be generated with an event or a method that does not carry the value.
/// </summary>
internal static class SignalRHubValidator
{
    public static List<DiagnosticMessage> Validate(
        OpenApiDocument document,
        string sourceFilePath)
        => SignalRHubHelper.FindProblems(document)
            .Select(problem => new DiagnosticMessage(
                RuleId: RuleIdentifiers.SignalRHubMemberUnreadable,
                Message: problem.Message,
                Severity: DiagnosticSeverity.Error,
                FilePath: sourceFilePath,
                Context: problem.Path))
            .ToList();
}