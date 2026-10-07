namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// The length-limit attributes of a generated project: <c>ApiMinLengthAttribute</c> and <c>ApiMaxLengthAttribute</c>,
/// derived from <c>MinLengthAttribute</c> and <c>MaxLengthAttribute</c>. A <c>minLength</c>/<c>maxLength</c> and a
/// <c>minItems</c>/<c>maxItems</c> are written with them, because the base constructors require unreferenced code -
/// the base types count an unknown type through reflection - and so break a trimmed or Native AOT build. Generated
/// properties and parameters are strings and collections, which the base types count without reflection; validation,
/// its messages and ASP.NET Core's OpenAPI document are those of the base types.
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
    /// Whether the generator writes a length limit: a string with <c>minLength</c> or <c>maxLength</c>, or an array
    /// with <c>minItems</c> or <c>maxItems</c>, declared in place (not through a <c>$ref</c>) where validation
    /// attributes are written - on a property of an object schema, a parameter or a request body.
    /// </summary>
    /// <param name="openApiDoc">The specification.</param>
    /// <returns>True when the attributes are needed.</returns>
    public static bool IsNeeded(OpenApiDocument openApiDoc)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        if (SchemaWalker.Any(openApiDoc, schema => schema.Properties?.Values.Any(HasLengthLimit) == true))
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
            (o.PathItem.Parameters ?? []).Concat(o.Operation.Parameters ?? []).Any(p => HasLengthLimit(p?.Schema)) ||
            (o.Operation.RequestBody?.Content?.Values ?? Enumerable.Empty<IOpenApiMediaType>()).Any(m => HasLengthLimit(m?.Schema)));
    }

    /// <summary>
    /// The source of the attributes, in the given namespace - the base namespace every generated namespace is in,
    /// so a model or parameters type names them without a using.
    /// </summary>
    /// <param name="namespaceName">The namespace of the generated file.</param>
    /// <returns>The file content.</returns>
    public static string Generate(string namespaceName)
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
                 }
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
                 }
             }
             """;

    // As OpenApiSchemaExtensions.GetValidationAttributes writes them: only on a schema declared in place
    private static bool HasLengthLimit(IOpenApiSchema? schema)
        => schema is OpenApiSchema actual &&
           ((actual.Type?.HasFlag(JsonSchemaType.String) == true && (actual.MinLength > 0 || actual.MaxLength > 0)) ||
            (actual.Type?.HasFlag(JsonSchemaType.Array) == true && (actual.MinItems > 0 || actual.MaxItems > 0)));
}