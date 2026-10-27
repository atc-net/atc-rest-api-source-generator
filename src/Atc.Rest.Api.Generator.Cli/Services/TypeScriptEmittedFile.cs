namespace Atc.Rest.Api.Generator.Cli.Services;

/// <summary>
/// One file of a <see cref="TypeScriptEmitPlan"/>.
/// </summary>
/// <param name="RelativePath">The path in the output folder, with <c>/</c> between folders.</param>
/// <param name="Content">The content.</param>
/// <param name="IsScaffold">Whether the file is a scaffold file: written once, then the user's.</param>
public sealed record TypeScriptEmittedFile(
    string RelativePath,
    string Content,
    bool IsScaffold);