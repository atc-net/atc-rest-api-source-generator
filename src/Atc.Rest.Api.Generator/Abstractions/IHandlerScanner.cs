namespace Atc.Rest.Api.Generator.Abstractions;

/// <summary>
/// Abstraction for scanning existing handler implementations.
/// Used by the domain generator to avoid generating scaffolds for already-implemented handlers.
/// Implemented by:
/// - RoslynHandlerScanner (source generator - uses Compilation.GetSymbolsWithName)
/// - NoOpHandlerScanner (CLI tool - returns empty set, always generates all handlers)
/// </summary>
public interface IHandlerScanner
{
    /// <summary>
    /// Finds all handler implementations in the current compilation/assembly.
    /// </summary>
    /// <returns>A set of handler names that are already implemented (without the "I" prefix).</returns>
    HashSet<string> FindImplementedHandlers();
}