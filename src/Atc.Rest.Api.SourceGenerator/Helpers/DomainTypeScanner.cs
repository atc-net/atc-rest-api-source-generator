namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// What the domain generator needs to know about the project's own types - the handlers, the FluentValidation
/// validators and the namespaces of handler interfaces - read per type declaration with a syntax provider. An edit
/// re-reads only the declarations of the edited file; the other files' facts stay cached, so a keystroke does not walk
/// every type of the compilation.
/// </summary>
internal static class DomainTypeScanner
{
    /// <summary>
    /// The tracking name of the per-declaration step (for the incremental tests).
    /// </summary>
    public const string TrackingName = "DomainTypeFacts";

    /// <summary>
    /// A declaration worth a semantic look: a top-level class, record or struct that names a base or an interface, or
    /// whose name ends with <c>Handler</c>; or an interface named <c>I…Handler</c>, nested or not.
    /// </summary>
    public static bool IsCandidate(SyntaxNode node)
    {
        switch (node)
        {
            case InterfaceDeclarationSyntax interfaceDeclaration:
                var interfaceName = interfaceDeclaration.Identifier.ValueText;
                return interfaceName.StartsWith("I", StringComparison.Ordinal) &&
                       interfaceName.EndsWith("Handler", StringComparison.Ordinal);
            case TypeDeclarationSyntax typeDeclaration when typeDeclaration is ClassDeclarationSyntax or RecordDeclarationSyntax or StructDeclarationSyntax:
                // Only top-level types, as before: a nested class is no handler or validator the registration can name
                return typeDeclaration.Parent is not TypeDeclarationSyntax &&
                       (typeDeclaration.BaseList is not null ||
                        typeDeclaration.Identifier.ValueText.EndsWith("Handler", StringComparison.Ordinal));
            default:
                return false;
        }
    }

    /// <summary>
    /// The facts of one candidate declaration, or null when it is none of the three.
    /// </summary>
    public static DomainTypeFacts? Read(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken) is not INamedTypeSymbol symbol)
        {
            return null;
        }

        var ns = symbol.ContainingNamespace is { IsGlobalNamespace: false } containing
            ? containing.ToDisplayString()
            : string.Empty;

        if (symbol.TypeKind == TypeKind.Interface)
        {
            return ns.Length > 0
                ? new DomainTypeFacts(new EquatableArray<HandlerInfo>(ImmutableArray<HandlerInfo>.Empty), Validator: null, InterfaceNamespace: ns)
                : null;
        }

        var handlers = symbol.IsAbstract
            ? ImmutableArray<HandlerInfo>.Empty
            : FindHandlerNames(symbol)
                .Select(name => new HandlerInfo(name, ns))
                .ToImmutableArray();
        var validator = FindValidator(symbol, ns);

        return handlers.Length == 0 && validator is null
            ? null
            : new DomainTypeFacts(new EquatableArray<HandlerInfo>(handlers), validator, InterfaceNamespace: null);
    }

    /// <summary>
    /// The project's facts as one summary, sorted, without the duplicates a partial type gives.
    /// </summary>
    public static DomainCompilationSummary Summarize(
        ImmutableArray<DomainTypeFacts> facts,
        DomainReferences references)
    {
        var handlers = facts
            .SelectMany(f => f.Handlers.Values)
            .Distinct()
            .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(h => h.Namespace, StringComparer.Ordinal)
            .ToImmutableArray();

        var interfaceNamespaces = facts
            .Select(f => f.InterfaceNamespace)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ns => ns, StringComparer.Ordinal)
            .ToImmutableArray();

        var validators = facts
            .Select(f => f.Validator)
            .OfType<ValidatorInfo>()
            .Distinct()
            .OrderBy(v => v.Name, StringComparer.Ordinal)
            .ThenBy(v => v.Namespace, StringComparer.Ordinal)
            .ToImmutableArray();

        return new DomainCompilationSummary(
            references.HasAspNetCore,
            references.HasLogging,
            references.AssemblyName,
            new EquatableArray<HandlerInfo>(handlers),
            new EquatableArray<string>(interfaceNamespaces),
            new EquatableArray<ValidatorInfo>(validators));
    }

    /// <summary>
    /// The handler names a type stands for: one per <c>I…Handler</c> interface it implements (the name without the
    /// <c>I</c>), and its own name when it ends with <c>Handler</c> and is declared in the project's sources (not in a
    /// generated file) - that catches a handler whose interface is not resolved yet.
    /// </summary>
    private static IEnumerable<string> FindHandlerNames(INamedTypeSymbol symbol)
    {
        var names = new List<string>();
        foreach (var interfaceSymbol in symbol.AllInterfaces)
        {
            var interfaceName = interfaceSymbol.Name;
            if (interfaceName.StartsWith("I", StringComparison.Ordinal) &&
                interfaceName.EndsWith("Handler", StringComparison.Ordinal) &&
                !names.Contains(interfaceName.Substring(1)))
            {
                names.Add(interfaceName.Substring(1));
            }
        }

        if (symbol.Name.EndsWith("Handler", StringComparison.Ordinal) &&
            !names.Contains(symbol.Name) &&
            symbol.DeclaringSyntaxReferences.FirstOrDefault() is { } syntaxReference &&
            !IsGeneratedFile(syntaxReference.SyntaxTree.FilePath))
        {
            names.Add(symbol.Name);
        }

        return names;
    }

    private static bool IsGeneratedFile(string filePath)
        => filePath.Contains("obj" + Path.DirectorySeparatorChar + "Generated") ||
           filePath.Contains("obj" + Path.AltDirectorySeparatorChar + "Generated") ||
           filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The validator a type is: the first <c>FluentValidation.AbstractValidator&lt;T&gt;</c> up its base types.
    /// </summary>
    private static ValidatorInfo? FindValidator(
        INamedTypeSymbol symbol,
        string ns)
    {
        for (var baseType = symbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.Name == "AbstractValidator" &&
                baseType.TypeArguments.Length == 1 &&
                baseType.ContainingNamespace?.ToDisplayString() == "FluentValidation")
            {
                return new ValidatorInfo(symbol.Name, ns, baseType.TypeArguments[0].ToDisplayString());
            }
        }

        return null;
    }
}