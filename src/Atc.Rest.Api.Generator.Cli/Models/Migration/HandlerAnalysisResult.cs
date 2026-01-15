namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of handler implementation analysis.
/// </summary>
public sealed class HandlerAnalysisResult
{
    /// <summary>
    /// Gets or sets the handler implementations found.
    /// </summary>
    public List<HandlerInfo> Handlers { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether all handlers implement generated interfaces.
    /// </summary>
    public bool AllHandlersCompliant { get; set; } = true;

    /// <summary>
    /// Gets or sets handlers that don't match expected patterns.
    /// </summary>
    public List<string> NonCompliantHandlers { get; set; } = [];

    /// <summary>
    /// Gets the total number of handlers found.
    /// </summary>
    public int HandlerCount => Handlers.Count;

    /// <summary>
    /// Gets a value indicating whether handlers were found.
    /// </summary>
    public bool HasHandlers => Handlers.Count > 0;
}