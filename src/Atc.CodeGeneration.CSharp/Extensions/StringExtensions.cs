// ReSharper disable once CheckNamespace
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable GrammarMistakeInComment
namespace System;

[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S1144:Remove the unused private method", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "S3928:The parameter name 'schema'", Justification = "OK - CLang14 - extension")]
public static class StringExtensions
{
    /// <param name="value">The string to convert.</param>
    extension(string value)
    {
        /// <summary>
        /// Converts the string to PascalCase format suitable for .NET identifiers.
        /// Handles various input formats: camelCase, kebab-case, snake_case, dot.separated,
        /// UPPER_SNAKE_CASE, and mixed formats.
        /// </summary>
        /// <returns>The string in PascalCase format (e.g., "MyPetStore").</returns>
        /// <remarks>
        /// Examples:
        /// - "myPetStore" → "MyPetStore" (camelCase)
        /// - "my-pet-store" → "MyPetStore" (kebab-case)
        /// - "my.pet-store" → "MyPetStore" (mixed separators)
        /// - "my-PET.store" → "MyPetStore" (mixed with ALL CAPS)
        /// - "XMLParser" → "XmlParser" (acronym handling)
        /// </remarks>
        public string ToPascalCaseForDotNet()
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

                // Capitalize first letter, lowercase the rest
                result.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                {
                    var lowerInvariant = word
                        .Substring(1)
                        .ToLowerInvariant();
                    result.Append(lowerInvariant);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Converts a header parameter name to a valid C# property name.
        /// Strips the "x-" prefix (commonly used for custom HTTP headers) before PascalCase conversion.
        /// </summary>
        /// <returns>The property name (e.g., "x-continuation" → "Continuation").</returns>
        /// <remarks>
        /// Examples:
        /// - "x-continuation" → "Continuation"
        /// - "x-correlation-id" → "CorrelationId"
        /// - "Content-Type" → "ContentType" (no x- prefix, stays the same)
        /// </remarks>
        public string ToHeaderPropertyName()
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var name = value;

            // Strip "x-" prefix (case-insensitive) - common for custom HTTP headers
            if (name.StartsWith("x-", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(2);
            }

            return name.ToPascalCaseForDotNet();
        }

        /// <summary>
        /// Converts the string to Pascal case format (each word capitalized) using custom separator characters.
        /// </summary>
        /// <param name="separators">An array of characters to use as word separators.</param>
        /// <param name="removeSeparators">If set to <see langword="true" />, removes all separator characters from the result.</param>
        /// <returns>The string in Pascal case format.</returns>
        public string ToPascalCase(
            char[]? separators = null,
            bool removeSeparators = false)
        {
            if (string.IsNullOrEmpty(value) || separators is null)
            {
                return value;
            }

            if (separators.Length <= 0)
            {
                return value
                           .Substring(0, 1)
                           .ToUpperInvariant() +
                       value
                           .Substring(1)
                           .ToLowerInvariant();
            }

            if (separators.Length == 1 && value.IndexOfAny(separators) == -1)
            {
                return value
                           .Substring(0, 1)
                           .ToUpperInvariant() +
                       value
                           .Substring(1)
                           .ToLowerInvariant();
            }

            var strArray = value.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < strArray.Length; i++)
            {
                var tmp = strArray[i]
                              .Substring(0, 1)
                              .ToUpperInvariant() +
                          strArray[i]
                              .Substring(1)
                              .ToLowerInvariant();

                for (var j = 1; j < strArray[i].Length - 1; j++)
                {
                    var c1 = strArray[i][j - 1];
                    var c2 = strArray[i][j];
                    var c3 = strArray[i][j + 1];
                    if (char.IsLower(c1) && char.IsUpper(c2) && char.IsLower(c3))
                    {
                        tmp = tmp.ReplaceAt(j, c2);
                    }
                }

                strArray[i] = tmp;
            }

            var sb = new StringBuilder();
            for (var i = 0; i < strArray.Length; i++)
            {
                sb.Append(strArray[i]);
                if (removeSeparators)
                {
                    continue;
                }

                if (i != strArray.Length - 1)
                {
                    sb.Append(value.Substring(sb.Length, 1));
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Splits the specified text into non-empty lines separated by \r\n, \n, or \r.
        /// </summary>
        /// <returns>
        /// An array whose elements contain the non-whitespace substrings from this instance
        /// that are delimited by newline characters.
        /// </returns>
        public string[] SplitIntoLines()
            => string.IsNullOrEmpty(value)
                ? []
                : value
                    .Split(["\r\n", "\n", "\r"], StringSplitOptions.None)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

        /// <summary>
        /// Splits the specified text into lines separated by \r\n, \n, or \r, preserving empty lines.
        /// Use this when you need to maintain blank line spacing (e.g., between methods in generated code).
        /// </summary>
        /// <returns>
        /// An array whose elements contain all substrings from this instance
        /// that are delimited by newline characters, including empty lines.
        /// </returns>
        public string[] SplitIntoLinesPreserveEmpty()
            => string.IsNullOrEmpty(value)
                ? []
                : value.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        /// <summary>
        /// Normalizes generated source code content by removing trailing whitespace.
        /// Use this method before writing to source files or adding to source generation context.
        /// </summary>
        /// <returns>The content with trailing whitespace removed.</returns>
        public string NormalizeForSourceOutput()
            => string.IsNullOrEmpty(value)
                ? value
                : value.TrimEnd();

        /// <summary>
        /// Converts a singular noun to its plural form using basic English pluralization rules.
        /// </summary>
        /// <returns>The pluralized form of the word.</returns>
        public string Pluralize()
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            // If already plural, return as-is
            if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith("es", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            // Apply common pluralization rules
            if (value.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
                value.Length > 1 &&
                !IsVowel(value[value.Length - 2]))
            {
                // words ending in consonant + y: category -> categories
                return value.Substring(0, value.Length - 1) + "ies";
            }

            if (value.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith("z", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
                value.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
            {
                // words ending in s, x, z, ch, sh: box -> boxes
                return value + "es";
            }

            if (value.EndsWith("f", StringComparison.OrdinalIgnoreCase))
            {
                // words ending in f: leaf -> leaves
                return value.Substring(0, value.Length - 1) + "ves";
            }

            if (value.EndsWith("fe", StringComparison.OrdinalIgnoreCase))
            {
                // words ending in fe: knife -> knives
                return value.Substring(0, value.Length - 2) + "ves";
            }

            // Default: just add 's'
            return value + "s";
        }

        /// <summary>
        /// Sanitizes a type name for use in a file name by removing invalid characters.
        /// Generic types like "PaginatedResult&lt;T&gt;" become "PaginatedResultT".
        /// </summary>
        /// <returns>The sanitized type name suitable for use in file names.</returns>
        public string SanitizeForFileName()
            => string.IsNullOrEmpty(value)
                ? value
                : value
                    .Replace("<", string.Empty)
                    .Replace(">", string.Empty)
                    .Replace(",", string.Empty)
                    .Replace(" ", string.Empty);

        /// <summary>
        /// Checks if a character is a vowel.
        /// </summary>
        private static bool IsVowel(char c)
            => c is 'a' or 'e' or 'i' or 'o' or 'u' or 'A' or 'E' or 'I' or 'O' or 'U';

        /// <summary>
        /// Splits a string into words based on separators and case transitions.
        /// </summary>
        private static List<string> SplitIntoWords(string input)
        {
            var words = new List<string>();
            var currentWord = new StringBuilder();

            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];

                // Check if current char is a separator
                if (c is '-' or '.' or '_' or ' ')
                {
                    AddWordIfNotEmpty(words, currentWord);
                    continue;
                }

                // Check for case transition (lowercase to uppercase = new word boundary)
                if (i > 0 && char.IsUpper(c))
                {
                    var prevChar = input[i - 1];

                    // Word boundary if:
                    // 1. Previous char was lowercase (e.g., "myPet" - 'P' starts new word)
                    // 2. Previous char was a digit (e.g., "Api31Features" - 'F' starts new word)
                    // 3. Previous char was uppercase AND next char is lowercase (acronym ending, e.g., "XMLParser")
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

            // Add the last word
            AddWordIfNotEmpty(words, currentWord);

            return words;
        }

        /// <summary>
        /// Adds the current word to the list if it's not empty, then clears the builder.
        /// </summary>
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
