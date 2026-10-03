namespace Atc.CodeGeneration.TypeScript.CodeDocumentation;

/// <summary>
/// The parts of a JSDoc comment: description, <c>@param</c>s, <c>@returns</c>, <c>@deprecated</c> and <c>@example</c>.
/// </summary>
public class JsDocComment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsDocComment"/> class with a description only.
    /// </summary>
    /// <param name="description">The description text, or null for none.</param>
    public JsDocComment(string? description)
    {
        Description = description;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsDocComment"/> class with every part.
    /// </summary>
    /// <param name="description">The description text, or null for none.</param>
    /// <param name="parameters">The <c>@param</c> descriptions by parameter name, or null for none.</param>
    /// <param name="returns">The <c>@returns</c> text, or null for none.</param>
    /// <param name="isDeprecated">True to write a <c>@deprecated</c> tag.</param>
    /// <param name="deprecatedMessage">The text after <c>@deprecated</c>, or null for a bare tag.</param>
    /// <param name="example">The <c>@example</c> code, or null for none.</param>
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

    /// <summary>
    /// Gets the description text, or null for none.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the <c>@param</c> descriptions by parameter name, or null for none.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; }

    /// <summary>
    /// Gets the <c>@returns</c> text, or null for none.
    /// </summary>
    public string? Returns { get; }

    /// <summary>
    /// Gets a value indicating whether a <c>@deprecated</c> tag is written.
    /// </summary>
    public bool IsDeprecated { get; }

    /// <summary>
    /// Gets the text after <c>@deprecated</c>, or null for a bare tag.
    /// </summary>
    public string? DeprecatedMessage { get; }

    /// <summary>
    /// Gets the <c>@example</c> code, or null for none.
    /// </summary>
    public string? Example { get; }
}