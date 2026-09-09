namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A single source file emitted by the generator, paired with the name used to snapshot it.
/// </summary>
/// <param name="Name">
/// The fully-qualified snapshot identity derived from the generator hint name, without the
/// <c>.g.cs</c> suffix - for example <c>InlineSchemas.Generated.Reports.Models.GetReportResponse</c>.
/// </param>
/// <param name="Source">The emitted source text.</param>
internal sealed record GeneratedSnapshot(
    string Name,
    string Source);