// ReSharper disable once CheckNamespace
namespace System;

[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
public static class TypeScriptStringExtensions
{
    /// <summary>
    /// TypeScript reserved words and strict-mode reserved words that would either be a
    /// syntax error or shadow built-ins when used as a top-level function name. Method
    /// names on a class can be any of these, so this list is intentionally tight — we
    /// only block names that break standalone function declarations.
    /// </summary>
    private static readonly HashSet<string> TypeScriptReservedWords = new(StringComparer.Ordinal)
    {
        "break", "case", "catch", "class", "const", "continue", "debugger", "default",
        "delete", "do", "else", "enum", "export", "extends", "false", "finally", "for",
        "function", "if", "import", "in", "instanceof", "new", "null", "return", "super",
        "switch", "this", "throw", "true", "try", "typeof", "var", "void", "while", "with",
        "as", "implements", "interface", "let", "package", "private", "protected",
        "public", "static", "yield", "any", "boolean", "constructor", "declare", "get",
        "module", "require", "number", "set", "string", "symbol", "type", "from", "of",
        "async", "await",
    };

    extension(string value)
    {
        public string ToCamelCase()
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var pascalCase = value.ToPascalCase();
            if (string.IsNullOrEmpty(pascalCase))
            {
                return pascalCase;
            }

            return char.ToLowerInvariant(pascalCase[0]) + pascalCase.Substring(1);
        }

        public string ToPascalCase()
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var result = new StringBuilder();
            var words = SplitIntoWords(value);

            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(word))
                {
                    continue;
                }

                result.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                {
                    result.Append(word.Substring(1).ToLowerInvariant());
                }
            }

            return result.ToString();
        }

        public string[] SplitIntoLines()
            => string.IsNullOrEmpty(value)
                ? Array.Empty<string>()
                : value
                    .Split(["\r\n", "\n", "\r"], StringSplitOptions.None)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

        public string[] SplitIntoLinesPreserveEmpty()
            => string.IsNullOrEmpty(value)
                ? Array.Empty<string>()
                : value.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        public string NormalizeForSourceOutput()
            => string.IsNullOrEmpty(value)
                ? value
                : value.TrimEnd();

        /// <summary>
        /// Returns a TypeScript-safe identifier. Apply this to operationIds and any other
        /// spec-supplied names that become standalone function or top-level binding names:
        /// <list type="bullet">
        ///   <item>Identifiers starting with a digit are prefixed with <c>_</c>.</item>
        ///   <item>Identifiers that match a TypeScript reserved word are prefixed with <c>_</c>.</item>
        ///   <item>Already-safe identifiers pass through unchanged.</item>
        /// </list>
        /// Whitespace / hyphen / dot splitting is intentionally NOT done here — call
        /// <c>ToCamelCase()</c> or <c>ToPascalCase()</c> first if the input has separators.
        /// </summary>
        public string ToTypeScriptIdentifier()
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var result = value;

            if (char.IsDigit(result[0]))
            {
                result = "_" + result;
            }

            return TypeScriptReservedWords.Contains(result)
                ? "_" + result
                : result;
        }

        private static List<string> SplitIntoWords(string input)
        {
            var words = new List<string>();
            var currentWord = new StringBuilder();

            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];

                if (c == '-' || c == '.' || c == '_' || c == ' ')
                {
                    AddWordIfNotEmpty(words, currentWord);
                    continue;
                }

                if (i > 0 && char.IsUpper(c))
                {
                    var prevChar = input[i - 1];
                    var isWordBoundary = char.IsLower(prevChar) ||
                                         char.IsDigit(prevChar) ||
                                         (char.IsUpper(prevChar) &&
                                          i + 1 < input.Length &&
                                          char.IsLower(input[i + 1]));

                    if (isWordBoundary)
                    {
                        AddWordIfNotEmpty(words, currentWord);
                    }
                }

                currentWord.Append(c);
            }

            AddWordIfNotEmpty(words, currentWord);

            return words;
        }

        private static void AddWordIfNotEmpty(
            List<string> words,
            StringBuilder currentWord)
        {
            if (currentWord.Length <= 0)
            {
                return;
            }

            words.Add(currentWord.ToString());
            currentWord.Clear();
        }
    }
}
