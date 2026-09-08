namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Projects a generated typed-client <see cref="ClassParameters"/> into the matching
/// <c>I{ClientName}</c> <see cref="InterfaceParameters"/>.
/// <para>
/// The interface exists so consumers can depend on - and mock - the API contract instead of
/// stubbing <c>HttpClient</c>.
/// </para>
/// </summary>
public static class HttpClientInterfaceExtractor
{
    /// <summary>
    /// Builds the interface name for a client type name, e.g. <c>PetsClient</c> =&gt; <c>IPetsClient</c>.
    /// </summary>
    public static string BuildInterfaceName(string clientTypeName)
        => $"I{clientTypeName}";

    /// <summary>
    /// Projects the public operation methods of the client class onto an interface.
    /// Returns <see langword="null"/> when there is nothing to expose.
    /// </summary>
    /// <param name="clientClass">The extracted typed-client class parameters.</param>
    public static InterfaceParameters? Extract(ClassParameters? clientClass)
    {
        if (clientClass is null)
        {
            return null;
        }

        var methods = clientClass.Methods?
            .Where(IsOperationMethod)
            .Select(ToInterfaceMethod)
            .ToList();

        if (methods is null || methods.Count == 0)
        {
            return null;
        }

        return new InterfaceParameters(
            HeaderContent: clientClass.HeaderContent,
            Namespace: clientClass.Namespace,
            DocumentationTags: null,
            Attributes:
            [
                new("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\""),
            ],
            DeclarationModifier: DeclarationModifiers.PublicInterface,
            InterfaceTypeName: BuildInterfaceName(clientClass.ClassTypeName),
            InheritedInterfaceTypeName: null,
            Properties: null,
            Methods: methods);
    }

    /// <summary>
    /// Only the public operation methods belong on the interface - private helpers such as
    /// <c>EnsureSuccessAsync</c> are an implementation detail.
    /// </summary>
    private static bool IsOperationMethod(MethodParameters method)
        => method.DeclarationModifier is DeclarationModifiers.PublicAsync or DeclarationModifiers.Public;

    /// <summary>
    /// Strips everything that must not appear on an interface member: the declaration modifier
    /// (interface members carry none), the body, method-level attributes and parameter-level
    /// attributes such as <c>[EnumeratorCancellation]</c>, which is only valid on an implementation.
    /// </summary>
    private static MethodParameters ToInterfaceMethod(MethodParameters method)
        => method with
        {
            DeclarationModifier = DeclarationModifiers.None,
            Attributes = null,
            Content = null,
            UseExpressionBody = false,
            Parameters = method.Parameters?
                .Select(x => x with { Attributes = null })
                .ToList(),
        };
}