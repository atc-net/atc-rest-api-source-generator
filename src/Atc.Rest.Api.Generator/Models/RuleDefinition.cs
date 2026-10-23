namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// A rule of the <see cref="RuleCatalog"/>.
/// </summary>
/// <param name="Title">The rule's title.</param>
/// <param name="MessageFormat">The rule's message, with <c>{0}</c>-style arguments.</param>
/// <param name="Severity">The rule's default severity.</param>
public sealed record RuleDefinition(
    string Title,
    string MessageFormat,
    DiagnosticSeverity Severity);