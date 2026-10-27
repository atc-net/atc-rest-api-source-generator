namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// Evaluates a parsed query (RFC 9535 sections 2.3 to 2.5).
/// </summary>
internal static class JsonPathEvaluator
{
    private const int MaxCachedRegexes = 256;

    private static readonly Dictionary<string, Regex?> RegexCache = new(StringComparer.Ordinal);

    public static List<JsonPathMatch> Select(
        IReadOnlyList<JsonPathSegment> segments,
        JsonPathMatch start,
        JsonNode? root)
    {
        var current = new List<JsonPathMatch> { start };
        foreach (var segment in segments)
        {
            var next = new List<JsonPathMatch>();
            foreach (var input in current)
            {
                if (segment.IsDescendant)
                {
                    VisitDescendants(input, segment.Selectors, next, root);
                }
                else
                {
                    ApplySelectors(input, segment.Selectors, next, root);
                }
            }

            current = next;
        }

        return current;
    }

    /// <summary>
    /// Whether two JSON values are equal: numbers by value, strings by their characters, arrays element by
    /// element, objects member by member in any order.
    /// </summary>
    /// <param name="a">One value.</param>
    /// <param name="b">The other.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    public static bool DeepEquals(
        JsonNode? a,
        JsonNode? b)
    {
        var kindA = Kind(a);
        var kindB = Kind(b);
        if (kindA != kindB)
        {
            return false;
        }

        switch (kindA)
        {
            case JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False:
                return true;
            case JsonValueKind.Number:
                return Number((JsonValue)a!).CompareTo(Number((JsonValue)b!)) == 0;
            case JsonValueKind.String:
                return string.Equals(((JsonValue)a!).GetValue<string>(), ((JsonValue)b!).GetValue<string>(), StringComparison.Ordinal);
            case JsonValueKind.Array:
            {
                var x = (JsonArray)a!;
                var y = (JsonArray)b!;
                if (x.Count != y.Count)
                {
                    return false;
                }

                for (var i = 0; i < x.Count; i++)
                {
                    if (!DeepEquals(x[i], y[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            case JsonValueKind.Object:
            {
                var x = (JsonObject)a!;
                var y = (JsonObject)b!;
                if (x.Count != y.Count)
                {
                    return false;
                }

                foreach (var member in x)
                {
                    if (!y.TryGetPropertyValue(member.Key, out var other) || !DeepEquals(member.Value, other))
                    {
                        return false;
                    }
                }

                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// The JSON kind of a node; <see cref="JsonValueKind.Null"/> for <see langword="null"/>.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>Its kind.</returns>
    public static JsonValueKind Kind(JsonNode? node)
        => node switch
        {
            null => JsonValueKind.Null,
            JsonObject => JsonValueKind.Object,
            JsonArray => JsonValueKind.Array,
            JsonValue value => value.GetValueKind(),
            _ => JsonValueKind.Undefined,
        };

    // The input node, then each of its descendants (an array's elements in order), each before its own descendants
    private static void VisitDescendants(
        JsonPathMatch input,
        IReadOnlyList<JsonPathSelector> selectors,
        List<JsonPathMatch> output,
        JsonNode? root)
    {
        ApplySelectors(input, selectors, output, root);
        foreach (var child in Children(input))
        {
            VisitDescendants(child, selectors, output, root);
        }
    }

    private static void ApplySelectors(
        JsonPathMatch input,
        IReadOnlyList<JsonPathSelector> selectors,
        List<JsonPathMatch> output,
        JsonNode? root)
    {
        foreach (var selector in selectors)
        {
            switch (selector)
            {
                case NameSelector name:
                    if (input.Value is JsonObject obj && obj.TryGetPropertyValue(name.Name, out var member))
                    {
                        output.Add(new JsonPathMatch(member, input, name.Name, -1));
                    }

                    break;
                case WildcardSelector:
                    output.AddRange(Children(input));
                    break;
                case IndexSelector index:
                    if (input.Value is JsonArray array)
                    {
                        var i = index.Index < 0 ? array.Count + index.Index : index.Index;
                        if (i >= 0 && i < array.Count)
                        {
                            output.Add(new JsonPathMatch(array[(int)i], input, null, (int)i));
                        }
                    }

                    break;
                case SliceSelector slice:
                    if (input.Value is JsonArray sliced)
                    {
                        foreach (var i in SliceIndexes(slice, sliced.Count))
                        {
                            output.Add(new JsonPathMatch(sliced[i], input, null, i));
                        }
                    }

                    break;
                case FilterSelector filter:
                    foreach (var child in Children(input))
                    {
                        if (IsTrue(filter.Expression, child.Value, root))
                        {
                            output.Add(child);
                        }
                    }

                    break;
            }
        }
    }

    private static IEnumerable<JsonPathMatch> Children(JsonPathMatch input)
    {
        switch (input.Value)
        {
            case JsonObject obj:
                foreach (var member in obj)
                {
                    yield return new JsonPathMatch(member.Value, input, member.Key, -1);
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    yield return new JsonPathMatch(array[i], input, null, i);
                }

                break;
        }
    }

    // RFC 9535 section 2.3.4.2.2
    private static IEnumerable<int> SliceIndexes(
        SliceSelector slice,
        int length)
    {
        var step = slice.Step ?? 1;
        if (step == 0)
        {
            yield break;
        }

        long Normalize(long i)
            => i >= 0 ? i : length + i;

        if (step > 0)
        {
            var lower = System.Math.Min(System.Math.Max(Normalize(slice.Start ?? 0), 0), length);
            var upper = System.Math.Min(System.Math.Max(Normalize(slice.End ?? length), 0), length);
            for (var i = lower; i < upper; i += step)
            {
                yield return (int)i;
            }
        }
        else
        {
            var upper = System.Math.Min(System.Math.Max(Normalize(slice.Start ?? length - 1), -1), length - 1);
            var lower = System.Math.Min(System.Math.Max(Normalize(slice.End ?? -length - 1), -1), length - 1);
            for (var i = upper; lower < i; i += step)
            {
                yield return (int)i;
            }
        }
    }

    private static bool IsTrue(
        JsonPathExpression expression,
        JsonNode? current,
        JsonNode? root)
        => expression switch
        {
            OrExpression or => IsTrue(or.Left, current, root) || IsTrue(or.Right, current, root),
            AndExpression and => IsTrue(and.Left, current, root) && IsTrue(and.Right, current, root),
            NotExpression not => !IsTrue(not.Operand, current, root),
            ComparisonExpression comparison => Compare(comparison, current, root),
            TestExpression { Operand: QueryExpression query } => Nodes(query, current, root).Count > 0,
            TestExpression { Operand: FunctionExpression function } => FunctionIsTrue(function, current, root),
            _ => false,
        };

    private static List<JsonPathMatch> Nodes(
        QueryExpression query,
        JsonNode? current,
        JsonNode? root)
        => Select(query.Segments, new JsonPathMatch(query.IsRelative ? current : root, parent: null, memberName: null, arrayIndex: -1), root);

    // A value, or nothing (no node)
    private static (bool IsNothing, JsonNode? Value) Value(
        JsonPathExpression expression,
        JsonNode? current,
        JsonNode? root)
    {
        switch (expression)
        {
            case LiteralExpression literal:
                return (false, literal.Value);
            case QueryExpression query:
            {
                var nodes = Nodes(query, current, root);
                return nodes.Count == 1 ? (false, nodes[0].Value) : (true, null);
            }

            case FunctionExpression function:
                return FunctionValue(function, current, root);
            default:
                return (true, null);
        }
    }

    private static (bool IsNothing, JsonNode? Value) FunctionValue(
        FunctionExpression function,
        JsonNode? current,
        JsonNode? root)
    {
        switch (function.Name)
        {
            case "length":
            {
                var (isNothing, value) = Value(function.Arguments[0], current, root);
                if (isNothing)
                {
                    return (true, null);
                }

                return value switch
                {
                    JsonObject obj => (false, JsonValue.Create(obj.Count)),
                    JsonArray array => (false, JsonValue.Create(array.Count)),
                    JsonValue scalar when Kind(scalar) == JsonValueKind.String => (false, JsonValue.Create(CodePointCount(scalar.GetValue<string>()))),
                    _ => (true, null),
                };
            }

            case "count":
                return (false, JsonValue.Create(Nodes((QueryExpression)function.Arguments[0], current, root).Count));
            case "value":
            {
                var nodes = Nodes((QueryExpression)function.Arguments[0], current, root);
                return nodes.Count == 1 ? (false, nodes[0].Value) : (true, null);
            }

            default:
                return (true, null);
        }
    }

    // match() and search() (I-Regexp, RFC 9485): a full match, or a match anywhere
    private static bool FunctionIsTrue(
        FunctionExpression function,
        JsonNode? current,
        JsonNode? root)
    {
        var (subjectIsNothing, subject) = Value(function.Arguments[0], current, root);
        var (patternIsNothing, pattern) = Value(function.Arguments[1], current, root);
        if (subjectIsNothing || patternIsNothing ||
            subject is not JsonValue subjectValue || Kind(subjectValue) != JsonValueKind.String ||
            pattern is not JsonValue patternValue || Kind(patternValue) != JsonValueKind.String)
        {
            return false;
        }

        var regex = RegexFor(patternValue.GetValue<string>(), fullMatch: function.Name == "match");
        if (regex is null)
        {
            return false;
        }

        try
        {
            return regex.IsMatch(subjectValue.GetValue<string>());
        }
        catch (RegexMatchTimeoutException ex)
        {
            // A silent false would select the wrong nodes: the overlay is wrong, and says so
            throw new JsonPathException($"{function.Name}(): the regular expression '{patternValue.GetValue<string>()}' timed out after a second on a value of {subjectValue.GetValue<string>().Length} characters.", ex);
        }
    }

    private static Regex? RegexFor(
        string pattern,
        bool fullMatch)
    {
        var key = (fullMatch ? "m:" : "s:") + pattern;
        lock (RegexCache)
        {
            if (RegexCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            Regex? regex;
            try
            {
                var translated = TranslateIRegexp(pattern);
                regex = translated is null
                    ? null
                    : new Regex(fullMatch ? @"\A(?:" + translated + @")\z" : translated, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException)
            {
                regex = null;
            }

            // A build sees a few dozen patterns; an IDE session lives long
            if (RegexCache.Count >= MaxCachedRegexes)
            {
                RegexCache.Clear();
            }

            RegexCache[key] = regex;
            return regex;
        }
    }

    // I-Regexp to .NET: '.' is any character but a line break (a surrogate pair is one character), and '^' / '$'
    // anchor at the start and end of the whole string. Constructs I-Regexp does not have make the regex invalid.
    private static string? TranslateIRegexp(string pattern)
    {
        const string AnyCharacter = @"(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^\n\r])";
        var sb = new StringBuilder(pattern.Length + 16);
        var inClass = false;
        var i = -1;
        while (++i < pattern.Length)
        {
            var c = pattern[i];
            if (c == '\\')
            {
                if (i + 1 >= pattern.Length)
                {
                    return null;
                }

                var next = pattern[i + 1];

                // I-Regexp escapes: the metacharacters, n r t, and \p{..} / \P{..} categories
                if (next is 'p' or 'P')
                {
                    var close = pattern.IndexOf('}', i);
                    if (i + 2 >= pattern.Length || pattern[i + 2] != '{' || close < 0)
                    {
                        return null;
                    }

                    sb.Append(pattern, i, close - i + 1);
                    i = close;
                    continue;
                }

                if ("\\|.-^?*+{}()[]nrt".IndexOf(next) < 0)
                {
                    return null;
                }

                sb.Append(c).Append(next);
                i++;
                continue;
            }

            if (inClass)
            {
                if (c == ']')
                {
                    inClass = false;
                }

                sb.Append(c);
                continue;
            }

            switch (c)
            {
                case '[':
                    inClass = true;
                    sb.Append(c);
                    if (i + 1 < pattern.Length && pattern[i + 1] == '^')
                    {
                        sb.Append('^');
                        i++;
                    }

                    if (i + 1 < pattern.Length && pattern[i + 1] == ']')
                    {
                        sb.Append(@"\]");
                        i++;
                    }

                    break;
                case '.':
                    sb.Append(AnyCharacter);
                    break;
                case '^':
                    sb.Append(@"\A");
                    break;
                case '$':
                    sb.Append(@"\z");
                    break;
                case '(':
                    // No groups beyond plain ones: (?...) is not I-Regexp
                    if (i + 1 < pattern.Length && pattern[i + 1] == '?')
                    {
                        return null;
                    }

                    sb.Append(c);
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return inClass ? null : sb.ToString();
    }

    private static bool Compare(
        ComparisonExpression comparison,
        JsonNode? current,
        JsonNode? root)
    {
        var left = Value(comparison.Left, current, root);
        var right = Value(comparison.Right, current, root);
        return comparison.Operator switch
        {
            "==" => AreEqual(left, right),
            "!=" => !AreEqual(left, right),
            "<" => IsLess(left, right),
            "<=" => IsLess(left, right) || AreEqual(left, right),
            ">" => IsGreater(left, right),
            ">=" => IsGreater(left, right) || AreEqual(left, right),
            _ => false,
        };
    }

    private static bool AreEqual(
        (bool IsNothing, JsonNode? Value) left,
        (bool IsNothing, JsonNode? Value) right)
    {
        if (left.IsNothing || right.IsNothing)
        {
            return left.IsNothing && right.IsNothing;
        }

        return DeepEquals(left.Value, right.Value);
    }

    private static bool IsGreater(
        (bool IsNothing, JsonNode? Value) left,
        (bool IsNothing, JsonNode? Value) right)
        => IsLess(right, left);

    private static bool IsLess(
        (bool IsNothing, JsonNode? Value) first,
        (bool IsNothing, JsonNode? Value) second)
    {
        if (first.IsNothing || second.IsNothing || first.Value is not JsonValue a || second.Value is not JsonValue b)
        {
            return false;
        }

        var kindA = Kind(a);
        var kindB = Kind(b);
        if (kindA == JsonValueKind.Number && kindB == JsonValueKind.Number)
        {
            return Number(a) < Number(b);
        }

        return kindA == JsonValueKind.String && kindB == JsonValueKind.String &&
               CompareCodePoints(a.GetValue<string>(), b.GetValue<string>()) < 0;
    }

    private static double Number(JsonValue value)
        => double.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static int CodePointCount(string s)
    {
        var count = 0;
        var i = -1;
        while (++i < s.Length)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                i++;
            }

            count++;
        }

        return count;
    }

    // Strings compare by their Unicode scalar values (UTF-16 ordinal order differs above U+FFFF)
    private static int CompareCodePoints(
        string a,
        string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            var x = NextCodePoint(a, ref i);
            var y = NextCodePoint(b, ref j);
            if (x != y)
            {
                return x < y ? -1 : 1;
            }
        }

        return (a.Length - i).CompareTo(b.Length - j);
    }

    private static int NextCodePoint(
        string s,
        ref int index)
    {
        int value;
        if (char.IsHighSurrogate(s[index]) && index + 1 < s.Length && char.IsLowSurrogate(s[index + 1]))
        {
            value = char.ConvertToUtf32(s[index], s[index + 1]);
            index += 2;
        }
        else
        {
            value = s[index];
            index++;
        }

        return value;
    }
}