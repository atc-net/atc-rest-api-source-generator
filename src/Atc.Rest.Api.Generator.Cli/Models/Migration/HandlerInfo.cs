namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Information about a discovered handler.
/// </summary>
public sealed class HandlerInfo
{
    /// <summary>
    /// Gets or sets the handler class name.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file path of the handler.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the interface this handler implements.
    /// </summary>
    public string? ImplementedInterface { get; set; }

    /// <summary>
    /// Gets or sets the resource group (e.g., "Accounts", "Users").
    /// </summary>
    public string? ResourceGroup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this handler has custom logic.
    /// </summary>
    public bool HasCustomLogic { get; set; }
}
