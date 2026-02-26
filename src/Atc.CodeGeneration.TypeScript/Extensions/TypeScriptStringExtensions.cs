// ReSharper disable once CheckNamespace
namespace System;

[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
public static class TypeScriptStringExtensions
{
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
                    .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

        public string[] SplitIntoLinesPreserveEmpty()
            => string.IsNullOrEmpty(value)
                ? Array.Empty<string>()
                : value.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        public string NormalizeForSourceOutput()
            => string.IsNullOrEmpty(value)
                ? value
                : value.TrimEnd();

        public string EnsureEnvironmentNewLines()
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", Environment.NewLine);
        }

        public string ReplaceAt(
            int index,
            char newChar)
        {
            if (string.IsNullOrEmpty(value) || index < 0 || index >= value.Length)
            {
                return value;
            }

            var chars = value.ToCharArray();
            chars[index] = newChar;
            return new string(chars);
        }

        public string EnsureEndsWithDot()
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.EndsWith(".", StringComparison.Ordinal)
                ? value
                : value + ".";
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
