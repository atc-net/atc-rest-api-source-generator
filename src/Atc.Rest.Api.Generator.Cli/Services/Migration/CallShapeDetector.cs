namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Best-effort detection of code written against the old generator's shapes that the source generator's do not fit:
/// a result factory called with an argument it no longer takes (a success the specification declares without content)
/// or with a string where it takes the declared error model, and a member access of a property the old generator kept
/// in the specification's casing (the source generator names it in PascalCase). Reported, not changed: a name can be
/// another type's member.
/// </summary>
internal static class CallShapeDetector
{
    private static readonly string[] FrameworkProblemTypes = ["ProblemDetails", "ValidationProblemDetails"];

    /// <summary>
    /// Detects the call shapes in <paramref name="files"/> that the specification's generated code does not fit.
    /// </summary>
    /// <returns>One finding per occurrence: <c>{path}:{line}: {what} - {why}</c>.</returns>
    public static List<string> Detect(
        OpenApiDocument document,
        IEnumerable<(string Path, string Content)> files)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(files);

        var factoryRules = FactoryRules(document);
        var renamedProperties = RenamedProperties(document);
        var findings = new List<string>();

        foreach (var (path, content) in files)
        {
            foreach (var (resultType, rules) in factoryRules)
            {
                if (!content.Contains(resultType + ".", StringComparison.Ordinal))
                {
                    continue;
                }

                var calls = new Regex(
                    $@"\b{Regex.Escape(resultType)}\.(?<factory>[A-Z]\w*)\((?<args>[^()]*(?:\([^()]*\)[^()]*)*)\)",
                    RegexOptions.ExplicitCapture,
                    TimeSpan.FromSeconds(1));
                foreach (Match call in calls.Matches(content))
                {
                    if (rules.TryGetValue(call.Groups["factory"].Value, out var rule) &&
                        rule.Mismatch(call.Groups["args"].Value.Trim()) is { } why)
                    {
                        findings.Add($"{path}:{LineOf(content, call.Index)}: {call.Value} - {why}");
                    }
                }
            }

            foreach (var (wireName, newName) in renamedProperties)
            {
                if (!content.Contains("." + wireName, StringComparison.Ordinal))
                {
                    continue;
                }

                var accesses = new Regex($@"\.{Regex.Escape(wireName)}\b(?!\s*\()", RegexOptions.None, TimeSpan.FromSeconds(1));
                foreach (Match access in accesses.Matches(content))
                {
                    findings.Add($"{path}:{LineOf(content, access.Index)}: .{wireName} → {newName} - the property is named in PascalCase now");
                }
            }
        }

        return findings;
    }

    private static Dictionary<string, Dictionary<string, FactoryRule>> FactoryRules(
        OpenApiDocument document)
    {
        var rules = new Dictionary<string, Dictionary<string, FactoryRule>>(StringComparer.Ordinal);
        foreach (var (path, pathItem) in document.Paths ?? new OpenApiPaths())
        {
            foreach (var (method, operation) in pathItem.Operations ?? new Dictionary<HttpMethod, OpenApiOperation>())
            {
                var resultType = ContractsNamespaceMap.OperationName(path, method.Method, operation) + "Result";
                var factories = new Dictionary<string, FactoryRule>(StringComparer.Ordinal);
                foreach (var (status, response) in operation.Responses ?? new OpenApiResponses())
                {
                    if (!int.TryParse(status, NumberStyles.None, CultureInfo.InvariantCulture, out var code))
                    {
                        continue;
                    }

                    var factory = HttpStatusCodeHelper.ToEnumName(code) is "OK" ? "Ok" : HttpStatusCodeHelper.ToEnumName(code);
                    var schema = response.Content?.Values.FirstOrDefault()?.Schema;
                    if (code is >= 200 and < 300 && response.Content is not { Count: > 0 })
                    {
                        factories[factory] = new FactoryRule(TakesNoArgument: true, ErrorModel: null, status);
                    }
                    else if (code >= 400 &&
                             schema is OpenApiSchemaReference { Reference.Id: { Length: > 0 } model } &&
                             !FrameworkProblemTypes.Contains(model, StringComparer.Ordinal) &&
                             schema.Type?.HasFlag(JsonSchemaType.String) != true)
                    {
                        factories[factory] = new FactoryRule(TakesNoArgument: false, ErrorModel: model, status);
                    }
                }

                if (factories.Count > 0)
                {
                    rules[resultType] = factories;
                }
            }
        }

        return rules;
    }

    private static List<(string WireName, string NewName)> RenamedProperties(
        OpenApiDocument document)
        => (document.Components?.Schemas?.Values ?? Enumerable.Empty<IOpenApiSchema>())
            .SelectMany(schema => schema.Properties?.Keys ?? Enumerable.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .Where(key => key.Length > 1 && char.IsLower(key[0]))
            .Select(key => (WireName: key, NewName: key.ToPascalCaseForDotNet()))
            .Where(names => !string.Equals(names.WireName, names.NewName, StringComparison.Ordinal))
            .ToList();

    private static int LineOf(
        string content,
        int index)
        => content.AsSpan(0, index).Count('\n') + 1;

    private sealed record FactoryRule(
        bool TakesNoArgument,
        string? ErrorModel,
        string Status)
    {
        public string? Mismatch(string arguments)
        {
            if (TakesNoArgument)
            {
                return arguments.Length > 0
                    ? $"it takes no argument now (the specification declares {Status} without content)"
                    : null;
            }

            // A string where the factory takes the declared model: a literal, an interpolation, an exception message
            var isString = arguments.StartsWith('"') ||
                           arguments.StartsWith("$\"", StringComparison.Ordinal) ||
                           arguments.EndsWith(".Message", StringComparison.Ordinal) ||
                           arguments.EndsWith(".ToString()", StringComparison.Ordinal);
            return isString
                ? $"it takes a {ErrorModel} now (the specification declares {Status} with that model), not a string"
                : null;
        }
    }
}