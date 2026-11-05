namespace Atc.Rest.Api.SourceGenerator.Tests.Helpers;

/// <summary>
/// The generator's own patterns run over the code it writes and the specification's text with no time limit. A limit
/// is a wall-clock limit: on a busy build machine (parallel builds, a starved CI runner) a scan of a large file can
/// pass it, and the timeout ends the generation part way - the build then sees some of the files and an error.
/// These patterns have no nested repetition, so their time grows with the text alone.
/// </summary>
public sealed class GeneratorRegexTimeoutTests
{
    // Patterns that keep a limit, and why: each one's timeout is caught and answered without ending the generation
    private static readonly HashSet<string> KeepTheirLimit = new(StringComparer.Ordinal)
    {
        // Runs over a handler file the user wrote; a timeout leaves that file as it is
        "Atc.Rest.Api.SourceGenerator.ApiServerDomainGenerator.ExecuteAsyncSignatureRegex",
    };

    public static TheoryData<string> GeneratorAssemblies()
        => new()
        {
            typeof(ApiServerGenerator).Assembly.GetName().Name!,
            typeof(Atc.Rest.Api.Generator.Helpers.ImplicitUsingsHelper).Assembly.GetName().Name!,
            typeof(OpenApiDocumentHelper).Assembly.GetName().Name!,
        };

    [Theory]
    [MemberData(nameof(GeneratorAssemblies))]
    public void TheGeneratorsOwnPatterns_HaveNoTimeLimit(string assemblyName)
    {
        var assembly = Assembly.Load(new AssemblyName(assemblyName));

        var limited = new List<string>();
        foreach (var type in assembly.GetTypes().Where(t => !t.ContainsGenericParameters))
        {
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                var name = $"{type.FullName}.{field.Name}";
                foreach (var (key, regex) in RegexesOf(field))
                {
                    if (regex.MatchTimeout != Regex.InfiniteMatchTimeout &&
                        !KeepTheirLimit.Contains(name))
                    {
                        limited.Add($"{name}{key} ({regex.MatchTimeout})");
                    }
                }
            }
        }

        Assert.True(
            limited.Count == 0,
            $"{limited.Count} pattern(s) with a time limit, which a busy machine can pass:\n  {string.Join("\n  ", limited)}");
    }

    [Fact]
    public void AddImplicitUsings_OfALargeFile_AddsTheUsingsItsTypesNeed()
    {
        // A file the size of a large generated one, naming a List only at its end
        var body = new StringBuilder();
        body.Append("namespace Sample;\n\npublic static class Big\n{\n");
        for (var i = 0; i < 20_000; i++)
        {
            body.Append("    public static int Value").Append(i).Append(" => ").Append(i).Append(";\n");
        }

        body.Append("    public static object Items => new List<int>();\n}\n");

        var content = Atc.Rest.Api.Generator.Helpers.ImplicitUsingsHelper.AddImplicitUsings(body.ToString());

        Assert.Contains("using System.Collections.Generic;", content, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Net.Http;", content, StringComparison.Ordinal);
    }

    private static IEnumerable<(string Key, Regex Regex)> RegexesOf(
        FieldInfo field)
    {
        if (field.FieldType == typeof(Regex))
        {
            if (field.GetValue(null) is Regex regex)
            {
                yield return (string.Empty, regex);
            }

            yield break;
        }

        if (typeof(System.Collections.IDictionary).IsAssignableFrom(field.FieldType) &&
            field.GetValue(null) is System.Collections.IDictionary dictionary)
        {
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                if (entry.Value is Regex regex)
                {
                    yield return ($"[{entry.Key}]", regex);
                }
            }
        }
    }
}