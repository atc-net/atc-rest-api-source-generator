namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// The validation attributes a generated project declares for itself.
/// <list type="bullet">
///   <item><c>ApiMinLengthAttribute</c> and <c>ApiMaxLengthAttribute</c>, derived from <c>MinLengthAttribute</c> and
///   <c>MaxLengthAttribute</c>: a <c>minLength</c>/<c>maxLength</c> and a <c>minItems</c>/<c>maxItems</c> are written with
///   them, because the base constructors require unreferenced code - the base types count an unknown type through
///   reflection - and so break a trimmed or Native AOT build. Generated properties and parameters are strings and
///   collections, which the base types count without reflection; validation, its messages and ASP.NET Core's OpenAPI
///   document are those of the base types.</item>
///   <item><c>ApiRegularExpressionAttribute</c>, derived from <c>RegularExpressionAttribute</c>: a <c>pattern</c>, which
///   applies to an empty string too (the base counts <c>""</c> as valid).</item>
///   <item><c>ApiItemsRegularExpressionAttribute</c>: a <c>pattern</c> on the items of an array, each item matched.</item>
///   <item><c>ApiAbsoluteUriAttribute</c>: a <c>format: uri</c> value is an absolute URI (a relative reference is
///   <c>format: uri-reference</c>); the <c>Uri</c> it binds to takes relative text too.</item>
/// </list>
/// </summary>
public static class LengthLimitAttributesExtractor
{
    /// <summary>
    /// The attribute for <c>minLength</c> and <c>minItems</c>, as written in an attribute list.
    /// </summary>
    public const string MinLengthName = "ApiMinLength";

    /// <summary>
    /// The attribute for <c>maxLength</c> and <c>maxItems</c>, as written in an attribute list.
    /// </summary>
    public const string MaxLengthName = "ApiMaxLength";

    /// <summary>
    /// The attribute for a <c>pattern</c>, as written in an attribute list.
    /// </summary>
    public const string RegularExpressionName = "ApiRegularExpression";

    /// <summary>
    /// The attribute for a <c>pattern</c> on the items of an array, as written in an attribute list.
    /// </summary>
    public const string ItemsRegularExpressionName = "ApiItemsRegularExpression";

    /// <summary>
    /// The attribute for <c>format: uri</c>, as written in an attribute list.
    /// </summary>
    public const string AbsoluteUriName = "ApiAbsoluteUri";

    /// <summary>
    /// Whether the generator writes a length limit: a string with <c>minLength</c> or <c>maxLength</c>, or an array
    /// with <c>minItems</c> or <c>maxItems</c> - declared in place or on the primitive component it references - where
    /// validation attributes are written: on a property of an object schema, a parameter or a request body.
    /// </summary>
    /// <param name="openApiDoc">The specification.</param>
    /// <returns>True when the attributes are needed.</returns>
    public static bool IsNeeded(OpenApiDocument openApiDoc)
        => AnyValidated(openApiDoc, schema => Writes(schema, MinLengthName) || Writes(schema, MaxLengthName));

    /// <summary>
    /// The pattern and URI attributes the generator writes for this specification (<see cref="RegularExpressionName"/>,
    /// <see cref="ItemsRegularExpressionName"/>, <see cref="AbsoluteUriName"/>), in that order.
    /// </summary>
    /// <param name="openApiDoc">The specification.</param>
    /// <returns>The names of the attributes needed; empty when none is.</returns>
    public static IReadOnlyList<string> NeededValueAttributes(
        OpenApiDocument openApiDoc)
        => new[] { RegularExpressionName, ItemsRegularExpressionName, AbsoluteUriName }
            .Where(name => AnyValidated(openApiDoc, schema => Writes(schema, name)))
            .ToList();

    /// <summary>
    /// The source of the length-limit attributes, in the given namespace - the base namespace every generated namespace
    /// is in, so a model or parameters type names them without a using.
    /// </summary>
    /// <param name="namespaceName">The namespace of the generated file.</param>
    /// <param name="readsTextBody">Whether the attributes read the text of the server's text request body (the
    /// specification has one).</param>
    /// <returns>The file content.</returns>
    public static string Generate(
        string namespaceName,
        bool readsTextBody = false)
        => $$"""
             {{HeaderBuilder.WithUsings(
                 NamespaceConstants.System,
                 NamespaceConstants.SystemCodeDomCompiler,
                 NamespaceConstants.SystemComponentModelDataAnnotations,
                 "System.Diagnostics.CodeAnalysis").TrimEnd()}}

             namespace {{namespaceName}};

             /// <summary>
             /// <see cref="MinLengthAttribute"/> for a generated string or collection: the same validation, message and OpenAPI
             /// metadata, without the base constructor's demand for unreferenced code.
             /// </summary>
             [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
             [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
             internal sealed class {{MinLengthName}}Attribute : MinLengthAttribute
             {
                 [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Only on generated strings and collections, which MinLengthAttribute counts without reflection.")]
                 public {{MinLengthName}}Attribute(int length)
                     : base(length)
                 {
                 }{{TextOverride(readsTextBody)}}
             }

             /// <summary>
             /// <see cref="MaxLengthAttribute"/> for a generated string or collection: the same validation, message and OpenAPI
             /// metadata, without the base constructor's demand for unreferenced code.
             /// </summary>
             [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
             [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
             internal sealed class {{MaxLengthName}}Attribute : MaxLengthAttribute
             {
                 [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Only on generated strings and collections, which MaxLengthAttribute counts without reflection.")]
                 public {{MaxLengthName}}Attribute(int length)
                     : base(length)
                 {
                 }{{TextOverride(readsTextBody)}}
             }
             """;

    /// <summary>
    /// The source of the pattern and URI attributes the specification needs (<see cref="NeededValueAttributes"/>), in
    /// the given namespace.
    /// </summary>
    /// <param name="namespaceName">The namespace of the generated file.</param>
    /// <param name="attributeNames">The attributes to declare.</param>
    /// <param name="readsTextBody">Whether the pattern attribute reads the text of the server's text request body (the
    /// specification has one).</param>
    /// <returns>The file content.</returns>
    public static string GenerateValueAttributes(
        string namespaceName,
        IReadOnlyCollection<string> attributeNames,
        bool readsTextBody = false)
    {
        if (attributeNames is null)
        {
            throw new ArgumentNullException(nameof(attributeNames));
        }

        var declarations = new List<string>();
        if (attributeNames.Contains(RegularExpressionName, StringComparer.Ordinal))
        {
            declarations.Add(RegularExpressionDeclaration(readsTextBody));
        }

        if (attributeNames.Contains(ItemsRegularExpressionName, StringComparer.Ordinal))
        {
            declarations.Add(ItemsRegularExpressionDeclaration);
        }

        if (attributeNames.Contains(AbsoluteUriName, StringComparer.Ordinal))
        {
            declarations.Add(AbsoluteUriDeclaration);
        }

        var header = HeaderBuilder.WithUsings(
            NamespaceConstants.System,
            "System.Collections.Generic",
            NamespaceConstants.SystemCodeDomCompiler,
            NamespaceConstants.SystemComponentModelDataAnnotations,
            "System.Diagnostics.CodeAnalysis",
            "System.Globalization",
            "System.Text.RegularExpressions").TrimEnd();

        return header + "\n\nnamespace " + namespaceName + ";\n\n" + string.Join("\n\n", declarations) + "\n";
    }

    private static string GeneratedCodeAttribute
        => $"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]";

    // A length attribute on the server's text request body counts its text
    private static string TextOverride(bool readsTextBody)
        => readsTextBody
            ? $$"""


                 public override bool IsValid(object? value)
                     => base.IsValid(value is {{TextRequestBodyExtractor.TypeName}} text ? text.Text : value);
             """.TrimEnd()
            : string.Empty;

    private static string RegularExpressionDeclaration(bool readsTextBody)
        => $$"""
             /// <summary>
             /// <see cref="RegularExpressionAttribute"/> for a <c>pattern</c> in the specification: the same validation, message and
             /// OpenAPI metadata, and an empty string is matched against the pattern too - the base counts it as valid, while the
             /// specification's pattern applies to every string.
             /// </summary>
             {{GeneratedCodeAttribute}}
             [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
             internal sealed class {{RegularExpressionName}}Attribute : RegularExpressionAttribute
             {
                 public {{RegularExpressionName}}Attribute([StringSyntax(StringSyntaxAttribute.Regex)] string pattern)
                     : base(pattern)
                 {
                 }

                 public override bool IsValid(object? value)
                 {{{(readsTextBody ? $"\n                     value = value is {TextRequestBodyExtractor.TypeName} text ? text.Text : value;" : string.Empty)}}
                     if (value is not string { Length: 0 })
                     {
                         return base.IsValid(value);
                     }

                     var match = Regex.Match(string.Empty, Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(MatchTimeoutInMilliseconds));
                     return match.Success && match.Index == 0 && match.Length == 0;
                 }
             }
             """;

    private static string ItemsRegularExpressionDeclaration
        => $$"""
             /// <summary>
             /// A <c>pattern</c> on the items of an array: every item of the list must match it, as
             /// <see cref="RegularExpressionAttribute"/> matches one string - the whole item, an empty one too. The message names
             /// the first item that does not match, by its position.
             /// </summary>
             {{GeneratedCodeAttribute}}
             [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
             internal sealed class {{ItemsRegularExpressionName}}Attribute : ValidationAttribute
             {
                 private readonly Regex regex;

                 public {{ItemsRegularExpressionName}}Attribute([StringSyntax(StringSyntaxAttribute.Regex)] string pattern)
                     : base("The field {0} must hold items that each match the regular expression '{1}'.")
                 {
                     Pattern = pattern;
                     regex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
                 }

                 /// <summary>
                 /// The pattern every item must match.
                 /// </summary>
                 public string Pattern { get; }

                 public override string FormatErrorMessage(string name)
                     => string.Format(CultureInfo.CurrentCulture, ErrorMessageString, name, Pattern);

                 public override bool IsValid(object? value)
                     => FirstMismatch(value) < 0;

                 protected override ValidationResult? IsValid(
                     object? value,
                     ValidationContext validationContext)
                 {
                     ArgumentNullException.ThrowIfNull(validationContext);
                     var index = FirstMismatch(value);
                     if (index < 0)
                     {
                         return ValidationResult.Success;
                     }

                     var message = $"{FormatErrorMessage(validationContext.DisplayName)} Item {index.ToString(CultureInfo.InvariantCulture)} does not.";
                     return validationContext.MemberName is { } memberName
                         ? new ValidationResult(message, new[] { memberName })
                         : new ValidationResult(message);
                 }

                 private int FirstMismatch(object? value)
                 {
                     if (value is not IEnumerable<string?> items)
                     {
                         return -1;
                     }

                     var index = 0;
                     foreach (var item in items)
                     {
                         if (item is not null)
                         {
                             var match = regex.Match(item);
                             if (!match.Success || match.Index != 0 || match.Length != item.Length)
                             {
                                 return index;
                             }
                         }

                         index++;
                     }

                     return -1;
                 }
             }
             """;

    private static string AbsoluteUriDeclaration
        => $$"""
             /// <summary>
             /// A <c>format: uri</c> value is an absolute URI, as the specification defines the format (a relative reference is
             /// <c>format: uri-reference</c>): <see cref="Uri"/> binds relative text too. A list's items are each absolute.
             /// </summary>
             {{GeneratedCodeAttribute}}
             [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
             internal sealed class {{AbsoluteUriName}}Attribute : ValidationAttribute
             {
                 public {{AbsoluteUriName}}Attribute()
                     : base("The field {0} must be an absolute URI.")
                 {
                 }

                 public override bool IsValid(object? value)
                     => value switch
                     {
                         Uri uri => uri.IsAbsoluteUri,
                         IEnumerable<Uri?> uris => System.Linq.Enumerable.All(uris, item => item is null || item.IsAbsoluteUri),
                         _ => true,
                     };
             }
             """;

    private static bool AnyValidated(
        OpenApiDocument openApiDoc,
        Func<IOpenApiSchema?, bool> predicate)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (SchemaWalker.Any(openApiDoc, schema => schema.Properties?.Values.Any(predicate) == true))
        {
            return true;
        }

        var operations = (openApiDoc.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
            .Concat(openApiDoc.Webhooks?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
            .Where(pathItem => pathItem is not null)
            .SelectMany(pathItem => (pathItem.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
                .Where(operation => operation is not null)
                .Select(operation => (PathItem: pathItem, Operation: operation)));

        return operations.Any(o =>
            (o.PathItem.Parameters ?? []).Concat(o.Operation.Parameters ?? []).Any(p => predicate(p?.Schema)) ||
            (o.Operation.RequestBody?.Content?.Values ?? Enumerable.Empty<IOpenApiMediaType>()).Any(m => predicate(m?.Schema) || predicate(m?.ItemSchema)));
    }

    // As OpenApiSchemaExtensions.GetValidationAttributes writes them: the name alone, or with its arguments
    private static bool Writes(
        IOpenApiSchema? schema,
        string attributeName)
        => schema is not null &&
           schema.GetValidationAttributes(isRequired: false).Any(a =>
               string.Equals(a, attributeName, StringComparison.Ordinal) ||
               a.StartsWith(attributeName + "(", StringComparison.Ordinal));
}