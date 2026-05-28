namespace Atc.Rest.Api.Generator.Cli.Services;

/// <summary>
/// Result of TypeScript client generation.
/// </summary>
public record TypeScriptGenerationResult(
    int ModelCount,
    int EnumCount,
    int ErrorTypeCount = 0,
    int TypeCount = 0,
    int ClientCount = 0,
    int HookCount = 0,
    int ZodSchemaCount = 0,
    int MswHandlerCount = 0,
    bool ScaffoldGenerated = false)
{
    /// <summary>
    /// Non-fatal advisories collected during generation (e.g. spec features the TypeScript
    /// emitter intentionally skips). Empty when generation found nothing worth flagging.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}