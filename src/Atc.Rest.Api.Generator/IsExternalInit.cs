// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill for init-only setters support in netstandard2.0.
/// This type is required for C# 9 init-only setters to work in netstandard2.0.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar", "S2094:Classes should not be empty", Justification = "Required polyfill for init-only setters in netstandard2.0")]
internal static class IsExternalInit
{
}