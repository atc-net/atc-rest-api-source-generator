namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// What a client class computes for one operation before its request method is written: its result union, the error
/// statuses it has arms for, the response headers it reads and its security option.
/// </summary>
/// <param name="ResultTypeName">The operation's own result union, or <see langword="null"/> (a stream).</param>
/// <param name="ArmsOption">The error statuses the result union has arms for, or <see langword="null"/>.</param>
/// <param name="HeaderReadersOption">The response headers the result reads, or <see langword="null"/>.</param>
/// <param name="SecurityOption">The request's security option, or <see langword="null"/>.</param>
internal sealed record TypeScriptOperationAnswer(
    string? ResultTypeName,
    string? ArmsOption,
    IReadOnlyList<string>? HeaderReadersOption,
    string? SecurityOption);