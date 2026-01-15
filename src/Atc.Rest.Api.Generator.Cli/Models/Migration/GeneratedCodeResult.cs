namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of generated code analysis.
/// </summary>
public sealed class GeneratedCodeResult
{
    /// <summary>
    /// Gets or sets the number of generated server files found.
    /// </summary>
    public int ServerFileCount { get; set; }

    /// <summary>
    /// Gets or sets the number of generated client files found.
    /// </summary>
    public int ClientFileCount { get; set; }

    /// <summary>
    /// Gets or sets the generator version detected from generated code.
    /// </summary>
    public string? GeneratorVersion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether IApiContractAssemblyMarker was found.
    /// </summary>
    public bool HasApiContractMarker { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether IDomainAssemblyMarker was found.
    /// </summary>
    public bool HasDomainMarker { get; set; }

    /// <summary>
    /// Gets or sets the detected namespaces in generated code.
    /// </summary>
    public List<string> DetectedNamespaces { get; set; } = [];

    /// <summary>
    /// Gets or sets generated handler interfaces found.
    /// </summary>
    public List<string> HandlerInterfaces { get; set; } = [];

    /// <summary>
    /// Gets or sets generated endpoint definitions found.
    /// </summary>
    public List<string> EndpointDefinitions { get; set; } = [];

    /// <summary>
    /// Gets or sets generated model/contract types found.
    /// </summary>
    public List<string> ModelTypes { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether generated code was found.
    /// </summary>
    public bool HasGeneratedCode => ServerFileCount > 0 || ClientFileCount > 0;

    /// <summary>
    /// Gets the total number of generated files.
    /// </summary>
    public int TotalFileCount => ServerFileCount + ClientFileCount;
}
