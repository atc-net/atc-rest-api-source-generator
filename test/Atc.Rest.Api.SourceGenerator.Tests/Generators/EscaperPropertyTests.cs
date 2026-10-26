namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Property-based tests of the helpers that put specification text into generated source: any string - control
/// characters, quotes, backslashes, line separators, lone surrogates, any script - must come out as a literal that
/// means the same text and survives the generated file's UTF-8 encoding, and any name as a valid identifier. A culture
/// must never change what is written on the wire. The fixed odd strings of the robustness fuzz test cover a few
/// cases; these cover the space.
/// </summary>
public sealed class EscaperPropertyTests
{
    private const long Iterations = 20_000;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // Any UTF-16 code unit (lone surrogates included) and CsCheck's own string generator, which leans towards edges
    private static readonly Gen<string> AnyText = Gen.OneOf(
        Gen.Char[char.MinValue, char.MaxValue].Array[0, 40].Select(chars => new string(chars)),
        Gen.String);

    private static readonly Gen<string> AnyName = AnyText.Where(s => s.Length > 0);

    [Fact]
    public void CSharpStringLiteral_MeansTheSameText_AndIsValidUtf8()
        => AnyText.Sample(
            text =>
            {
                var literal = CSharpLiteralHelper.ToStringLiteral(text);
                var expression = SyntaxFactory.ParseExpression(literal);

                Assert.Empty(expression.GetDiagnostics());
                var token = Assert.IsType<LiteralExpressionSyntax>(expression).Token;
                Assert.Equal(text, token.ValueText);
                StrictUtf8.GetBytes(literal);
            },
            iter: Iterations);

    [Fact]
    public void TypeScriptStringLiteral_MeansTheSameText_AndIsValidUtf8()
        => AnyText.Sample(
            text =>
            {
                var literal = TypeScriptLiteralHelper.ToStringLiteral(text);

                Assert.Equal(text, DecodeJavaScriptSingleQuoted(literal));
                StrictUtf8.GetBytes(literal);
            },
            iter: Iterations);

    [Fact]
    public void CSharpEnumMemberName_IsAValidIdentifier()
        => AnyName.Sample(name => AssertCSharpIdentifier(CSharpLiteralHelper.ToEnumMemberName(name), name), iter: Iterations);

    [Fact]
    public void SchemaName_IsAValidTypeName()
        => AnyName.Sample(name => AssertCSharpIdentifier(OpenApiSchemaExtensions.SanitizeSchemaName(name), name), iter: Iterations);

    [Fact]
    public void PolicyConstantName_IsAValidIdentifier()
        => AnyName.Sample(name => AssertCSharpIdentifier(PolicyNamingHelper.ToConstantName(name), name), iter: Iterations);

    [Fact]
    public void TypeScriptEnumMemberName_IsAValidIdentifier()
        => AnyName.Sample(
            name =>
            {
                var member = TypeScriptLiteralHelper.ToEnumMemberName(name);
                Assert.True(IsTypeScriptIdentifier(member), $"{Show(name)} became {Show(member)}");
                StrictUtf8.GetBytes(member);
            },
            iter: Iterations);

    // A member name is a property name, where a reserved word is allowed - the sample once drew "do"
    [Theory]
    [InlineData("do")]
    [InlineData("delete")]
    [InlineData("default")]
    public void TypeScriptEnumMemberName_KeepsAReservedWord(string name)
        => Assert.Equal(name, TypeScriptLiteralHelper.ToEnumMemberName(name));

    // U+A7D5 (small double thorn): the ICU of a newer Linux upper-cases it to a letter this runtime does not know yet,
    // which the property tests drew on CI. The member name keeps a letter it can read
    [Theory]
    [InlineData("ꟕ")]
    [InlineData("x-ꟕ")]
    [InlineData("ꟕ︞-䋄")]
    public void EnumMemberName_OfALetterWhoseUpperCaseIsNoLetterHere_IsAValidIdentifier(
        string name)
    {
        AssertCSharpIdentifier(CSharpLiteralHelper.ToEnumMemberName(name), name);
        var member = TypeScriptLiteralHelper.ToEnumMemberName(name);
        Assert.True(IsTypeScriptIdentifier(member), $"{Show(name)} became {Show(member)}");
    }

    /// <summary>
    /// The wire text of a date, time or number is the same under every culture, and reads back to the same value.
    /// The expressions are the ones the generated clients and servers use (compiled once per C# type).
    /// </summary>
    [Fact]
    public void WireText_IsTheSameUnderEveryCulture_AndReadsBack()
    {
        var types = new[] { "DateOnly", "TimeOnly", "DateTime", "DateTimeOffset", "double", "float", "decimal", "int", "long" };
        var source = new StringBuilder("namespace WireProbe; public static class Wire {");
        foreach (var type in types)
        {
            source.Append(CultureInfo.InvariantCulture, $" public static string? Format_{type}({type} value) => {WireValueHelper.TryBuildWireExpression("value", type)};");
        }

        source.Append(" }");
        var wire = CompilationVerificationHarness.EmitAndLoad([("Wire.cs", source.ToString())]).GetType("WireProbe.Wire", throwOnError: true)!;
        var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);

        string Format(
            string type,
            object value,
            CultureInfo culture)
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = culture;
            try
            {
                return (string)wire.GetMethod("Format_" + type)!.Invoke(null, [value])!;
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        void Check<T>(
            string type,
            Gen<T> values,
            Func<string, T> parse)
            where T : notnull
            => Gen.Select(values, Gen.Int[0, cultures.Length - 1]).Sample(
                (value, cultureIndex) =>
                {
                    var invariant = Format(type, value, CultureInfo.InvariantCulture);
                    var text = Format(type, value, cultures[cultureIndex]);

                    Assert.Equal(invariant, text);
                    Assert.Equal(value, parse(text));
                },
                iter: 2_000);

        Check("DateOnly", Gen.Date.Select(DateOnly.FromDateTime), s => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        Check("TimeOnly", Gen.Long[0, TimeSpan.TicksPerDay - 1].Select(t => new TimeOnly(t)), s => TimeOnly.ParseExact(s, "HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
        Check("DateTimeOffset", Gen.DateTimeOffset, s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        Check("DateTime", Gen.DateTime, s => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        Check("double", Gen.Double, s => double.Parse(s, CultureInfo.InvariantCulture));
        Check("float", Gen.Float, s => float.Parse(s, CultureInfo.InvariantCulture));
        Check("decimal", Gen.Decimal, s => decimal.Parse(s, NumberStyles.Number, CultureInfo.InvariantCulture));
        Check("int", Gen.Int, s => int.Parse(s, CultureInfo.InvariantCulture));
        Check("long", Gen.Long, s => long.Parse(s, CultureInfo.InvariantCulture));
    }

    private static void AssertCSharpIdentifier(
        string identifier,
        string name)
    {
        Assert.True(
            SyntaxFacts.IsValidIdentifier(identifier) && SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None,
            $"{Show(name)} became {Show(identifier)}");
        StrictUtf8.GetBytes(identifier);
    }

    // An ECMAScript identifier name: a letter, $ or _ first, then letters, digits, $ or _. A reserved word is one too:
    // an enum member is a property name (enum E { do = 'do' } compiles), so TypeScriptEnumMemberName_KeepsAReservedWord
    private static bool IsTypeScriptIdentifier(string name)
        => name.Length > 0
           && (char.IsLetter(name[0]) || name[0] is '$' or '_')
           && name.All(c => char.IsLetterOrDigit(c) || c is '$' or '_');

    // What a JavaScript engine reads from a single-quoted literal, for the escapes the helper may write
    private static string DecodeJavaScriptSingleQuoted(string literal)
    {
        Assert.True(literal.Length >= 2 && literal[0] == '\'' && literal[^1] == '\'', $"not a single-quoted literal: {Show(literal)}");
        var sb = new StringBuilder();
        var i = 0;
        while (++i < literal.Length - 1)
        {
            var c = literal[i];
            if (c is '\'' or '\n' or '\r' or (char)0x2028 or (char)0x2029)
            {
                Assert.Fail($"a raw {Show(c.ToString())} ends the literal: {Show(literal)}");
            }

            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }

            var escape = literal[++i];
            switch (escape)
            {
                case '\\':
                case '\'':
                case '"':
                    sb.Append(escape);
                    break;
                case 'n':
                    sb.Append('\n');
                    break;
                case 'r':
                    sb.Append('\r');
                    break;
                case 't':
                    sb.Append('\t');
                    break;
                case 'u':
                    sb.Append((char)int.Parse(literal.AsSpan(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    i += 4;
                    break;
                default:
                    Assert.Fail($"an escape the helper should not write: \\{escape} in {Show(literal)}");
                    break;
            }
        }

        return sb.ToString();
    }

    private static string Show(string text)
        => string.Concat(text.Select(c => c is >= ' ' and <= '~' ? c.ToString() : $"\\u{(int)c:x4}"));
}