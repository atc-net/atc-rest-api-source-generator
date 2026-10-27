namespace Atc.OpenApi.Models;

/// <summary>
/// The <c>x-</c> extensions an operation inherits, nearest first: the operation's own, its path item's, the document's -
/// how <c>x-retry-*</c>, <c>x-cache-*</c> and <c>x-ratelimit-*</c> settings are read (the operation overrides its path,
/// the path overrides the document).
/// </summary>
/// <param name="operation">The operation's extensions.</param>
/// <param name="path">The path item's extensions.</param>
/// <param name="document">The document's extensions.</param>
public sealed class OpenApiExtensionScope(
    IDictionary<string, IOpenApiExtension>? operation,
    IDictionary<string, IOpenApiExtension>? path,
    IDictionary<string, IOpenApiExtension>? document)
{
    /// <summary>
    /// Gets the operation's extensions.
    /// </summary>
    public IDictionary<string, IOpenApiExtension>? Operation { get; } = operation;

    /// <summary>
    /// Gets the path item's extensions.
    /// </summary>
    public IDictionary<string, IOpenApiExtension>? Path { get; } = path;

    /// <summary>
    /// Gets the document's extensions.
    /// </summary>
    public IDictionary<string, IOpenApiExtension>? Document { get; } = document;

    /// <summary>
    /// The nearest value a setting has: the operation's, else the path item's, else the document's.
    /// </summary>
    /// <typeparam name="T">The value's type, nullable (<c>int?</c>, <c>string?</c>).</typeparam>
    /// <param name="read">Reads the setting from one level's extensions.</param>
    /// <returns>The nearest value, or <see langword="null"/> when no level has one.</returns>
    public T First<T>(Func<IDictionary<string, IOpenApiExtension>?, T> read)
    {
        if (read is null)
        {
            throw new ArgumentNullException(nameof(read));
        }

        var value = read(Operation);
        if (value is not null)
        {
            return value;
        }

        value = read(Path);
        return value is not null
            ? value
            : read(Document);
    }
}