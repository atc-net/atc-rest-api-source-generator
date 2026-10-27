namespace Atc.CodeGeneration.TypeScript.Content;

/// <summary>
/// The keywords a TypeScript declaration or member can carry, combinable as flags; rendered in the order
/// <c>export [default]</c>, <c>abstract</c>, <c>async</c>, <c>readonly</c>.
/// </summary>
[Flags]
public enum TypeScriptModifiers
{
    /// <summary>
    /// No keyword is written.
    /// </summary>
    None = 0,

    /// <summary>
    /// The <c>export</c> keyword.
    /// </summary>
    Export = 1 << 0,

    /// <summary>
    /// The <c>export default</c> keywords; takes precedence over <see cref="Export"/>.
    /// </summary>
    ExportDefault = 1 << 1,

    /// <summary>
    /// The <c>abstract</c> keyword.
    /// </summary>
    Abstract = 1 << 2,

    /// <summary>
    /// The <c>readonly</c> keyword.
    /// </summary>
    Readonly = 1 << 3,

    /// <summary>
    /// The <c>async</c> keyword.
    /// </summary>
    Async = 1 << 4,
}