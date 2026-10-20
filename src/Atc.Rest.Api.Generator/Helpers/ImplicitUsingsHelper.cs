namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Gives a generated C# file the usings the SDK's implicit usings would otherwise supply, so the file compiles in a
/// project with <c>ImplicitUsings</c> off as well. The emitters write code against the SDK's base set (<c>Task</c>,
/// <c>List&lt;T&gt;</c>, <c>HttpClient</c>, LINQ, ...); a file gets <c>System</c> and each other namespace whose types (or
/// LINQ's extension methods) it names - a model is not given <c>System.Net.Http</c>. Where the project has them on, the
/// file's own using of a namespace the project imports globally is harmless.
/// </summary>
public static class ImplicitUsingsHelper
{
    /// <summary>
    /// The namespaces <c>Microsoft.NET.Sdk</c> imports with <c>ImplicitUsings</c> on.
    /// </summary>
    private static readonly string[] ImplicitNamespaces =
    [
        "System",
        "System.Collections.Generic",
        "System.IO",
        "System.Linq",
        "System.Net.Http",
        "System.Threading",
        "System.Threading.Tasks",
    ];

    // The names that use a namespace: its types, or (System.Linq) its extension methods and query syntax
    private static readonly Dictionary<string, Regex> UsedBy = new(StringComparer.Ordinal)
    {
        ["System.Collections.Generic"] = Words(
            [
                "List", "IList", "Dictionary", "IDictionary", "IReadOnlyDictionary", "IReadOnlyList", "IReadOnlyCollection",
                "ICollection", "IEnumerable", "IEnumerator", "IAsyncEnumerable", "IAsyncEnumerator", "HashSet", "ISet",
                "IReadOnlySet", "KeyValuePair", "Queue", "Stack", "LinkedList", "SortedDictionary", "SortedSet", "SortedList",
                "IEqualityComparer", "EqualityComparer", "IComparer", "Comparer", "KeyNotFoundException", "CollectionExtensions",
                "GetValueOrDefault", "TryAdd", "AsReadOnly",
            ]),
        ["System.IO"] = Words(
            [
                "Stream", "MemoryStream", "FileStream", "BufferedStream", "File", "Path", "Directory", "TextReader", "TextWriter",
                "StreamReader", "StreamWriter", "StringReader", "StringWriter", "BinaryReader", "BinaryWriter", "IOException",
                "InvalidDataException", "EndOfStreamException", "SeekOrigin", "FileMode", "FileAccess", "FileShare",
            ]),
        ["System.Linq"] = new Regex(
            @"\.(Select|SelectMany|Where|Any|All|First|FirstOrDefault|Last|LastOrDefault|Single|SingleOrDefault|Count|LongCount|"
            + @"ToList|ToArray|ToDictionary|ToHashSet|ToLookup|OrderBy|OrderByDescending|ThenBy|ThenByDescending|GroupBy|Distinct|"
            + @"DistinctBy|Concat|Skip|SkipWhile|SkipLast|Take|TakeWhile|TakeLast|Sum|Max|Min|MaxBy|MinBy|Average|Aggregate|"
            + @"Contains|SequenceEqual|Zip|Reverse|Except|ExceptBy|Intersect|IntersectBy|Union|UnionBy|Cast|OfType|Prepend|Append|"
            + @"DefaultIfEmpty|ElementAt|ElementAtOrDefault|AsEnumerable|Chunk|Join|GroupJoin|Order|OrderDescending|Index|CountBy|AggregateBy)\s*[<(]|\bEnumerable\.|\bfrom\s+\w+\s+in\b",
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1)),
        ["System.Net.Http"] = Words(
            [
                "HttpClient", "IHttpClientFactory", "HttpMethod", "HttpRequestMessage", "HttpResponseMessage", "HttpContent",
                "StreamContent", "StringContent", "ByteArrayContent", "ReadOnlyMemoryContent", "FormUrlEncodedContent",
                "MultipartContent", "MultipartFormDataContent", "HttpRequestException", "HttpCompletionOption",
                "HttpMessageHandler", "DelegatingHandler", "HttpClientHandler", "SocketsHttpHandler", "HttpRequestOptions",
                "HttpRequestOptionsKey", "HttpVersionPolicy", "HttpIOException",
            ]),
        ["System.Threading"] = Words(
            [
                "CancellationToken", "CancellationTokenSource", "CancellationTokenRegistration", "Interlocked", "SemaphoreSlim",
                "Semaphore", "Thread", "Timeout", "Volatile", "Monitor", "Lock", "Mutex", "ManualResetEvent",
                "ManualResetEventSlim", "AutoResetEvent", "AsyncLocal", "ThreadLocal", "Timer", "LazyInitializer",
                "SynchronizationContext", "WaitHandle", "ReaderWriterLockSlim", "SpinWait",
            ]),
        ["System.Threading.Tasks"] = Words(
            [
                "Task", "ValueTask", "TaskCompletionSource", "Parallel", "TaskScheduler", "TaskCreationOptions",
                "TaskContinuationOptions", "TaskFactory", "TaskStatus", "TaskCanceledException", "ConfigureAwaitOptions",
            ]),
    };

    /// <summary>
    /// Adds a using for each implicit namespace the file does not import itself: before its first using directive, after
    /// its global usings (which must come first), else before the first line of code.
    /// </summary>
    /// <param name="content">The generated file.</param>
    /// <returns>The file with the usings, in its own line endings.</returns>
    public static string AddImplicitUsings(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        var lines = content.Split('\n');
        var imported = new HashSet<string>(
            lines
                .Select(l => l.Trim())
                .Select(ImportedNamespace)
                .Where(ns => ns is not null)
                .Select(ns => ns!),
            StringComparer.Ordinal);

        var missing = ImplicitNamespaces
            .Where(ns => !imported.Contains(ns) && IsUsed(ns, content))
            .ToList();
        if (missing.Count == 0)
        {
            return content;
        }

        var insertAt = 0;
        while (insertAt < lines.Length && IsHeader(lines[insertAt].Trim()))
        {
            insertAt++;
        }

        // Nothing but header (a file of global usings): no code that could need one
        if (insertAt == lines.Length)
        {
            return content;
        }

        // The file's own line ending, so the usings do not introduce a mixed one
        var eol = content.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r" : string.Empty;
        var block = missing.Select(ns => $"using {ns};{eol}").ToList();
        if (insertAt < lines.Length &&
            lines[insertAt].Trim().Length > 0 &&
            !IsUsingDirective(lines[insertAt].Trim()))
        {
            block.Add(eol);
        }

        return string.Join("\n", lines.Take(insertAt).Concat(block).Concat(lines.Skip(insertAt)));
    }

    // A namespace the file names a type of (or, for System.Linq, calls an extension method of). Naming one in a comment
    // or a string counts too: an unused using is harmless, a missing one does not compile. System is always used.
    private static bool IsUsed(
        string ns,
        string content)
        => !UsedBy.TryGetValue(ns, out var pattern) || pattern.IsMatch(content);

    private static Regex Words(string[] names)
        => new(
            @"\b(" + string.Join("|", names) + @")\b",
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));

    // Comments, preprocessor directives, blank lines and global usings come before the usings
    private static bool IsHeader(string line)
        => line.Length == 0 ||
           line.StartsWith("//", StringComparison.Ordinal) ||
           line.StartsWith("#", StringComparison.Ordinal) ||
           line.StartsWith("global using ", StringComparison.Ordinal);

    private static bool IsUsingDirective(string line)
        => line.StartsWith("using ", StringComparison.Ordinal) &&
           line.EndsWith(";", StringComparison.Ordinal) &&
           line.IndexOf('(') < 0;

    private static string? ImportedNamespace(string line)
    {
        var directive = line.StartsWith("global using ", StringComparison.Ordinal)
            ? line.Substring("global ".Length)
            : line;
        if (!IsUsingDirective(directive) ||
            directive.IndexOf('=') >= 0 ||
            directive.StartsWith("using static ", StringComparison.Ordinal))
        {
            return null;
        }

        var name = directive.Substring("using ".Length, directive.Length - "using ".Length - 1).Trim();
        return name.StartsWith("global::", StringComparison.Ordinal)
            ? name.Substring("global::".Length)
            : name;
    }
}