namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// The models a property's TypeScript type and its Zod schema name - so a model file imports them, and only them (an
/// unused import fails <c>noUnusedLocals</c> as a missing one fails the build): the property's own <c>$ref</c>, the
/// <c>$ref</c> members of an <c>allOf</c>, <c>oneOf</c> or <c>anyOf</c> (a union of them), an array's <c>$ref</c> items, and
/// whatever a dictionary's values name (<c>Record&lt;string, Cat[]&gt;</c>).
/// </summary>
internal static class TypeScriptSchemaImports
{
    /// <summary>
    /// Adds the component schema names a property schema's type names.
    /// </summary>
    /// <param name="schema">The property's schema.</param>
    /// <param name="importTypes">The names collected so far.</param>
    public static void Collect(
        IOpenApiSchema schema,
        HashSet<string> importTypes)
    {
        if (schema is OpenApiSchemaReference schemaRef)
        {
            Add(schemaRef, importTypes);
            return;
        }

        if (schema is not OpenApiSchema actualSchema)
        {
            return;
        }

        foreach (var member in (actualSchema.AllOf ?? Enumerable.Empty<IOpenApiSchema>())
                     .Concat(actualSchema.OneOf ?? Enumerable.Empty<IOpenApiSchema>())
                     .Concat(actualSchema.AnyOf ?? Enumerable.Empty<IOpenApiSchema>()))
        {
            if (member is OpenApiSchemaReference memberRef)
            {
                Add(memberRef, importTypes);
            }
        }

        if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true && actualSchema.Items is OpenApiSchemaReference itemRef)
        {
            Add(itemRef, importTypes);
        }

        // A dictionary's value is typed as a property is: a $ref, an array of them, a union of them
        if (actualSchema.AdditionalProperties is { } values)
        {
            Collect(values, importTypes);
        }
    }

    private static void Add(
        OpenApiSchemaReference reference,
        HashSet<string> importTypes)
    {
        var refName = reference.Reference.Id ?? reference.Id;
        if (refName is not null)
        {
            importTypes.Add(refName);
        }
    }
}