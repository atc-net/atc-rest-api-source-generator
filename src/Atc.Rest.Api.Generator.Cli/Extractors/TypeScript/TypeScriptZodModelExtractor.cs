namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Extracts OpenAPI schema definitions and converts them to Zod validation schema content.
/// </summary>
public static class TypeScriptZodModelExtractor
{
    /// <summary>
    /// Extracts all object model definitions from OpenAPI document components and generates Zod schemas.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="config">TypeScript client generation configuration.</param>
    /// <param name="enumNames">Names of types that are enums (for correct import paths).</param>
    /// <returns>List of (TypeName, GeneratedContent) tuples for each model Zod schema.</returns>
    public static List<(string Name, string Content)> Extract(
        OpenApiDocument openApiDoc,
        TypeScriptClientConfig config,
        HashSet<string>? enumNames = null)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);
        ArgumentNullException.ThrowIfNull(config);

        var results = new List<(string Name, string Content)>();

        if (openApiDoc.Components?.Schemas is null || openApiDoc.Components.Schemas.Count == 0)
        {
            return results;
        }

        var headerContent = config.GenerateFileHeaders ? TypeScriptHeaderBuilder.FileHeader : null;

        // Build the cross-schema cycle map once. Each cyclic schema maps to the set of
        // sibling schemas in the same strongly-connected component — when emitting a
        // property whose $ref points into that set, the emitter wraps with z.lazy(...).
        // Direct self-references are still caught by SchemaReferencesType; this map adds
        // mutual-recursion coverage (Tag <-> Category style).
        var cycleMembers = BuildCycleMemberMap(openApiDoc);
        var discriminators = TypeScriptDiscriminatorLiterals.Collect(openApiDoc);

        foreach (var schema in openApiDoc.Components.Schemas)
        {
            var schemaName = schema.Key;
            var schemaValue = schema.Value;

            // Skip schema references
            if (schemaValue is OpenApiSchemaReference)
            {
                continue;
            }

            // Skip deprecated schemas if not including them
            if (!config.IncludeDeprecated && schemaValue is OpenApiSchema { Deprecated: true })
            {
                continue;
            }

            if (schemaValue is not OpenApiSchema actualSchema)
            {
                continue;
            }

            // Skip array and dictionary schemas - handled by ExtractArrayTypeAliases
            if (actualSchema.IsOfType(JsonSchemaType.Array) || TypeScriptModelExtractor.IsRecordAliasSchema(actualSchema))
            {
                continue;
            }

            // Skip enum schemas - handled by TypeScriptZodEnumExtractor
            if (actualSchema.IsOfType(JsonSchemaType.String) && actualSchema.Enum is { Count: > 0 })
            {
                continue;
            }

            // Only process object schemas - the same set the TypeScript models are emitted for,
            // including allOf compositions without an explicit `type`.
            if (!TypeScriptModelExtractor.IsObjectSchema(actualSchema))
            {
                continue;
            }

            cycleMembers.TryGetValue(schemaName, out var cyclePeers);
            var content = GenerateZodObject(schemaName, actualSchema, headerContent, enumNames, config.NamingStrategy, cyclePeers, discriminators, config.ConvertDates);
            if (content is not null)
            {
                results.Add((schemaName, content));
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts a Zod schema for every union type alias (top-level <c>oneOf</c> / <c>anyOf</c> of
    /// <c>$ref</c>s): <c>export const PaymentMethodSchema = z.union([CreditCardSchema, PayPalSchema]);</c>.
    /// The clients validate a response of that type against it (<c>--zod-runtime-validate</c>).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="config">TypeScript client generation configuration.</param>
    /// <returns>List of (TypeName, FileContent) tuples for each union schema.</returns>
    public static List<(string Name, string Content)> ExtractUnionTypeAliases(
        OpenApiDocument openApiDoc,
        TypeScriptClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);
        ArgumentNullException.ThrowIfNull(config);

        var results = new List<(string Name, string Content)>();
        var headerContent = config.GenerateFileHeaders ? TypeScriptHeaderBuilder.FileHeader : null;

        var cycleMembers = BuildCycleMemberMap(openApiDoc);

        foreach (var (schemaName, leaves, discriminator) in TypeScriptModelExtractor.CollectUnionAliases(openApiDoc, config))
        {
            var sb = new StringBuilder();
            if (headerContent is not null)
            {
                sb.Append(headerContent);
            }

            if (leaves.Count > 1)
            {
                sb.AppendLine("import { z } from 'zod';");
                sb.AppendLine();
            }

            foreach (var leaf in leaves)
            {
                sb.Append("import { ").Append(leaf).Append("Schema } from './").Append(leaf).AppendLine(".zod';");
            }

            var schemas = string.Join(", ", leaves.Select(l => l + "Schema"));
            sb.AppendLine();
            sb.Append("export const ").Append(schemaName).Append("Schema = ");
            if (leaves.Count == 1)
            {
                sb.Append(leaves[0]).Append("Schema");
            }
            else if (discriminator is not null && leaves.TrueForAll(l => IsDiscriminatedObject(openApiDoc, l, discriminator, cycleMembers)))
            {
                // The option schemas carry the property under the models' name (the naming strategy), so the union
                // dispatches on that name: `payment_type` on the wire is `paymentType` in CardSchema
                var discriminatorKey = TypeScriptPropertyNames.Resolve(discriminator, VariantPropertyKeys(openApiDoc, leaves[0]), config.NamingStrategy);
                sb.Append("z.discriminatedUnion('").Append(discriminatorKey).Append("', [").Append(schemas).Append("])");
            }
            else
            {
                // A plain union takes the first variant that parses, and an object schema strips the keys it does not
                // know - {x, y} against [A{x}, B{x, y}] came back as {x}. The object members pass unknown keys through,
                // so the data stays what the server sent (a cyclic member is a ZodType, which has no passthrough).
                var members = string.Join(", ", leaves.Select(l => l + "Schema" + (IsPlainObject(openApiDoc, l, cycleMembers) ? ".passthrough()" : string.Empty)));
                sb.Append("z.union([").Append(members).Append("])");
            }

            sb.AppendLine(";");

            results.Add((schemaName, sb.ToString()));
        }

        return results;
    }

    /// <summary>
    /// Extracts array wrapper schemas and generates Zod array schemas.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document containing schema definitions.</param>
    /// <param name="config">TypeScript client generation configuration.</param>
    /// <returns>List of (TypeName, GeneratedContent) tuples for each array type Zod schema.</returns>
    public static List<(string Name, string Content)> ExtractArrayTypeAliases(
        OpenApiDocument openApiDoc,
        TypeScriptClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);
        ArgumentNullException.ThrowIfNull(config);

        var results = new List<(string Name, string Content)>();

        if (openApiDoc.Components?.Schemas is null || openApiDoc.Components.Schemas.Count == 0)
        {
            return results;
        }

        var headerContent = config.GenerateFileHeaders ? TypeScriptHeaderBuilder.FileHeader : null;

        // An array alias in a cycle (NodeList: array of Node, Node.children: NodeList) is evaluated lazily, or
        // whichever module loads first reads the other's binding before it is initialized
        var cycleMembers = BuildCycleMemberMap(openApiDoc);

        foreach (var schema in openApiDoc.Components.Schemas)
        {
            var schemaName = schema.Key;
            var schemaValue = schema.Value;

            if (schemaValue is not OpenApiSchema actualSchema)
            {
                continue;
            }

            var isScalarAlias = TypeScriptModelExtractor.IsScalarAliasSchema(actualSchema);
            var isRecordAlias = TypeScriptModelExtractor.IsRecordAliasSchema(actualSchema);
            if (!actualSchema.IsOfType(JsonSchemaType.Array) && !isScalarAlias && !isRecordAlias)
            {
                continue;
            }

            // Skip deprecated schemas if not including them
            if (!config.IncludeDeprecated && actualSchema.Deprecated)
            {
                continue;
            }

            if (isScalarAlias)
            {
                results.Add((schemaName, GenerateZodScalarAlias(schemaName, actualSchema, headerContent, config.ConvertDates, config.ExtensibleEnums)));
                continue;
            }

            if (isRecordAlias)
            {
                results.Add((schemaName, GenerateZodRecordAlias(schemaName, actualSchema, headerContent, config.ConvertDates)));
                continue;
            }

            var content = GenerateZodArray(schemaName, actualSchema, headerContent, config.ConvertDates, cycleMembers.ContainsKey(schemaName));
            results.Add((schemaName, content));
        }

        return results;
    }

    /// <summary>
    /// A JSON Schema <c>pattern</c> as the body of a JavaScript regex literal (<c>/.../</c>): an unescaped
    /// <c>/</c> would end the literal (<c>^\d{2}/\d{2}$</c>), and a line terminator is not allowed in one.
    /// Existing escapes are kept as they are.
    /// </summary>
    internal static string ToRegexLiteralBody(string pattern)
    {
        var sb = new StringBuilder(pattern.Length + 4);
        var escaped = false;
        foreach (var c in pattern)
        {
            // The character after a backslash is part of an escape and stays as it is.
            if (escaped || c == '\\')
            {
                sb.Append(c);
                escaped = !escaped;
                continue;
            }

            sb.Append(c switch
            {
                '/' => "\\/",
                '\n' => "\\n",
                '\r' => "\\r",
                (char)0x2028 => "\\u2028",
                (char)0x2029 => "\\u2029",
                _ => c.ToString(),
            });
        }

        return sb.ToString();
    }

    /// <summary>
    /// A JSON Schema <c>pattern</c> as a JavaScript regex literal (<c>/.../flags</c>). A pattern written for .NET
    /// is translated where JavaScript has an equivalent: the inline options <c>(?i)</c>, <c>(?m)</c>, <c>(?s)</c>
    /// at the start become the literal's flags (JavaScript has no inline options - a syntax error), <c>\A</c> is
    /// <c>^</c>, and <c>\Z</c> / <c>\z</c> are <c>$</c> (in JavaScript they are identity escapes matching a letter).
    /// A pattern without <c>^...$</c> is anchored: the value matches as a whole, as on the generated server.
    /// </summary>
    internal static string ToRegexLiteral(string pattern)
    {
        var flags = new StringBuilder();
        var body = pattern;
        var options = Regex.Match(body, @"^\(\?(?<options>[imsx]+)\)", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
        if (options.Success)
        {
            foreach (var option in options.Groups["options"].Value.Where(o => o is 'i' or 'm' or 's').Distinct())
            {
                flags.Append(option);
            }

            body = body.Substring(options.Length);
        }

        // \A, \Z and \z outside an escape (a preceding backslash escapes the backslash)
        body = Regex.Replace(body, @"(?<!\\)(?<pairs>(?:\\\\)*)\\A", "${pairs}^", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
        body = Regex.Replace(body, @"(?<!\\)(?<pairs>(?:\\\\)*)\\[Zz]", "${pairs}$$", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

        // The whole value, as the generated server matches it (ApiRegularExpression): a pattern without ^...$ is anchored
        // (ATC_API_SCH028 tells the specification's author)
        if (!(body.StartsWith("^", StringComparison.Ordinal) && body.EndsWith("$", StringComparison.Ordinal) && !body.EndsWith("\\$", StringComparison.Ordinal)))
        {
            body = "^(?:" + body + ")$";
        }

        return "/" + ToRegexLiteralBody(body) + "/" + flags;
    }

    /// <summary>
    /// Builds a map from each cyclic schema name to the set of sibling schemas in its
    /// strongly-connected component — i.e. the set of names that are mutually reachable
    /// via direct <c>$ref</c> / array-item-<c>$ref</c> / oneOf|anyOf|allOf / additionalProperties.
    /// Schemas not participating in any cycle are absent from the map. Direct
    /// self-loops are included so the existing direct-self-reference path can stay
    /// untouched (it still handles its case independently).
    /// </summary>
    private static Dictionary<string, HashSet<string>> BuildCycleMemberMap(
        OpenApiDocument openApiDoc)
    {
        var graph = BuildSchemaDependencyGraph(openApiDoc);
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        // For each node, compute forward reachability (BFS via outgoing edges) and the
        // SCC = intersection with reverse reachability. Cost is O(V·(V+E)), fine for
        // the schema counts a single spec carries.
        var reverse = ReverseGraph(graph);

        foreach (var node in graph.Keys)
        {
            var forward = ReachableFrom(graph, node);
            var backward = ReachableFrom(reverse, node);

            // SCC = forward ∩ backward, excluding the node itself unless self-loop.
            var scc = new HashSet<string>(StringComparer.Ordinal);
            foreach (var peer in forward)
            {
                if (backward.Contains(peer))
                {
                    scc.Add(peer);
                }
            }

            // A node is part of a cycle if its SCC has >1 members OR it has a direct
            // self-edge in the graph. Without the self-edge check, a singleton SCC
            // (the empty cycle case) would falsely register.
            var hasSelfLoop = graph[node].Contains(node);
            if (scc.Count > 1 || (scc.Count == 1 && hasSelfLoop))
            {
                result[node] = scc;
            }
        }

        return result;
    }

    private static Dictionary<string, HashSet<string>> BuildSchemaDependencyGraph(
        OpenApiDocument openApiDoc)
    {
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        if (openApiDoc.Components?.Schemas is null)
        {
            return graph;
        }

        foreach (var (name, schemaValue) in openApiDoc.Components.Schemas)
        {
            var edges = new HashSet<string>(StringComparer.Ordinal);
            if (schemaValue is OpenApiSchema actualSchema)
            {
                CollectDirectRefs(actualSchema, edges);
            }

            graph[name] = edges;
        }

        return graph;
    }

    /// <summary>
    /// Walks the schema's direct references (without following them) and records the
    /// names. Mirrors <see cref="TypeScriptSchemaImports.Collect"/> but writes into an arbitrary
    /// set so it can be reused for graph building.
    /// </summary>
    private static void CollectDirectRefs(
        OpenApiSchema schema,
        HashSet<string> edges)
    {
        if (schema.Properties is not null)
        {
            foreach (var (_, propValue) in schema.Properties)
            {
                CollectRefsFromSchema(propValue, edges);
            }
        }

        // An array alias (NodeList: array of Node) or a dictionary closes a cycle through its own items
        if (schema.Items is not null)
        {
            CollectRefsFromSchema(schema.Items, edges);
        }

        if (schema.AdditionalProperties is not null)
        {
            CollectRefsFromSchema(schema.AdditionalProperties, edges);
        }

        if (schema.AllOf is not null)
        {
            foreach (var sub in schema.AllOf)
            {
                CollectRefsFromSchema(sub, edges);
            }
        }

        if (schema.OneOf is not null)
        {
            foreach (var sub in schema.OneOf)
            {
                CollectRefsFromSchema(sub, edges);
            }
        }

        if (schema.AnyOf is not null)
        {
            foreach (var sub in schema.AnyOf)
            {
                CollectRefsFromSchema(sub, edges);
            }
        }
    }

    private static void CollectRefsFromSchema(
        IOpenApiSchema schema,
        HashSet<string> edges)
    {
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refName = schemaRef.Reference.Id ?? schemaRef.Id;
            if (refName is not null)
            {
                edges.Add(refName);
            }

            return;
        }

        if (schema is not OpenApiSchema actual)
        {
            return;
        }

        if (actual.Items is not null)
        {
            CollectRefsFromSchema(actual.Items, edges);
        }

        if (actual.AdditionalProperties is not null)
        {
            CollectRefsFromSchema(actual.AdditionalProperties, edges);
        }

        if (actual.AllOf is not null)
        {
            foreach (var sub in actual.AllOf)
            {
                CollectRefsFromSchema(sub, edges);
            }
        }

        if (actual.OneOf is not null)
        {
            foreach (var sub in actual.OneOf)
            {
                CollectRefsFromSchema(sub, edges);
            }
        }

        if (actual.AnyOf is not null)
        {
            foreach (var sub in actual.AnyOf)
            {
                CollectRefsFromSchema(sub, edges);
            }
        }
    }

    private static Dictionary<string, HashSet<string>> ReverseGraph(
        Dictionary<string, HashSet<string>> graph)
    {
        var reverse = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var node in graph.Keys)
        {
            reverse[node] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var (node, edges) in graph)
        {
            foreach (var target in edges)
            {
                if (!reverse.TryGetValue(target, out var bucket))
                {
                    bucket = new HashSet<string>(StringComparer.Ordinal);
                    reverse[target] = bucket;
                }

                bucket.Add(node);
            }
        }

        return reverse;
    }

    private static HashSet<string> ReachableFrom(
        Dictionary<string, HashSet<string>> graph,
        string start)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!graph.TryGetValue(current, out var edges))
            {
                continue;
            }

            foreach (var next in edges)
            {
                if (visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return visited;
    }

    // The property keys a variant declares - its own and its inline allOf members' - as the model extractor names them
    private static IEnumerable<string> VariantPropertyKeys(
        OpenApiDocument openApiDoc,
        string variantName)
    {
        if (openApiDoc.Components?.Schemas is null ||
            !openApiDoc.Components.Schemas.TryGetValue(variantName, out var variant) ||
            variant is not OpenApiSchema variantSchema)
        {
            return [];
        }

        var inlineMembers = variantSchema.AllOf?.OfType<OpenApiSchema>() ?? [];
        return (variantSchema.Properties?.Keys ?? Enumerable.Empty<string>())
            .Concat(inlineMembers.SelectMany(m => m.Properties?.Keys ?? Enumerable.Empty<string>()));
    }

    /// <summary>
    /// Whether a variant's Zod schema is a <c>z.object</c> whose <paramref name="discriminator"/> is a literal
    /// (<see cref="DiscriminatorLiteralChain"/>) - what <c>z.discriminatedUnion</c> requires of each option. A
    /// variant in a reference cycle is typed <c>z.ZodType</c>, which is not an object schema, and a variant that
    /// inherits the property from an <c>allOf</c> base does not declare the literal itself.
    /// </summary>
    private static bool IsDiscriminatedObject(
        OpenApiDocument openApiDoc,
        string variantName,
        string discriminator,
        Dictionary<string, HashSet<string>> cycleMembers)
    {
        if (cycleMembers.ContainsKey(variantName) ||
            openApiDoc.Components?.Schemas is null ||
            !openApiDoc.Components.Schemas.TryGetValue(variantName, out var variant) ||
            variant is not OpenApiSchema variantSchema ||
            !TypeScriptModelExtractor.IsObjectSchema(variantSchema))
        {
            return false;
        }

        var inlineMembers = variantSchema.AllOf?.OfType<OpenApiSchema>().ToList() ?? [];
        var ownProperties = (variantSchema.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
            .Concat(inlineMembers.SelectMany(m => m.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)));

        // An optional discriminator would make `undefined` a value every variant shares, which
        // z.discriminatedUnion rejects as a duplicate.
        var required = (variantSchema.Required ?? new HashSet<string>(StringComparer.Ordinal))
            .Concat(inlineMembers.SelectMany(m => m.Required ?? new HashSet<string>(StringComparer.Ordinal)));

        return required.Contains(discriminator, StringComparer.Ordinal) &&
               ownProperties.Any(p =>
                   string.Equals(p.Key, discriminator, StringComparison.Ordinal) &&
                   p.Value is OpenApiSchema { Enum: null or { Count: 0 } } plain &&
                   plain.Type?.HasFlag(JsonSchemaType.String) == true);
    }

    /// <summary>
    /// Whether a union member is emitted as a <c>z.object(...)</c> (an object schema, composed or not, outside every
    /// cycle): the one kind of member that has <c>passthrough()</c>.
    /// </summary>
    private static bool IsPlainObject(
        OpenApiDocument openApiDoc,
        string schemaName,
        Dictionary<string, HashSet<string>> cycleMembers)
    {
        if (cycleMembers.ContainsKey(schemaName) ||
            openApiDoc.Components?.Schemas is null ||
            !openApiDoc.Components.Schemas.TryGetValue(schemaName, out var member) ||
            member is not OpenApiSchema schema)
        {
            return false;
        }

        return schema.Enum is not { Count: > 0 } &&
               schema.OneOf is not { Count: > 0 } &&
               schema.AnyOf is not { Count: > 0 } &&
               !schema.IsOfType(JsonSchemaType.Array) &&
               !TypeScriptModelExtractor.IsScalarAliasSchema(schema) &&
               !TypeScriptModelExtractor.IsRecordAliasSchema(schema) &&
               (schema.Properties is { Count: > 0 } || schema.AllOf is { Count: > 0 } || schema.IsOfType(JsonSchemaType.Object));
    }

    private static string? GenerateZodObject(
        string schemaName,
        OpenApiSchema schema,
        string? headerContent,
        HashSet<string>? enumNames,
        TypeScriptNamingStrategy namingStrategy,
        HashSet<string>? cyclePeers,
        TypeScriptDiscriminatorLiterals discriminators,
        bool convertDates = false)
    {
        // allOf composition, as in TypeScriptModelExtractor: every $ref member is extended, the inline
        // members contribute their properties (and required names) as if declared on the schema.
        var properties = new List<KeyValuePair<string, IOpenApiSchema>>(schema.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal));
        var required = new HashSet<string>(schema.Required ?? new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var extendsTypeNames = new List<string>();
        if (schema.AllOf is { Count: > 0 })
        {
            foreach (var subSchema in schema.AllOf)
            {
                if (subSchema is OpenApiSchemaReference allOfRef)
                {
                    if ((allOfRef.Reference.Id ?? allOfRef.Id) is { } refName &&
                        !extendsTypeNames.Contains(refName, StringComparer.Ordinal))
                    {
                        extendsTypeNames.Add(refName);
                    }
                }
                else if (subSchema is OpenApiSchema inlineMember)
                {
                    if (inlineMember.Properties is not null)
                    {
                        properties.AddRange(inlineMember.Properties.Where(p => properties.TrueForAll(o => !string.Equals(o.Key, p.Key, StringComparison.Ordinal))));
                    }

                    if (inlineMember.Required is not null)
                    {
                        required.UnionWith(inlineMember.Required);
                    }
                }
            }
        }

        var importTypes = new HashSet<string>(extendsTypeNames, StringComparer.Ordinal);

        // Collect referenced types from properties, and from the value schema of a catchall
        foreach (var prop in properties)
        {
            TypeScriptSchemaImports.Collect(prop.Value, importTypes);
        }

        if (schema.AdditionalProperties is not null && properties.Count > 0)
        {
            TypeScriptSchemaImports.Collect(schema.AdditionalProperties, importTypes);
        }

        // Detect direct self-reference. A property whose $ref / array.items.$ref /
        // oneOf|anyOf|allOf member resolves to schemaName itself requires `z.lazy(...)`
        // around the recursive sub-expression AND an explicit `z.ZodType<Name>` type
        // annotation on the exported const, or TS 7022/2448 trip on strict tsc.
        var hasSelfReference = properties.Any(p => SchemaReferencesType(p.Value, schemaName));

        // Cyclic membership: mutual recursion (Tag <-> Category) trips TS 7022 / TS 2448
        // at MODULE-LOAD time when one schema's initializer references the other's
        // CategorySchema binding before it has resolved. cyclePeers contains the full
        // strongly-connected component; any property referencing a sibling needs the
        // same z.lazy + type-annotation treatment as a direct self-reference.
        var isCyclic = hasSelfReference || (cyclePeers is not null && cyclePeers.Count > 0);

        var sb = new StringBuilder();

        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        sb.AppendLine("import { z } from 'zod';");

        // Self-referencing or cyclic schemas need a `type` import (not a value) from the
        // sibling model file so `z.ZodType<Name>` resolves without circular evaluation.
        if (isCyclic)
        {
            sb.Append("import type { ").Append(schemaName).Append(" } from './").Append(schemaName).AppendLine("';");
        }

        // Build import statements for referenced schemas
        var imports = BuildZodImportStatements(importTypes, schemaName, enumNames);
        if (imports.Count > 0)
        {
            sb.AppendLine();
            foreach (var import in imports)
            {
                sb.AppendLine(import);
            }
        }

        sb.AppendLine();

        var typeAnnotation = isCyclic ? ": z.ZodType<" + schemaName + ">" : string.Empty;

        if (extendsTypeNames.Count > 0)
        {
            // BaseSchema.extend({...}); further bases join through their shape.
            sb.Append("export const ").Append(schemaName).Append("Schema").Append(typeAnnotation).Append(" = ").Append(extendsTypeNames[0]).Append("Schema");
            foreach (var additionalBase in extendsTypeNames.Skip(1))
            {
                sb.Append(".extend(").Append(additionalBase).Append("Schema.shape)");
            }

            sb.AppendLine(".extend({");
        }
        else
        {
            sb.Append("export const ").Append(schemaName).Append("Schema").Append(typeAnnotation).AppendLine(" = z.object({");
        }

        for (var i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            var propName = TypeScriptPropertyNames.Resolve(prop.Key, properties.Select(p => p.Key), namingStrategy);
            var isRequired = required.Contains(prop.Key, StringComparer.Ordinal);

            // Parent name flows into the property mapper so recursive sub-expressions
            // get wrapped in z.lazy. Non-self-referencing properties skip the wrap.
            // cyclePeers extends the wrap to mutual-recursion siblings (Tag <-> Category).
            var zodChain = DiscriminatorLiteralChain(schemaName, prop, isRequired, discriminators)
                           ?? MapPropertyToZod(prop.Value, isRequired, enumNames, schemaName, cyclePeers, convertDates);

            sb.Append("  ").Append(TypeScriptPropertyNames.ToMemberSyntax(propName)).Append(": ").Append(zodChain).Append(',');
            sb.AppendLine();
        }

        // additionalProperties beside declared properties: z.object strips the keys it does not know, so the
        // extra keys are kept (passthrough) or validated against the value schema (catchall).
        if (schema.AdditionalProperties is not null && properties.Count > 0)
        {
            sb.Append("})");
            if (schema.IsAdditionalPropertiesAnyType())
            {
                sb.AppendLine(".passthrough();");
            }
            else
            {
                sb.Append(".catchall(").Append(MapPropertyToZod(schema.AdditionalProperties, isRequired: true, enumNames, schemaName, cyclePeers, convertDates)).AppendLine(");");
            }
        }
        else
        {
            sb.AppendLine("});");
        }

        return sb.ToString();
    }

    /// <summary>
    /// The Zod chain of a discriminator property on its variant - a literal of the value(s) the union names it
    /// by - or <see langword="null"/> when the property is not one, or is not a plain string (an enum reference
    /// keeps its enum schema).
    /// </summary>
    private static string? DiscriminatorLiteralChain(
        string schemaName,
        KeyValuePair<string, IOpenApiSchema> prop,
        bool isRequired,
        TypeScriptDiscriminatorLiterals discriminators)
    {
        if (prop.Value is not OpenApiSchema { Enum: null or { Count: 0 } } plain ||
            plain.Type?.HasFlag(JsonSchemaType.String) != true ||
            discriminators.ZodOf(schemaName, prop.Key) is not { } literal)
        {
            return null;
        }

        var sb = new StringBuilder(literal);
        AppendModifiers(sb, plain.IsNullable(), isRequired, plain.Default);
        return sb.ToString();
    }

    /// <summary>
    /// Walks <paramref name="schema"/> (including <c>$ref</c>, array items, additional
    /// properties, and <c>allOf</c>/<c>oneOf</c>/<c>anyOf</c> members) and returns
    /// <c>true</c> if any of them resolves to <paramref name="targetName"/>. Used to
    /// detect recursive schema references so the Zod emitter can apply <c>z.lazy(...)</c>
    /// where needed.
    /// </summary>
    private static bool SchemaReferencesType(
        IOpenApiSchema schema,
        string targetName)
    {
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refName = schemaRef.Reference.Id ?? schemaRef.Id;
            return string.Equals(refName, targetName, StringComparison.Ordinal);
        }

        if (schema is not OpenApiSchema actualSchema)
        {
            return false;
        }

        if (actualSchema.Items is not null && SchemaReferencesType(actualSchema.Items, targetName))
        {
            return true;
        }

        if (actualSchema.AdditionalProperties is not null &&
            SchemaReferencesType(actualSchema.AdditionalProperties, targetName))
        {
            return true;
        }

        if (actualSchema.AllOf is { Count: > 0 })
        {
            foreach (var sub in actualSchema.AllOf)
            {
                if (SchemaReferencesType(sub, targetName))
                {
                    return true;
                }
            }
        }

        if (actualSchema.OneOf is { Count: > 0 })
        {
            foreach (var sub in actualSchema.OneOf)
            {
                if (SchemaReferencesType(sub, targetName))
                {
                    return true;
                }
            }
        }

        if (actualSchema.AnyOf is { Count: > 0 })
        {
            foreach (var sub in actualSchema.AnyOf)
            {
                if (SchemaReferencesType(sub, targetName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The Zod schema of a component that only names a primitive or a numeric enum.
    /// </summary>
    private static string GenerateZodScalarAlias(
        string schemaName,
        OpenApiSchema schema,
        string? headerContent,
        bool convertDates,
        bool extensibleEnums)
    {
        var sb = new StringBuilder();

        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        sb.AppendLine("import { z } from 'zod';");
        sb.AppendLine();
        sb.Append("export const ").Append(schemaName).Append("Schema = ");

        // A numeric enum is open like a string enum when enums are extensible: a value the server adds later is a number
        var literals = TypeScriptModelExtractor.GetNumericEnumLiterals(schema);
        if (literals.Count == 1 && !extensibleEnums)
        {
            sb.Append("z.literal(").Append(literals[0]).Append(')');
        }
        else if (literals.Count > 0)
        {
            var options = literals.Select(l => $"z.literal({l})").ToList();
            if (extensibleEnums)
            {
                options.Add("z.number()");
            }

            sb.Append("z.union([").Append(string.Join(", ", options)).Append("])");
        }
        else
        {
            sb.Append(MapPrimitiveToZod(schema, convertDates));
        }

        sb.AppendLine(";");

        return sb.ToString();
    }

    /// <summary>
    /// The Zod schema of a dictionary component (<c>additionalProperties</c>, no properties of its own):
    /// <c>z.record(z.string(), V)</c>. As <c>z.object({})</c> it stripped every key.
    /// </summary>
    private static string GenerateZodRecordAlias(
        string schemaName,
        OpenApiSchema schema,
        string? headerContent,
        bool convertDates)
    {
        var sb = new StringBuilder();

        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        sb.AppendLine("import { z } from 'zod';");

        var importTypes = new HashSet<string>(StringComparer.Ordinal);
        TypeScriptSchemaImports.Collect(schema.AdditionalProperties!, importTypes);
        var imports = BuildZodImportStatements(importTypes, schemaName, enumNames: null);
        if (imports.Count > 0)
        {
            sb.AppendLine();
            foreach (var import in imports)
            {
                sb.AppendLine(import);
            }
        }

        sb.AppendLine();
        sb.Append("export const ").Append(schemaName).Append("Schema = ");
        sb.Append(MapPropertyToZod(schema, isRequired: true, enumNames: null, parentSchemaName: null, cyclePeers: null, convertDates));
        sb.AppendLine(";");

        return sb.ToString();
    }

    private static string GenerateZodArray(
        string schemaName,
        OpenApiSchema arraySchema,
        string? headerContent,
        bool convertDates = false,
        bool isCyclic = false)
    {
        var sb = new StringBuilder();

        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        sb.AppendLine("import { z } from 'zod';");

        // In a cycle the const is typed and lazy, like a cyclic object schema
        if (isCyclic)
        {
            sb.Append("import type { ").Append(schemaName).Append(" } from './").Append(schemaName).AppendLine("';");
        }

        // Add import for the item type if it references another schema
        string? itemSchemaName = null;
        if (arraySchema.Items is OpenApiSchemaReference itemRef)
        {
            itemSchemaName = itemRef.Reference.Id ?? itemRef.Id;
            if (itemSchemaName is not null)
            {
                sb.AppendLine();
                sb.Append("import { ").Append(itemSchemaName).Append("Schema } from './").Append(itemSchemaName).AppendLine(".zod';");
            }
        }

        sb.AppendLine();

        sb.Append("export const ").Append(schemaName).Append("Schema");
        if (isCyclic)
        {
            sb.Append(": z.ZodType<").Append(schemaName).Append("> = z.lazy(() => ");
        }
        else
        {
            sb.Append(" = ");
        }

        sb.Append("z.array(");

        if (itemSchemaName is not null)
        {
            sb.Append(itemSchemaName).Append("Schema");
        }
        else if (arraySchema.Items is OpenApiSchema itemSchema)
        {
            sb.Append(MapPrimitiveToZod(itemSchema, convertDates));
        }
        else
        {
            sb.Append("z.unknown()");
        }

        sb.Append(')');

        // Add minItems/maxItems constraints
        if (arraySchema.MinItems > 0)
        {
            sb.Append(".min(").Append(arraySchema.MinItems.Value.ToString(CultureInfo.InvariantCulture)).Append(')');
        }

        if (arraySchema.MaxItems > 0)
        {
            sb.Append(".max(").Append(arraySchema.MaxItems.Value.ToString(CultureInfo.InvariantCulture)).Append(')');
        }

        if (isCyclic)
        {
            sb.Append(')');
        }

        sb.AppendLine(";");

        return sb.ToString();
    }

    /// <summary>
    /// Maps an OpenAPI property schema to a Zod chain string. When
    /// <paramref name="parentSchemaName"/> is supplied and any part of <paramref name="schema"/>
    /// references it (direct <c>$ref</c>, array items, etc.), the chain is wrapped in
    /// <c>z.lazy(() =&gt; ...)</c> to defer the recursive reference; this is required for
    /// strict TypeScript compilation of recursive schemas (TS 7022 / TS 2448).
    /// </summary>
    private static string MapPropertyToZod(
        IOpenApiSchema schema,
        bool isRequired,
        HashSet<string>? enumNames,
        string? parentSchemaName = null,
        HashSet<string>? cyclePeers = null,
        bool convertDates = false)
    {
        var inner = BuildPropertyZodChain(schema, isRequired, enumNames, parentSchemaName, cyclePeers, convertDates);

        // Wrap recursive properties in z.lazy at the top level. Modifiers (.optional() /
        // .nullable() / .default()) stay inside the lazy so the lambda produces the fully
        // resolved schema for the caller. Both direct self-references and mutual-cycle
        // siblings (via cyclePeers) get the wrap.
        if (PropertyTouchesCycle(schema, parentSchemaName, cyclePeers))
        {
            return "z.lazy(() => " + inner + ")";
        }

        return inner;
    }

    /// <summary>
    /// Returns <c>true</c> when the property's schema references the parent (direct
    /// self-loop) OR any sibling in the parent's strongly-connected component (mutual
    /// recursion). The two cases share the same fix — wrap with <c>z.lazy</c>.
    /// </summary>
    private static bool PropertyTouchesCycle(
        IOpenApiSchema schema,
        string? parentSchemaName,
        HashSet<string>? cyclePeers)
    {
        if (parentSchemaName is not null && SchemaReferencesType(schema, parentSchemaName))
        {
            return true;
        }

        if (cyclePeers is null || cyclePeers.Count == 0)
        {
            return false;
        }

        foreach (var peer in cyclePeers)
        {
            if (string.Equals(peer, parentSchemaName, StringComparison.Ordinal))
            {
                continue;
            }

            if (SchemaReferencesType(schema, peer))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildPropertyZodChain(
        IOpenApiSchema schema,
        bool isRequired,
        HashSet<string>? enumNames,
        string? parentSchemaName,
        HashSet<string>? cyclePeers,
        bool convertDates = false)
    {
        var sb = new StringBuilder();

        // Handle schema references ($ref): a nullable component makes the property nullable
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refName = schemaRef.Reference.Id ?? schemaRef.Id ?? "unknown";
            sb.Append(refName).Append("Schema");

            // OpenAPI 3.1: a default next to the $ref is this property's own (the component's is the component's)
            var siblingDefault = schemaRef.Default is { } own && !ReferenceEquals(own, (schemaRef.Target as OpenApiSchema)?.Default)
                ? own
                : null;
            AppendModifiers(sb, schemaRef.Target?.IsNullable() == true, isRequired, siblingDefault);
            return sb.ToString();
        }

        if (schema is not OpenApiSchema actualSchema)
        {
            sb.Append("z.unknown()");
            AppendModifiers(sb, isNullable: false, isRequired, defaultValue: null);
            return sb.ToString();
        }

        var isNullable = actualSchema.IsNullable();

        // Handle allOf compositions (commonly used for nullable $ref in OpenAPI 3.0)
        if (actualSchema.AllOf is { Count: > 0 })
        {
            foreach (var subSchema in actualSchema.AllOf)
            {
                if (subSchema is OpenApiSchemaReference allOfRef)
                {
                    var refName = allOfRef.Reference.Id ?? allOfRef.Id ?? "unknown";
                    sb.Append(refName).Append("Schema");
                    break;
                }
            }

            if (sb.Length == 0)
            {
                sb.Append("z.unknown()");
            }

            AppendModifiers(sb, isNullable, isRequired, actualSchema.Default);
            return sb.ToString();
        }

        // Handle oneOf and anyOf (a union of their members, as the model types them): a branch that is only null
        // (3.1's `oneOf: [$ref, {type: 'null'}]`) is nullability, not a member
        if ((actualSchema.OneOf is { Count: > 0 } ? actualSchema.OneOf : actualSchema.AnyOf) is { Count: > 0 } union)
        {
            var members = union.Where(m => !IsNullBranch(m)).ToList();
            isNullable = isNullable || members.Count < union.Count;
            if (members.Count == 1 && members[0] is OpenApiSchemaReference oneOfRef)
            {
                var refName = oneOfRef.Reference.Id ?? oneOfRef.Id ?? "unknown";
                sb.Append(refName).Append("Schema");
            }
            else
            {
                sb.Append("z.union([");
                var first = true;
                foreach (var subSchema in members)
                {
                    if (!first)
                    {
                        sb.Append(", ");
                    }

                    first = false;

                    if (subSchema is OpenApiSchemaReference unionRef)
                    {
                        var refName = unionRef.Reference.Id ?? unionRef.Id ?? "unknown";
                        sb.Append(refName).Append("Schema");
                    }
                    else
                    {
                        sb.Append("z.unknown()");
                    }
                }

                sb.Append("])");
            }

            AppendModifiers(sb, isNullable, isRequired, actualSchema.Default);
            return sb.ToString();
        }

        // Handle additionalProperties (Record types)
        if (actualSchema.AdditionalProperties is not null)
        {
            sb.Append("z.record(z.string(), ");
            var valueZod = MapPropertyToZod(actualSchema.AdditionalProperties, isRequired: true, enumNames, parentSchemaName, cyclePeers, convertDates);
            sb.Append(valueZod).Append(')');
            AppendModifiers(sb, isNullable, isRequired, actualSchema.Default);
            return sb.ToString();
        }

        // Handle array types
        if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
        {
            sb.Append("z.array(");

            if (actualSchema.Items is OpenApiSchemaReference itemRef)
            {
                var refName = itemRef.Reference.Id ?? itemRef.Id ?? "unknown";
                sb.Append(refName).Append("Schema");
            }
            else if (actualSchema.Items is OpenApiSchema itemSchema)
            {
                sb.Append(MapPrimitiveToZod(itemSchema, convertDates));
            }
            else
            {
                sb.Append("z.unknown()");
            }

            sb.Append(')');

            // Array constraints
            if (actualSchema.MinItems > 0)
            {
                sb.Append(".min(").Append(actualSchema.MinItems.Value.ToString(CultureInfo.InvariantCulture)).Append(')');
            }

            if (actualSchema.MaxItems > 0)
            {
                sb.Append(".max(").Append(actualSchema.MaxItems.Value.ToString(CultureInfo.InvariantCulture)).Append(')');
            }

            AppendModifiers(sb, isNullable, isRequired, actualSchema.Default);
            return sb.ToString();
        }

        // Handle primitive types
        sb.Append(MapPrimitiveToZod(actualSchema, convertDates));
        AppendModifiers(sb, isNullable, isRequired, actualSchema.Default);
        return sb.ToString();
    }

    /// <summary>
    /// Maps an OpenAPI primitive schema to a Zod base chain (without nullable/optional modifiers).
    /// </summary>
    private static string MapPrimitiveToZod(
        OpenApiSchema schema,
        bool convertDates = false)
    {
        var sb = new StringBuilder();
        var typeValue = schema.Type ?? JsonSchemaType.Object;

        // Strip the Null flag
        if (typeValue.HasFlag(JsonSchemaType.Null))
        {
            typeValue &= ~JsonSchemaType.Null;
        }

        if (typeValue.HasFlag(JsonSchemaType.String))
        {
            // Check for binary format
            if (string.Equals(schema.Format, "binary", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("z.instanceof(Blob)");
                return sb.ToString();
            }

            // A date-time with convertDates: the response reviver has made it a Date before the check
            var format = schema.Format?.ToLowerInvariant();
            if (convertDates && format == "date-time")
            {
                sb.Append("z.coerce.date()");
                return sb.ToString();
            }

            sb.Append("z.string()");

            // Format-specific refinements
            switch (format)
            {
                case "email":
                    sb.Append(".email()");
                    break;
                case "uri":
                    sb.Append(".url()");
                    break;
                case "uuid":
                case "guid":
                    sb.Append(".uuid()");
                    break;
                case "date":
                    sb.Append(".date()");
                    break;
                case "date-time":
                    // An offset, Z, or neither: a DateTime of Kind Unspecified is written without one
                    sb.Append(".datetime({ offset: true, local: true })");
                    break;
            }

            // String length constraints
            if (schema.MinLength > 0)
            {
                sb.Append(".min(").Append(schema.MinLength.Value.ToString(CultureInfo.InvariantCulture)).Append(')');
            }

            if (schema.MaxLength > 0)
            {
                sb.Append(".max(").Append(schema.MaxLength.Value.ToString(CultureInfo.InvariantCulture)).Append(')');
            }

            // Pattern constraint
            if (!string.IsNullOrWhiteSpace(schema.Pattern))
            {
                sb.Append(".regex(").Append(ToRegexLiteral(schema.Pattern!)).Append(')');
            }

            return sb.ToString();
        }

        if (typeValue.HasFlag(JsonSchemaType.Integer))
        {
            sb.Append("z.number().int()");
            AppendNumericConstraints(sb, schema);
            return sb.ToString();
        }

        if (typeValue.HasFlag(JsonSchemaType.Number))
        {
            sb.Append("z.number()");
            AppendNumericConstraints(sb, schema);
            return sb.ToString();
        }

        if (typeValue.HasFlag(JsonSchemaType.Boolean))
        {
            sb.Append("z.boolean()");
            return sb.ToString();
        }

        sb.Append("z.unknown()");
        return sb.ToString();
    }

    private static void AppendNumericConstraints(
        StringBuilder sb,
        OpenApiSchema schema)
    {
        if (!string.IsNullOrEmpty(schema.Minimum))
        {
            sb.Append(".min(").Append(schema.Minimum).Append(')');
        }

        if (!string.IsNullOrEmpty(schema.Maximum))
        {
            sb.Append(".max(").Append(schema.Maximum).Append(')');
        }
    }

    /// <summary>
    /// A <c>oneOf</c> / <c>anyOf</c> branch whose only contribution is nullability: <c>{type: 'null'}</c>, or the 3.0
    /// spelling, an empty object that is nullable.
    /// </summary>
    private static bool IsNullBranch(IOpenApiSchema branch)
        => branch is OpenApiSchema { Properties: null or { Count: 0 }, AllOf: null or { Count: 0 }, OneOf: null or { Count: 0 }, AnyOf: null or { Count: 0 }, Items: null, Enum: null or { Count: 0 } } plain &&
           plain.Type is { } type &&
           ((type & ~JsonSchemaType.Null) == JsonSchemaType.Object || (type & ~JsonSchemaType.Null) == default) &&
           type.HasFlag(JsonSchemaType.Null);

    private static void AppendModifiers(
        StringBuilder sb,
        bool isNullable,
        bool isRequired,
        JsonNode? defaultValue)
    {
        // An optional property is written as null when it has no value (.NET writes nulls by default), so it is
        // null or absent; a required nullable one is null or a value
        if (!isRequired)
        {
            sb.Append(".nullish()");
        }
        else if (isNullable)
        {
            sb.Append(".nullable()");
        }

        if (defaultValue is not null)
        {
            var defaultStr = FormatDefaultValue(defaultValue);
            if (defaultStr is not null)
            {
                sb.Append(".default(").Append(defaultStr).Append(')');
            }
        }
    }

    // The TypeScript literal of a default: by the kind of the JSON value, whatever .NET type the reader keeps it in
    // (a number is not always a long), and a string escaped so that it stays one
    private static string? FormatDefaultValue(JsonNode defaultValue)
        => defaultValue is not JsonValue jsonValue
            ? null
            : jsonValue.GetValueKind() switch
            {
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => jsonValue.ToJsonString(),
                JsonValueKind.String => TypeScriptLiteralHelper.ToStringLiteral(jsonValue.GetValue<string>()),
                _ => null,
            };

    /// <summary>
    /// Builds import statements for referenced Zod schemas.
    /// </summary>
    private static List<string> BuildZodImportStatements(
        HashSet<string> importTypes,
        string currentTypeName,
        HashSet<string>? enumNames)
    {
        var imports = new List<string>();

        foreach (var typeName in importTypes.OrderBy(t => t, StringComparer.Ordinal))
        {
            // Don't import self
            if (typeName.Equals(currentTypeName, StringComparison.Ordinal))
            {
                continue;
            }

            // Use correct relative path based on whether the type is an enum or model
            var importPath = enumNames is not null && enumNames.Contains(typeName)
                ? $"../enums/{typeName}.zod"
                : $"./{typeName}.zod";

            imports.Add($"import {{ {typeName}Schema }} from '{importPath}';");
        }

        return imports;
    }
}