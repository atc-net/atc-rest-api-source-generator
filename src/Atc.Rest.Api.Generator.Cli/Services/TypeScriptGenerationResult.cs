namespace Atc.Rest.Api.Generator.Cli.Services;

/// <summary>
/// Result of TypeScript client generation.
/// </summary>
public record TypeScriptGenerationResult(
    int ModelCount,
    int EnumCount,
    int ErrorTypeCount = 0,
    int TypeCount = 0,
    int ClientCount = 0);