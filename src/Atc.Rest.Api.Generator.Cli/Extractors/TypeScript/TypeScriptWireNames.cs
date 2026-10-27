namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// The conversion between the API's values (the wire) and the TypeScript models: the property names (the naming
/// strategy) and, with <c>--convert-dates</c>, the values declared <c>format: date</c> / <c>date-time</c> - which
/// component models have a property to rename or a date to convert - their own, an inherited one (<c>allOf</c>) or
/// one in a model they hold - and the shape of any value, so the generated client converts both ways
/// (<c>helpers/wireNames.ts</c>). Only what the specification declares is converted: a plain string that looks like
/// a date stays a string.
/// </summary>
/// <remarks>
/// Only declared properties are renamed: dictionary keys (<c>additionalProperties</c>), keys of inline objects
/// (typed <c>unknown</c>) and anything the specification does not describe pass through as they are. With the
/// <c>Original</c> strategy, or a specification whose property names already follow the strategy (every camelCase
/// API with the default strategy), nothing is renamed and nothing is generated.
/// </remarks>
public sealed class TypeScriptWireNames
{
    private readonly OpenApiDocument document;
    private readonly Dictionary<string, List<WireProperty>> models = new(StringComparer.Ordinal);
    private readonly HashSet<string> mapped = new(StringComparer.Ordinal);
    private readonly bool convertDates;
    private readonly bool hasTopLevelDate;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeScriptWireNames"/> class, collecting the properties every
    /// component object model renames under <paramref name="namingStrategy"/>.
    /// </summary>
    /// <param name="document">The parsed OpenAPI document.</param>
    /// <param name="namingStrategy">How the models case their property names; <c>Original</c> renames nothing.</param>
    /// <param name="convertDates">When true, values declared <c>format: date</c> / <c>date-time</c> are <c>Date</c> in the models.</param>
    public TypeScriptWireNames(
        OpenApiDocument document,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        this.document = document;
        this.convertDates = convertDates;

        if ((namingStrategy == TypeScriptNamingStrategy.Original && !convertDates) ||
            document.Components?.Schemas is not { } schemas)
        {
            return;
        }

        foreach (var (name, schema) in schemas)
        {
            if (IsObjectModel(schema))
            {
                models[name] = CollectProperties(schema, namingStrategy, new HashSet<string>(StringComparer.Ordinal) { name });
            }
        }

        // Renamed directly, then - until nothing changes - holding a model that is renamed.
        foreach (var (name, properties) in models)
        {
            if (properties.Exists(p => !string.Equals(p.Wire, p.Ts, StringComparison.Ordinal)))
            {
                mapped.Add(name);
            }
        }

        bool changed;
        do
        {
            changed = false;
            foreach (var (name, properties) in models)
            {
                if (!mapped.Contains(name) && properties.Exists(p => ShapeOf(p.Schema) is not null))
                {
                    mapped.Add(name);
                    changed = true;
                }
            }
        }
        while (changed);

        // A bare date as a body or response (no model around it) needs the conversion helper as well
        hasTopLevelDate = convertDates &&
            (document.Paths?.Values ?? Enumerable.Empty<IOpenApiPathItem>())
                .SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
                .Any(operation =>
                    ShapeOf(operation.GetResponseSchema("200")) is not null ||
                    (operation.GetRequestBodySchemaWithContentType() is ({ } body, _) && ShapeOf(body) is not null));
    }

    /// <summary>
    /// Gets a value indicating whether any key has to be renamed, or any date converted, between the wire and the
    /// TypeScript models.
    /// </summary>
    public bool HasRenames
        => mapped.Count > 0 || hasTopLevelDate;

    /// <summary>
    /// The <c>wire</c> request option of an operation - how to rename its request body, its response (or stream
    /// item) and its declared error bodies - as a TypeScript object literal, or <see langword="null"/> when none of
    /// them has a key to rename.
    /// </summary>
    public string? OperationOption(
        OpenApiOperation operation,
        bool isStreaming,
        bool isFileUpload,
        bool isDownload)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!HasRenames)
        {
            return null;
        }

        var parts = new List<string>();

        if (!isFileUpload &&
            operation.GetRequestBodySchemaWithContentType() is ({ } bodySchema, _) &&
            ShapeOf(bodySchema) is { } bodyShape)
        {
            parts.Add("body: " + bodyShape);
        }

        var responseSchema = isStreaming
            ? operation.GetStreamingItemSchema() ?? ItemsOf(operation.GetResponseSchema("200"))
            : isDownload
                ? null
                : operation.GetResponseSchema("200") ?? operation.GetResponseSchema("201") ?? operation.GetResponseSchema("202");
        if (ShapeOf(responseSchema) is { } responseShape)
        {
            parts.Add("response: " + responseShape);
        }

        var errors = new List<string>();
        foreach (var (status, response) in operation.Responses ?? new OpenApiResponses())
        {
            var isDefault = string.Equals(status, "default", StringComparison.OrdinalIgnoreCase);
            if (!isDefault && !ErrorResponseShapeResolver.IsErrorStatusCode(status))
            {
                continue;
            }

            var shape = ErrorResponseShapeResolver.Resolve(response);
            if (shape.Schema is not null &&
                shape.Kind is ErrorResponseKind.JsonObject or ErrorResponseKind.Problem or ErrorResponseKind.ValidationProblem &&
                ShapeOf(shape.Schema) is { } errorShape)
            {
                errors.Add((isDefault ? "default" : $"'{status}'") + ": " + errorShape);
            }
        }

        if (errors.Count > 0)
        {
            parts.Add("errors: { " + string.Join(", ", errors) + " }");
        }

        return parts.Count == 0 ? null : "{ " + string.Join(", ", parts) + " }";
    }

    /// <summary>
    /// The shape of a value of <paramref name="schema"/> as a TypeScript literal (a model name, <c>{ array: … }</c>,
    /// <c>{ record: … }</c>, <c>{ union: [...] }</c>), or <see langword="null"/> when it holds no key to rename.
    /// </summary>
    public string? ShapeOf(IOpenApiSchema? schema)
        => ShapeOf(schema, new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// The shape of a value of the component schema <paramref name="schemaName"/> (e.g. a SignalR event payload), or
    /// <see langword="null"/> when it holds no key to rename.
    /// </summary>
    public string? ShapeOfModel(string schemaName)
        => document.Components?.Schemas?.TryGetValue(schemaName, out var schema) == true
            ? ShapeOf(new OpenApiSchemaReference(schemaName, document))
            : null;

    /// <summary>
    /// Generates <c>helpers/wireNames.ts</c>, the specification's half (its model table, converted with
    /// <c>wireNamesRuntime.ts</c>, which it re-exports), or <see langword="null"/> when nothing is renamed.
    /// </summary>
    public string? GenerateFile(string? headerContent)
    {
        if (!HasRenames)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.Append(headerContent);
        sb.Append("import { createWireConverter, type WireModels, type WireShape } from './").Append(RuntimeModule).AppendLine("';");
        sb.AppendLine();
        sb.AppendLine("/** The shape types and the converter, for code that imports them from here. */");
        sb.Append("export * from './").Append(RuntimeModule).AppendLine("';");
        sb.AppendLine();
        sb.AppendLine("/** Per model: the wire name of each property to rename, its TypeScript name and the shape of its value. */");
        sb.AppendLine("const wireModels: WireModels = {");
        foreach (var name in mapped.OrderBy(n => n, StringComparer.Ordinal))
        {
            sb.Append("  ").Append(Key(name)).AppendLine(": {");
            foreach (var property in models[name])
            {
                var shape = ShapeOf(property.Schema);
                if (string.Equals(property.Wire, property.Ts, StringComparison.Ordinal) && shape is null)
                {
                    continue;
                }

                sb.Append("    ").Append(Key(property.Wire)).Append(": [").Append(Quote(property.Ts));
                if (shape is not null)
                {
                    sb.Append(", ").Append(shape);
                }

                sb.AppendLine("],");
            }

            sb.AppendLine("  },");
        }

        sb.AppendLine("};");
        sb.AppendLine();
        sb.AppendLine("const converter = createWireConverter(wireModels);");
        sb.AppendLine();
        sb.AppendLine("/** Converts a value read from the wire to the TypeScript models (property names, dates). */");
        sb.AppendLine("export function fromWire<T = unknown>(value: unknown, shape: WireShape | undefined): T {");
        sb.AppendLine("  return converter.fromWire<T>(value, shape);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/** Converts a TypeScript model value to the API's form (property names, dates), for sending it. */");
        sb.AppendLine("export function toWire(value: unknown, shape: WireShape | undefined): unknown {");
        sb.AppendLine("  return converter.toWire(value, shape);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// The module of the generic half, next to <c>wireNames.ts</c>.
    /// </summary>
    public const string RuntimeModule = "wireNamesRuntime";

    /// <summary>
    /// Generates <c>helpers/wireNamesRuntime.ts</c>, the generic half: the shape types and the converter a
    /// specification's model table is turned into (<c>createWireConverter</c>).
    /// </summary>
    public static string GenerateRuntimeFile(string? headerContent)
    {
        var sb = new StringBuilder();
        sb.Append(headerContent);

        sb.AppendLine("/**");
        sb.AppendLine(" * The shape of a value, for converting it between the wire (the API's property names, dates as strings) and");
        sb.AppendLine(" * the TypeScript models (the naming strategy, dates as Date): a model name, a date, an array, a dictionary");
        sb.AppendLine(" * (its keys are data and stay as they are) or a union of models.");
        sb.AppendLine(" */");
        sb.AppendLine("export type WireShape =");
        sb.AppendLine("  | string");
        sb.AppendLine("  | { readonly date: 'date' | 'date-time' }");
        sb.AppendLine("  | { readonly array: WireShape }");
        sb.AppendLine("  | { readonly record: WireShape }");
        sb.AppendLine("  | { readonly union: readonly WireShape[]; readonly discriminator?: WireDiscriminator };");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * The discriminator of a union: the property's name on the wire and in the models, and the member each of its");
        sb.AppendLine(" * values names (null: a member with nothing to convert).");
        sb.AppendLine(" */");
        sb.AppendLine("export interface WireDiscriminator {");
        sb.AppendLine("  readonly wire: string;");
        sb.AppendLine("  readonly ts: string;");
        sb.AppendLine("  readonly mapping: Readonly<Record<string, string | null>>;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/** The shapes of an operation's request body, response (or stream item) and declared error bodies. */");
        sb.AppendLine("export interface WireShapes {");
        sb.AppendLine("  readonly body?: WireShape;");
        sb.AppendLine("  readonly response?: WireShape;");
        sb.AppendLine("  readonly errors?: Readonly<Record<string, WireShape>>;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("export type WireProperties = Readonly<Record<string, readonly [name: string, shape?: WireShape]>>;");
        sb.AppendLine();
        sb.AppendLine("/** Per model: the wire name of each property to rename, its TypeScript name and the shape of its value. */");
        sb.AppendLine("export type WireModels = Readonly<Record<string, WireProperties>>;");
        sb.AppendLine();
        sb.AppendLine("/** Converts values between the wire and the TypeScript models with one specification's model table. */");
        sb.AppendLine("export interface WireConverter {");
        sb.AppendLine("  fromWire<T = unknown>(value: unknown, shape: WireShape | undefined): T;");
        sb.AppendLine("  toWire(value: unknown, shape: WireShape | undefined): unknown;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("function isPlainObject(value: unknown): value is Record<string, unknown> {");
        sb.AppendLine("  if (value === null || typeof value !== 'object' || Array.isArray(value)) {");
        sb.AppendLine("    return false;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  const prototype = Object.getPrototypeOf(value);");
        sb.AppendLine("  return prototype === Object.prototype || prototype === null;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("const DATE_ONLY = /^(\\d{4})-(\\d{2})-(\\d{2})$/;");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * A declared date: a string from the wire becomes a Date; a Date is written as yyyy-MM-dd (format: date) or ISO 8601.");
        sb.AppendLine(" * A format: date is a calendar day, not an instant: it is read as the local day (new Date(y, m, d), not UTC midnight)");
        sb.AppendLine(" * and written from the local year, month and day, so it is the same day in every time zone.");
        sb.AppendLine(" */");
        sb.AppendLine("function convertDate(value: unknown, kind: 'date' | 'date-time', toWire: boolean): unknown {");
        sb.AppendLine("  if (toWire) {");
        sb.AppendLine("    if (!(value instanceof Date)) {");
        sb.AppendLine("      return value;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (kind !== 'date') {");
        sb.AppendLine("      return value.toISOString();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const month = String(value.getMonth() + 1).padStart(2, '0');");
        sb.AppendLine("    const day = String(value.getDate()).padStart(2, '0');");
        sb.AppendLine("    return `${value.getFullYear()}-${month}-${day}`;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  if (typeof value !== 'string') {");
        sb.AppendLine("    return value;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  const dateOnly = DATE_ONLY.exec(value);");
        sb.AppendLine("  const parsed = dateOnly");
        sb.AppendLine("    ? new Date(Number(dateOnly[1]), Number(dateOnly[2]) - 1, Number(dateOnly[3]))");
        sb.AppendLine("    : new Date(value);");
        sb.AppendLine("  return Number.isNaN(parsed.getTime()) ? value : parsed;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/** The converter of a model table (a specification's wireModels). */");
        sb.AppendLine("export function createWireConverter(wireModels: WireModels): WireConverter {");
        sb.AppendLine("  const tsModels = new Map<string, WireProperties>();");
        sb.AppendLine();
        sb.AppendLine("  /** The same properties keyed by their TypeScript name (for renaming the other way). */");
        sb.AppendLine("  function tsProperties(model: string): WireProperties | undefined {");
        sb.AppendLine("    const wire = wireModels[model];");
        sb.AppendLine("    if (!wire) {");
        sb.AppendLine("      return undefined;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    let ts = tsModels.get(model);");
        sb.AppendLine("    if (!ts) {");
        sb.AppendLine("      ts = Object.fromEntries(Object.entries(wire).map(([wireName, [tsName, shape]]) => [tsName, [wireName, shape] as const]));");
        sb.AppendLine("      tsModels.set(model, ts);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    return ts;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  function convert(value: unknown, shape: WireShape | undefined, toWire: boolean): unknown {");
        sb.AppendLine("    if (shape !== undefined && typeof shape === 'object' && 'date' in shape) {");
        sb.AppendLine("      return convertDate(value, shape.date, toWire);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (shape === undefined || value === null || typeof value !== 'object') {");
        sb.AppendLine("      return value;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (typeof shape === 'string') {");
        sb.AppendLine("      if (!isPlainObject(value)) {");
        sb.AppendLine("        return value;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      const properties = toWire ? tsProperties(shape) : wireModels[shape];");
        sb.AppendLine("      if (!properties) {");
        sb.AppendLine("        return value;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      const result: Record<string, unknown> = {};");
        sb.AppendLine("      for (const [key, item] of Object.entries(value)) {");
        sb.AppendLine("        const property = properties[key];");
        sb.AppendLine("        if (property) {");
        sb.AppendLine("          result[property[0]] = convert(item, property[1], toWire);");
        sb.AppendLine("        } else {");
        sb.AppendLine("          result[key] = item;");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      return result;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if ('array' in shape) {");
        sb.AppendLine("      return Array.isArray(value) ? value.map((item) => convert(item, shape.array, toWire)) : value;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if ('record' in shape) {");
        sb.AppendLine("      return isPlainObject(value)");
        sb.AppendLine("        ? Object.fromEntries(Object.entries(value).map(([key, item]) => [key, convert(item, shape.record, toWire)]))");
        sb.AppendLine("        : value;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // A union: the member its discriminator names; without one (or with a value the specification does not");
        sb.AppendLine("    // map), the member model the object has the most keys of.");
        sb.AppendLine("    if (!isPlainObject(value)) {");
        sb.AppendLine("      return value;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const discriminator = shape.discriminator;");
        sb.AppendLine("    if (discriminator) {");
        sb.AppendLine("      const kind = value[toWire ? discriminator.ts : discriminator.wire];");
        sb.AppendLine("      const member = typeof kind === 'string' ? discriminator.mapping[kind] : undefined;");
        sb.AppendLine("      if (typeof member === 'string') {");
        sb.AppendLine("        return convert(value, member, toWire);");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      if (member === null) {");
        sb.AppendLine("        return value;");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const keys = Object.keys(value);");
        sb.AppendLine("    let best: WireShape | undefined;");
        sb.AppendLine("    let bestScore = 0;");
        sb.AppendLine("    for (const member of shape.union) {");
        sb.AppendLine("      const properties = typeof member === 'string' ? (toWire ? tsProperties(member) : wireModels[member]) : undefined;");
        sb.AppendLine("      const score = properties ? keys.filter((key) => key in properties).length : 0;");
        sb.AppendLine("      if (score > bestScore) {");
        sb.AppendLine("        best = member;");
        sb.AppendLine("        bestScore = score;");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    return best === undefined ? value : convert(value, best, toWire);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  return {");
        sb.AppendLine("    fromWire<T = unknown>(value: unknown, shape: WireShape | undefined): T {");
        sb.AppendLine("      return convert(value, shape, false) as T;");
        sb.AppendLine("    },");
        sb.AppendLine("    toWire(value: unknown, shape: WireShape | undefined): unknown {");
        sb.AppendLine("      return convert(value, shape, true);");
        sb.AppendLine("    },");
        sb.AppendLine("  };");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static bool IsObjectModel(IOpenApiSchema schema)
        => schema.Properties is { Count: > 0 } || schema.AllOf is { Count: > 0 };

    private static IOpenApiSchema? ItemsOf(IOpenApiSchema? schema)
        => schema?.Items ?? schema;

    private static string? RefId(IOpenApiSchema? schema)
        => schema is OpenApiSchemaReference { Reference.Id: { Length: > 0 } id } ? id : null;

    /// <summary>
    /// The <c>discriminator</c> of a union as a TypeScript literal - the property's wire and model names and, per
    /// value (the mapping key, else the member's schema name), the member model to convert with (<c>null</c> for a
    /// member with nothing to rename, so the value is left as it is rather than converted as another member) - or
    /// <see langword="null"/> when the union declares none: the runtime then picks the member by its keys.
    /// </summary>
    private string? DiscriminatorOf(
        IOpenApiSchema schema,
        IList<IOpenApiSchema> branches,
        HashSet<string> visiting)
    {
        if (schema.DiscriminatorPropertyName is not { Length: > 0 } wire)
        {
            return null;
        }

        var mapping = schema.GetDiscriminatorMapping();
        var entries = new List<string>();
        string? ts = null;
        foreach (var branch in branches)
        {
            if (RefId(branch) is not { } member)
            {
                continue;
            }

            var values = mapping?
                .Where(m => string.Equals(m.Value, member, StringComparison.Ordinal))
                .Select(m => m.Key)
                .ToList() ?? [];
            if (values.Count == 0)
            {
                values.Add(member);
            }

            // A member is a model (its name) or nothing to convert; any other shape is not a union member a
            // discriminator can name
            var memberShape = ShapeOf(branch, visiting) is { } shape && shape.StartsWith('\'') ? shape : "null";
            ts ??= models.TryGetValue(member, out var properties)
                ? properties.Find(p => string.Equals(p.Wire, wire, StringComparison.Ordinal))?.Ts
                : null;
            entries.AddRange(values.Select(value => Key(value) + ": " + memberShape));
        }

        return entries.Count == 0
            ? null
            : "{ wire: " + Quote(wire) + ", ts: " + Quote(ts ?? wire) + ", mapping: { " + string.Join(", ", entries) + " } }";
    }

    private static string Quote(string value)
        => "'" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal) + "'";

    private static string Key(string value)
        => Regex.IsMatch(value, "^[A-Za-z_$][A-Za-z0-9_$]*$", RegexOptions.None, TimeSpan.FromSeconds(1)) ? value : Quote(value);

    /// <summary>
    /// A model's properties - its own and those of its <c>allOf</c> members (a <c>$ref</c> base's, inherited, and an
    /// inline member's) - with their wire and TypeScript names, as the model extractor emits them.
    /// </summary>
    private List<WireProperty> CollectProperties(
        IOpenApiSchema schema,
        TypeScriptNamingStrategy namingStrategy,
        HashSet<string> visiting)
    {
        var properties = new List<WireProperty>();
        foreach (var member in schema.AllOf ?? [])
        {
            if (RefId(member) is { } baseName)
            {
                if (visiting.Add(baseName) &&
                    document.Components?.Schemas?.TryGetValue(baseName, out var baseSchema) == true)
                {
                    properties.AddRange(CollectProperties(baseSchema, namingStrategy, visiting));
                }
            }
            else
            {
                properties.AddRange(CollectProperties(member, namingStrategy, visiting));
            }
        }

        foreach (var (key, propertySchema) in schema.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            properties.RemoveAll(p => string.Equals(p.Wire, key, StringComparison.Ordinal));
            properties.Add(new WireProperty(key, TypeScriptPropertyNames.Resolve(key, schema.Properties!.Keys, namingStrategy), propertySchema));
        }

        return properties;
    }

    private string? ShapeOf(
        IOpenApiSchema? schema,
        HashSet<string> visiting)
    {
        if (schema is null)
        {
            return null;
        }

        if (RefId(schema) is { } name)
        {
            if (models.ContainsKey(name))
            {
                return mapped.Contains(name) ? Quote(name) : null;
            }

            // An alias (an array, a dictionary, a union): its structure, once per path.
            return visiting.Add(name) &&
                   document.Components?.Schemas?.TryGetValue(name, out var target) == true
                ? ShapeOf(target, visiting)
                : null;
        }

        // A declared date (only with --convert-dates; a plain string that looks like a date is left alone)
        if (convertDates && schema.DateFormat is { } dateFormat)
        {
            return "{ date: '" + dateFormat + "' }";
        }

        // allOf on a property or an inline body is typed from its first $ref (and an intersection's inline members
        // keep their wire names).
        if (schema.AllOf is { Count: > 0 } allOf && allOf.FirstOrDefault(m => RefId(m) is not null) is { } firstRef)
        {
            return ShapeOf(firstRef, visiting);
        }

        if (schema.Items is { } items)
        {
            return ShapeOf(items, visiting) is { } itemShape ? "{ array: " + itemShape + " }" : null;
        }

        if ((schema.OneOf is { Count: > 0 } ? schema.OneOf : schema.AnyOf) is { Count: > 0 } branches)
        {
            var shapes = branches
                .Select(b => ShapeOf(b, visiting))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (shapes.Count == 0)
            {
                return null;
            }

            var discriminator = DiscriminatorOf(schema, branches, visiting);
            return "{ union: [" + string.Join(", ", shapes) + "]" +
                   (discriminator is null ? string.Empty : ", discriminator: " + discriminator) +
                   " }";
        }

        if (schema.Properties is not { Count: > 0 } &&
            schema.AdditionalProperties is { } values &&
            ShapeOf(values, visiting) is { } valueShape)
        {
            return "{ record: " + valueShape + " }";
        }

        return null;
    }

    private sealed record WireProperty(
        string Wire,
        string Ts,
        IOpenApiSchema? Schema);
}