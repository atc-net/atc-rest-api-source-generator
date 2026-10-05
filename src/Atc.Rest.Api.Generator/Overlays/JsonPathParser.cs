namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// Parses RFC 9535 JSONPath (section 2 and the collected ABNF of appendix A), including the well-typedness rules of
/// section 2.4.3: an ill-typed function expression is a syntax error.
/// </summary>
internal sealed class JsonPathParser
{
    // I-JSON integer range (RFC 9535 section 2.1)
    private const long MaxSafeInteger = 9007199254740991;

    private readonly string text;
    private int position;

    public JsonPathParser(string text)
    {
        this.text = text;
    }

    private bool AtEnd => position >= text.Length;

    private char Current => text[position];

    public JsonPathSegment[] ParseRootQuery()
    {
        if (AtEnd || Current != '$')
        {
            throw Error("a query starts with '$'");
        }

        position++;
        var segments = ParseSegments();
        if (!AtEnd)
        {
            throw Error($"unexpected '{Current}'");
        }

        return segments;
    }

    private JsonPathSegment[] ParseSegments()
    {
        var segments = new List<JsonPathSegment>();
        while (true)
        {
            var start = position;
            SkipBlanks();
            if (!AtEnd && (Current == '[' || Current == '.'))
            {
                segments.Add(ParseSegment());
                continue;
            }

            position = start;
            return [.. segments];
        }
    }

    private JsonPathSegment ParseSegment()
    {
        if (Current == '[')
        {
            return new JsonPathSegment(IsDescendant: false, ParseBracketedSelection());
        }

        // '.'
        position++;
        if (!AtEnd && Current == '.')
        {
            position++;
            if (AtEnd)
            {
                throw Error("a descendant segment needs a selector");
            }

            if (text[position] == '[')
            {
                return new JsonPathSegment(IsDescendant: true, ParseBracketedSelection());
            }

            return new JsonPathSegment(IsDescendant: true, [ParseDotSelector()]);
        }

        return new JsonPathSegment(IsDescendant: false, [ParseDotSelector()]);
    }

    private JsonPathSelector ParseDotSelector()
    {
        if (AtEnd)
        {
            throw Error("a member name or '*' must follow '.'");
        }

        if (Current == '*')
        {
            position++;
            return new WildcardSelector();
        }

        return new NameSelector(ParseMemberNameShorthand());
    }

    private string ParseMemberNameShorthand()
    {
        var start = position;
        if (AtEnd || !IsNameFirst(position, out var width))
        {
            throw Error("a member name must follow '.'");
        }

        position += width;
        while (!AtEnd && (IsDigit(Current) || IsNameFirst(position, out width)))
        {
            position += IsDigit(Current) ? 1 : width;
        }

        return text.Substring(start, position - start);
    }

    // name-first = ALPHA / "_" / %x80-D7FF / %xE000-10FFFF (a surrogate pair is one character)
    private bool IsNameFirst(
        int index,
        out int width)
    {
        width = 1;
        var c = text[index];
        if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_')
        {
            return true;
        }

        if (c is >= '\u0080' and <= '퟿' or >= '')
        {
            return true;
        }

        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            width = 2;
            return true;
        }

        return false;
    }

    private List<JsonPathSelector> ParseBracketedSelection()
    {
        // '['
        position++;
        var selectors = new List<JsonPathSelector>();
        SkipBlanks();
        selectors.Add(ParseSelector());
        while (true)
        {
            SkipBlanks();
            if (AtEnd)
            {
                throw Error("']' expected");
            }

            if (Current == ']')
            {
                position++;
                return selectors;
            }

            if (Current != ',')
            {
                throw Error($"',' or ']' expected, not '{Current}'");
            }

            position++;
            SkipBlanks();
            selectors.Add(ParseSelector());
        }
    }

    private JsonPathSelector ParseSelector()
    {
        if (AtEnd)
        {
            throw Error("a selector expected");
        }

        switch (Current)
        {
            case '\'' or '"':
                return new NameSelector(ParseStringLiteral());
            case '*':
                position++;
                return new WildcardSelector();
            case '?':
                position++;
                SkipBlanks();
                return new FilterSelector(ParseOr(allowBare: false));
        }

        // An index or a slice
        long? start = null;
        if (Current == '-' || IsDigit(Current))
        {
            start = ParseInteger();
        }

        SkipBlanks();
        if (AtEnd || Current != ':')
        {
            if (start is null)
            {
                throw Error("a selector expected");
            }

            return new IndexSelector(start.Value);
        }

        position++;
        SkipBlanks();
        long? end = null;
        if (!AtEnd && (Current == '-' || IsDigit(Current)))
        {
            end = ParseInteger();
            SkipBlanks();
        }

        long? step = null;
        if (!AtEnd && Current == ':')
        {
            position++;
            SkipBlanks();
            if (!AtEnd && (Current == '-' || IsDigit(Current)))
            {
                step = ParseInteger();
            }
        }

        return new SliceSelector(start, end, step);
    }

    // int = "0" / (["-"] DIGIT1 *DIGIT), within the I-JSON range
    private long ParseInteger()
    {
        var start = position;
        if (Current == '-')
        {
            position++;
        }

        if (AtEnd || !IsDigit(Current))
        {
            throw Error("a digit expected");
        }

        if (Current == '0')
        {
            position++;
            if (position - start > 1)
            {
                throw Error("'-0' is no integer");
            }

            if (!AtEnd && IsDigit(Current))
            {
                throw Error("an integer has no leading zero");
            }

            return 0;
        }

        while (!AtEnd && IsDigit(Current))
        {
            position++;
        }

        var digits = text.Substring(start, position - start);
        if (!long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ||
            value is > MaxSafeInteger or < -MaxSafeInteger)
        {
            throw Error($"{digits} is outside the integer range");
        }

        return value;
    }

    private JsonPathExpression ParseOr(bool allowBare)
    {
        var left = ParseAnd(allowBare);
        while (TryOperator("||"))
        {
            left = new OrExpression(ToLogical(left), ToLogical(ParseAnd(allowBare: false)));
        }

        return left;
    }

    private JsonPathExpression ParseAnd(bool allowBare)
    {
        var left = ParseBasic(allowBare);
        while (TryOperator("&&"))
        {
            left = new AndExpression(ToLogical(left), ToLogical(ParseBasic(allowBare: false)));
        }

        return left;
    }

    private bool TryOperator(string op)
    {
        var start = position;
        SkipBlanks();
        if (string.CompareOrdinal(text, position, op, 0, op.Length) == 0)
        {
            position += op.Length;
            SkipBlanks();
            return true;
        }

        position = start;
        return false;
    }

    // basic-expr = paren-expr / comparison-expr / test-expr
    private JsonPathExpression ParseBasic(bool allowBare)
    {
        if (AtEnd)
        {
            throw Error("an expression expected");
        }

        if (Current == '!' && !(position + 1 < text.Length && text[position + 1] == '='))
        {
            position++;
            SkipBlanks();
            if (!AtEnd && Current == '(')
            {
                return new NotExpression(ParseParenthesized());
            }

            var operand = ParsePrimary();
            if (operand is LiteralExpression)
            {
                throw Error("'!' applies to a query, a function or a parenthesized expression");
            }

            return new NotExpression(ToLogical(operand));
        }

        if (Current == '(')
        {
            return ParseParenthesized();
        }

        var left = ParsePrimary();
        var start = position;
        SkipBlanks();
        var comparison = ReadComparisonOperator();
        if (comparison is null)
        {
            position = start;
            return allowBare ? left : ToLogical(left);
        }

        SkipBlanks();
        var right = ParsePrimary();
        RequireComparable(left);
        RequireComparable(right);
        return new ComparisonExpression(comparison, left, right);
    }

    private JsonPathExpression ParseParenthesized()
    {
        // '('
        position++;
        SkipBlanks();
        var inner = ToLogical(ParseOr(allowBare: false));
        SkipBlanks();
        if (AtEnd || Current != ')')
        {
            throw Error("')' expected");
        }

        position++;
        return inner;
    }

    private string? ReadComparisonOperator()
    {
        foreach (var op in new[] { "==", "!=", "<=", ">=", "<", ">" })
        {
            if (string.CompareOrdinal(text, position, op, 0, op.Length) == 0)
            {
                position += op.Length;
                return op;
            }
        }

        return null;
    }

    // A comparable is a literal, a singular query or a function of value type
    private void RequireComparable(JsonPathExpression expression)
    {
        switch (expression)
        {
            case LiteralExpression:
                return;
            case QueryExpression query when query.IsSingular:
                return;
            case QueryExpression:
                throw Error("a comparison takes a singular query (names and indexes only)");
            case FunctionExpression { ResultType: JsonPathType.Value }:
                return;
            case FunctionExpression function:
                throw Error($"{function.Name}() does not give a value to compare");
            default:
                throw Error("a comparison takes a literal, a singular query or a function");
        }
    }

    // A query or a function used where a logical value is expected (a test expression)
    private JsonPathExpression ToLogical(JsonPathExpression expression)
        => expression switch
        {
            LiteralExpression => throw Error("a literal is no test: compare it"),
            QueryExpression => new TestExpression(expression),
            FunctionExpression { ResultType: JsonPathType.Value } function => throw Error($"{function.Name}() gives a value: compare it"),
            FunctionExpression => new TestExpression(expression),
            _ => expression,
        };

    private JsonPathExpression ParsePrimary()
    {
        if (AtEnd)
        {
            throw Error("an expression expected");
        }

        var c = Current;
        if (c is '@' or '$')
        {
            position++;
            return new QueryExpression(IsRelative: c == '@', ParseSegments());
        }

        if (c is '\'' or '"')
        {
            return new LiteralExpression(JsonValue.Create(ParseStringLiteral()));
        }

        if (c == '-' || IsDigit(c))
        {
            return new LiteralExpression(ParseNumberLiteral());
        }

        foreach (var (keyword, value) in new (string Keyword, JsonNode? Value)[] { ("true", JsonValue.Create(true)), ("false", JsonValue.Create(false)), ("null", null) })
        {
            if (string.CompareOrdinal(text, position, keyword, 0, keyword.Length) == 0 &&
                (position + keyword.Length >= text.Length || !IsFunctionNameChar(text[position + keyword.Length])))
            {
                position += keyword.Length;
                return new LiteralExpression(value);
            }
        }

        if (c is >= 'a' and <= 'z')
        {
            return ParseFunction();
        }

        throw Error($"unexpected '{c}'");
    }

    private JsonPathExpression ParseFunction()
    {
        var start = position;
        while (!AtEnd && IsFunctionNameChar(Current))
        {
            position++;
        }

        var name = text.Substring(start, position - start);
        if (AtEnd || Current != '(')
        {
            throw Error($"'(' expected after {name}");
        }

        position++;
        SkipBlanks();
        var arguments = new List<JsonPathExpression>();
        if (!AtEnd && Current != ')')
        {
            arguments.Add(ParseOr(allowBare: true));
            SkipBlanks();
            while (!AtEnd && Current == ',')
            {
                position++;
                SkipBlanks();
                arguments.Add(ParseOr(allowBare: true));
                SkipBlanks();
            }
        }

        if (AtEnd || Current != ')')
        {
            throw Error("')' expected");
        }

        position++;

        var (parameters, result) = name switch
        {
            "length" => (new[] { JsonPathType.Value }, JsonPathType.Value),
            "count" => (new[] { JsonPathType.Nodes }, JsonPathType.Value),
            "match" or "search" => (new[] { JsonPathType.Value, JsonPathType.Value }, JsonPathType.Logical),
            "value" => (new[] { JsonPathType.Nodes }, JsonPathType.Value),
            _ => throw Error($"unknown function {name}()"),
        };

        if (arguments.Count != parameters.Length)
        {
            throw Error($"{name}() takes {parameters.Length} argument(s)");
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            RequireArgumentType(name, arguments[i], parameters[i]);
        }

        return new FunctionExpression(name, arguments, result);
    }

    private void RequireArgumentType(
        string function,
        JsonPathExpression argument,
        JsonPathType parameter)
    {
        var fits = parameter switch
        {
            JsonPathType.Value => argument switch
            {
                LiteralExpression => true,
                QueryExpression query => query.IsSingular,
                FunctionExpression f => f.ResultType == JsonPathType.Value,
                _ => false,
            },
            JsonPathType.Nodes => argument is QueryExpression or FunctionExpression { ResultType: JsonPathType.Nodes },
            _ => argument is not LiteralExpression and not FunctionExpression { ResultType: JsonPathType.Value },
        };

        if (!fits)
        {
            throw Error($"an argument of {function}() has the wrong type");
        }
    }

    private static bool IsFunctionNameChar(char c)
        => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_';

    // number = (int / "-0") [ frac ] [ exp ]
    private JsonNode ParseNumberLiteral()
    {
        var start = position;
        if (Current == '-')
        {
            position++;
        }

        if (AtEnd || !IsDigit(Current))
        {
            throw Error("a digit expected");
        }

        if (Current == '0')
        {
            position++;
            if (!AtEnd && IsDigit(Current))
            {
                throw Error("a number has no leading zero");
            }
        }
        else
        {
            while (!AtEnd && IsDigit(Current))
            {
                position++;
            }
        }

        if (!AtEnd && Current == '.')
        {
            position++;
            RequireDigits();
        }

        if (!AtEnd && Current is 'e' or 'E')
        {
            position++;
            if (!AtEnd && Current is '+' or '-')
            {
                position++;
            }

            RequireDigits();
        }

        var literal = text.Substring(start, position - start);
        return JsonValue.Create(double.Parse(literal, NumberStyles.Float, CultureInfo.InvariantCulture));
    }

    private void RequireDigits()
    {
        if (AtEnd || !IsDigit(Current))
        {
            throw Error("a digit expected");
        }

        while (!AtEnd && IsDigit(Current))
        {
            position++;
        }
    }

    private string ParseStringLiteral()
    {
        var quote = Current;
        position++;
        var sb = new StringBuilder();
        while (true)
        {
            if (AtEnd)
            {
                throw Error("an unterminated string");
            }

            var c = Current;
            position++;
            if (c == quote)
            {
                return sb.ToString();
            }

            if (c == '\\')
            {
                sb.Append(ParseEscape(quote));
                continue;
            }

            if (c < 0x20)
            {
                throw Error("a control character must be escaped in a string");
            }

            if (char.IsHighSurrogate(c))
            {
                if (AtEnd || !char.IsLowSurrogate(Current))
                {
                    throw Error("an unpaired surrogate");
                }

                sb.Append(c).Append(Current);
                position++;
                continue;
            }

            if (char.IsLowSurrogate(c))
            {
                throw Error("an unpaired surrogate");
            }

            sb.Append(c);
        }
    }

    private string ParseEscape(char quote)
    {
        if (AtEnd)
        {
            throw Error("an unterminated escape");
        }

        var c = Current;
        position++;
        switch (c)
        {
            case 'b': return "\b";
            case 'f': return "\f";
            case 'n': return "\n";
            case 'r': return "\r";
            case 't': return "\t";
            case '/': return "/";
            case '\\': return "\\";
            case 'u': return ParseUnicodeEscape();
        }

        if (c == quote)
        {
            return c.ToString();
        }

        throw Error($"'\\{c}' is no escape");
    }

    private string ParseUnicodeEscape()
    {
        var first = ReadHex4();
        if (first is >= 0xDC00 and <= 0xDFFF)
        {
            throw Error("an unpaired low surrogate");
        }

        if (first is < 0xD800 or > 0xDBFF)
        {
            return ((char)first).ToString();
        }

        if (position + 1 >= text.Length || text[position] != '\\' || text[position + 1] != 'u')
        {
            throw Error("a high surrogate must be followed by a low one");
        }

        position += 2;
        var second = ReadHex4();
        if (second is < 0xDC00 or > 0xDFFF)
        {
            throw Error("a high surrogate must be followed by a low one");
        }

        return new string([(char)first, (char)second]);
    }

    private int ReadHex4()
    {
        if (position + 4 > text.Length)
        {
            throw Error("four hex digits expected");
        }

        var value = 0;
        for (var i = 0; i < 4; i++)
        {
            var c = text[position + i];
            var digit = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'f' => c - 'a' + 10,
                >= 'A' and <= 'F' => c - 'A' + 10,
                _ => throw Error("four hex digits expected"),
            };
            value = (value * 16) + digit;
        }

        position += 4;
        return value;
    }

    // S = *B, B = space / tab / LF / CR
    private void SkipBlanks()
    {
        while (!AtEnd && Current is ' ' or '\t' or '\n' or '\r')
        {
            position++;
        }
    }

    private static bool IsDigit(char c)
        => c is >= '0' and <= '9';

    private JsonPathException Error(string reason)
        => new($"Invalid JSONPath '{text}' at position {position}: {reason}.");
}