namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Writes a string enum of a generated client as an <b>extensible enum</b>: a <c>readonly record struct</c> over the
/// value on the wire, with the specification's values as named members.
/// <para>
/// A C# <c>enum</c> can only hold the values it was compiled with, so a client built before the server added a value
/// fails the whole response when that value arrives. The extensible enum keeps the value instead: a known value
/// equals its member (<c>status == DeviceStatus.Online</c>), and a value the specification does not list is read
/// with <c>IsKnown</c> false and written back unchanged. Servers keep the strict C# enum.
/// </para>
/// </summary>
public static class ExtensibleEnumContentGenerator
{
    /// <summary>
    /// The struct's own members: a specification value with one of these names gets the suffix <c>Member</c>.
    /// </summary>
    private static readonly HashSet<string> ReservedMemberNames = new(StringComparer.Ordinal)
    {
        "Value", "IsKnown", "KnownValues", "Names", "Parse", "TryParse", "ToString", "ToWireValue",
        "Equals", "GetHashCode", "GetType", "Deconstruct", "MemberwiseClone", "ReferenceEquals", "Finalize",
    };

    /// <summary>
    /// Whether an enum can be written as an extensible enum: a string enum (an integer enum is a C# enum that already
    /// holds any number, and a flags enum is a set rather than a value).
    /// </summary>
    /// <param name="parameters">The enum.</param>
    /// <returns><see langword="true"/> for a string enum.</returns>
    public static bool AppliesTo(EnumParameters parameters)
        => parameters is not null &&
           !parameters.UseFlags &&
           parameters.Values.Count > 0 &&
           parameters.Values.All(v => v.Value is null);

    /// <summary>
    /// Generates the extensible enum, its JSON converter and its <c>ToWireValue()</c> extension for a nullable value.
    /// </summary>
    /// <param name="parameters">The enum, as the enum extractors describe it.</param>
    /// <returns>The source of the file.</returns>
    public static string Generate(EnumParameters parameters)
    {
        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var name = parameters.EnumTypeName;
        var modifier = parameters.DeclarationModifier == DeclarationModifiers.Internal ? "internal" : "public";
        var members = MembersOf(parameters);
        var generatedCode = parameters.Attributes?.FirstOrDefault(a => a.Name == "GeneratedCode") is { Content: { } content }
            ? $"[GeneratedCode({content})]"
            : null;

        var sb = new StringBuilder();
        sb.Append(HeaderBuilder.WithUsings(
            NamespaceConstants.System,
            NamespaceConstants.SystemCodeDomCompiler,
            NamespaceConstants.SystemCollectionsGeneric,
            NamespaceConstants.SystemDiagnosticsCodeAnalysis,
            NamespaceConstants.SystemTextJson,
            NamespaceConstants.SystemTextJsonSerialization));
        sb.AppendLine($"namespace {parameters.Namespace};");
        sb.AppendLine();

        if (parameters.DocumentationTags is not null)
        {
            sb.Append(new CodeDocumentationTagsGenerator().GenerateTags(0, parameters.DocumentationTags));
        }

        sb.AppendLine("/// <remarks>");
        sb.AppendLine("/// An extensible enum: a value the specification does not list is kept in <see cref=\"Value\"/>, with");
        sb.AppendLine("/// <see cref=\"IsKnown\"/> <see langword=\"false\"/>, instead of failing the response it arrives in.");
        sb.AppendLine("/// </remarks>");
        AppendGeneratedCode(sb, generatedCode);
        sb.AppendLine($"[JsonConverter(typeof({name}JsonConverter))]");
        sb.AppendLine($"{modifier} readonly record struct {name} : IParsable<{name}>");
        sb.AppendLine("{");

        AppendNames(sb, members);
        AppendKnownMembers(sb, name, members);
        AppendConstructorAndValue(sb, name, members);
        AppendParsing(sb, name);
        AppendRecognize(sb, members);

        sb.AppendLine("}");
        sb.AppendLine();
        AppendJsonConverter(sb, name, modifier, generatedCode);
        sb.AppendLine();
        AppendNullableExtension(sb, name, modifier, generatedCode);

        return sb.ToString();
    }

    private static List<(string Name, string Literal)> MembersOf(
        EnumParameters parameters)
    {
        var used = new HashSet<string>(parameters.Values.Select(v => v.Name), StringComparer.Ordinal);
        var members = new List<(string Name, string Literal)>();
        foreach (var value in parameters.Values)
        {
            var memberName = value.Name;
            if (ReservedMemberNames.Contains(memberName) ||
                string.Equals(memberName, parameters.EnumTypeName, StringComparison.Ordinal))
            {
                var candidate = memberName + "Member";
                var suffix = 1;
                while (used.Contains(candidate))
                {
                    suffix++;
                    candidate = memberName + "Member" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                used.Add(candidate);
                memberName = candidate;
            }

            members.Add((memberName, CSharpLiteralHelper.ToStringLiteral(value.EnumMemberValue ?? value.Name)));
        }

        return members;
    }

    private static void AppendNames(
        StringBuilder sb,
        List<(string Name, string Literal)> members)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// The values the specification lists, as constants - for a <c>switch</c> on <see cref=\"Value\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static class Names");
        sb.AppendLine("    {");
        for (var i = 0; i < members.Count; i++)
        {
            sb.AppendLine($"        public const string {members[i].Name} = {members[i].Literal};");
            if (i < members.Count - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void AppendKnownMembers(
        StringBuilder sb,
        string name,
        List<(string Name, string Literal)> members)
    {
        foreach (var member in members)
        {
            sb.AppendLine($"    public static {name} {member.Name} {{ get; }} = new(Names.{member.Name});");
            sb.AppendLine();
        }

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// The values the specification lists, in its order.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static IReadOnlyList<{name}> KnownValues {{ get; }} = new[]");
        sb.AppendLine("    {");
        foreach (var member in members)
        {
            sb.AppendLine($"        {member.Name},");
        }

        sb.AppendLine("    };");
        sb.AppendLine();
    }

    private static void AppendConstructorAndValue(
        StringBuilder sb,
        string name,
        List<(string Name, string Literal)> members)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Initializes a new instance with a value as it is sent on the wire. A listed value in another casing is");
        sb.AppendLine("    /// taken as the listed one.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"value\">The value.</param>");
        sb.AppendLine($"    public {name}(string value)");
        sb.AppendLine("        => Value = Recognize(value ?? throw new ArgumentNullException(nameof(value)));");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// The value as it is sent on the wire; <see langword=\"null\"/> only for <see langword=\"default\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public string Value { get; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Whether the value is one the specification lists.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public bool IsKnown => Value is {string.Join(" or ", members.Select(m => "Names." + m.Name))};");
        sb.AppendLine();

        // Explicit: an implicit conversion took a null for a string (`none ? null : Status.Online`) and threw
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// A value as it is sent on the wire, written out: <c>(" + name + ")\"value\"</c>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"value\">The value.</param>");
        sb.AppendLine($"    public static explicit operator {name}(string value)");
        sb.AppendLine("        => new(value);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// The value as it is sent on the wire, for a path, a query string, a header or a cookie.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <returns>The value; empty for <see langword=\"default\"/>.</returns>");
        sb.AppendLine("    public string ToWireValue()");
        sb.AppendLine("        => Value ?? string.Empty;");
        sb.AppendLine();
        sb.AppendLine("    public override string ToString()");
        sb.AppendLine("        => Value ?? string.Empty;");
        sb.AppendLine();
    }

    private static void AppendParsing(
        StringBuilder sb,
        string name)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Takes any value: whether it is one the specification lists is <see cref=\"IsKnown\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static {name} Parse(string s)");
        sb.AppendLine("        => new(s);");
        sb.AppendLine();
        sb.AppendLine($"    public static {name} Parse(string s, IFormatProvider? provider)");
        sb.AppendLine("        => new(s);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Succeeds for any value but <see langword=\"null\"/>: whether it is one the specification lists is");
        sb.AppendLine("    /// <see cref=\"IsKnown\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static bool TryParse([NotNullWhen(true)] string? s, out {name} result)");
        sb.AppendLine("        => TryParse(s, provider: null, out result);");
        sb.AppendLine();
        sb.AppendLine($"    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out {name} result)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (s is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            result = default;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        result = new(s);");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void AppendRecognize(
        StringBuilder sb,
        List<(string Name, string Literal)> members)
    {
        sb.AppendLine("    private static string Recognize(string value)");
        sb.AppendLine("    {");
        sb.AppendLine($"        if (value is {string.Join(" or ", members.Select(m => "Names." + m.Name))})");
        sb.AppendLine("        {");
        sb.AppendLine("            return value;");
        sb.AppendLine("        }");
        sb.AppendLine();
        foreach (var member in members)
        {
            sb.AppendLine($"        if (string.Equals(value, Names.{member.Name}, StringComparison.OrdinalIgnoreCase))");
            sb.AppendLine("        {");
            sb.AppendLine($"            return Names.{member.Name};");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("        return value;");
        sb.AppendLine("    }");
    }

    private static void AppendJsonConverter(
        StringBuilder sb,
        string name,
        string modifier,
        string? generatedCode)
    {
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Reads and writes <see cref=\"{name}\"/> as the JSON string it is on the wire, also as a property name.");
        sb.AppendLine("/// </summary>");
        AppendGeneratedCode(sb, generatedCode);
        sb.AppendLine($"{modifier} sealed class {name}JsonConverter : JsonConverter<{name}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public override bool HandleNull => true;");
        sb.AppendLine();
        sb.AppendLine($"    public override {name} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
        sb.AppendLine("        => reader.TokenType switch");
        sb.AppendLine("        {");
        sb.AppendLine($"            JsonTokenType.String => new {name}(reader.GetString()!),");
        sb.AppendLine("            JsonTokenType.Number => FromNumber(ref reader),");
        sb.AppendLine("            JsonTokenType.Null => default,");
        sb.AppendLine($"            _ => throw new JsonException($\"A {name} is a JSON string, not {{reader.TokenType}}.\"),");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine($"    public override void Write(Utf8JsonWriter writer, {name} value, JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value.Value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNullValue();");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteStringValue(value.Value);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public override {name} ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)");
        sb.AppendLine($"        => new(reader.GetString()!);");
        sb.AppendLine();
        sb.AppendLine($"    public override void WriteAsPropertyName(Utf8JsonWriter writer, {name} value, JsonSerializerOptions options)");
        sb.AppendLine("        => writer.WritePropertyName(value.ToWireValue());");
        sb.AppendLine();
        sb.AppendLine("    // A server without a string converter writes a C# enum as its position in the list: read it as that value,");
        sb.AppendLine("    // and keep any other number as an unknown value");
        sb.AppendLine($"    private static {name} FromNumber(ref Utf8JsonReader reader)");
        sb.AppendLine($"        => reader.TryGetInt32(out var position) && position >= 0 && position < {name}.KnownValues.Count");
        sb.AppendLine($"            ? {name}.KnownValues[position]");
        sb.AppendLine($"            : new {name}(System.Text.Encoding.UTF8.GetString(reader.HasValueSequence ? System.Buffers.BuffersExtensions.ToArray(reader.ValueSequence) : reader.ValueSpan.ToArray()));");
        sb.AppendLine("}");
    }

    private static void AppendNullableExtension(
        StringBuilder sb,
        string name,
        string modifier,
        string? generatedCode)
    {
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// The value of a <see cref=\"{name}\"/> that may be absent, as the specification writes it.");
        sb.AppendLine("/// </summary>");
        AppendGeneratedCode(sb, generatedCode);
        sb.AppendLine($"{modifier} static class {name}Extensions");
        sb.AppendLine("{");
        sb.AppendLine($"    public static string? ToWireValue(this {name}? value)");
        sb.AppendLine("        => value is { } present ? present.ToWireValue() : null;");
        sb.Append('}');
    }

    private static void AppendGeneratedCode(
        StringBuilder sb,
        string? generatedCode)
    {
        if (generatedCode is not null)
        {
            sb.AppendLine(generatedCode);
        }
    }
}