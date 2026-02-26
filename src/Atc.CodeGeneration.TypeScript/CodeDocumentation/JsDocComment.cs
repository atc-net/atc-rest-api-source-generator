namespace Atc.CodeGeneration.TypeScript.CodeDocumentation;

public class JsDocComment
{
    public JsDocComment(string? description)
    {
        Description = description;
    }

    public JsDocComment(
        string? description,
        IReadOnlyDictionary<string, string>? parameters,
        string? returns,
        bool isDeprecated,
        string? deprecatedMessage,
        string? example)
    {
        Description = description;
        Parameters = parameters;
        Returns = returns;
        IsDeprecated = isDeprecated;
        DeprecatedMessage = deprecatedMessage;
        Example = example;
    }

    public string? Description { get; }

    public IReadOnlyDictionary<string, string>? Parameters { get; }

    public string? Returns { get; }

    public bool IsDeprecated { get; }

    public string? DeprecatedMessage { get; }

    public string? Example { get; }
}
