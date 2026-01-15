namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Represents a deleted folder with its item count.
/// </summary>
public sealed class DeletedItem
{
    public string Path { get; set; } = string.Empty;

    public int ItemCount { get; set; }
}