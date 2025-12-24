// ReSharper disable ConvertIfStatementToReturnStatement
namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Helper class for validating casing styles in identifiers.
/// Provides methods for checking camelCase, PascalCase, kebab-case, snake_case, and UPPER_SNAKE_CASE.
/// </summary>
public static class CasingHelper
{
    /// <summary>
    /// Determines whether the specified value is valid camelCase.
    /// camelCase: First character is lowercase, no separators (hyphens, underscores, spaces).
    /// Examples: "listPets", "getPetById", "createNewUser".
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is valid camelCase; otherwise, <see langword="false"/>.</returns>
    public static bool IsCamelCase(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // First character must be lowercase letter
        if (!char.IsLower(value![0]))
        {
            return false;
        }

        // No separators allowed (hyphens, underscores, spaces)
        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];

            // Allow letters (upper and lower) and digits
            if (char.IsLetterOrDigit(c))
            {
                continue;
            }

            // Disallow separators
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the specified value is valid kebab-case.
    /// kebab-case: All lowercase letters separated by hyphens.
    /// Examples: "list-pets", "get-pet-by-id", "create-new-user".
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is valid kebab-case; otherwise, <see langword="false"/>.</returns>
    public static bool IsKebabCase(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // First character must be lowercase letter
        if (!char.IsLower(value![0]))
        {
            return false;
        }

        var previousWasHyphen = false;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c == '-')
            {
                // No consecutive hyphens
                if (previousWasHyphen)
                {
                    return false;
                }

                // No trailing hyphen
                if (i == value.Length - 1)
                {
                    return false;
                }

                previousWasHyphen = true;
                continue;
            }

            previousWasHyphen = false;

            // Only lowercase letters and digits allowed (no uppercase)
            if (char.IsLower(c) || char.IsDigit(c))
            {
                continue;
            }

            // Uppercase letters are not allowed in kebab-case
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the specified value is valid PascalCase.
    /// PascalCase: First character is uppercase, no separators (hyphens, underscores, spaces).
    /// Examples: "ListPets", "GetPetById", "CreateNewUser".
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is valid PascalCase; otherwise, <see langword="false"/>.</returns>
    public static bool IsPascalCase(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // First character must be uppercase letter
        if (!char.IsUpper(value![0]))
        {
            return false;
        }

        // No separators allowed (hyphens, underscores, spaces)
        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];

            // Allow letters (upper and lower) and digits
            if (char.IsLetterOrDigit(c))
            {
                continue;
            }

            // Disallow separators
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the specified value is valid snake_case.
    /// snake_case: All lowercase letters separated by underscores.
    /// Examples: "list_pets", "get_pet_by_id", "create_new_user".
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is valid snake_case; otherwise, <see langword="false"/>.</returns>
    public static bool IsSnakeCase(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // First character must be lowercase letter
        if (!char.IsLower(value![0]))
        {
            return false;
        }

        var previousWasUnderscore = false;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c == '_')
            {
                // No consecutive underscores
                if (previousWasUnderscore)
                {
                    return false;
                }

                // No trailing underscore
                if (i == value.Length - 1)
                {
                    return false;
                }

                previousWasUnderscore = true;
                continue;
            }

            previousWasUnderscore = false;

            // Only lowercase letters and digits allowed (no uppercase)
            if (char.IsLower(c) || char.IsDigit(c))
            {
                continue;
            }

            // Uppercase letters are not allowed in snake_case
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the specified value is valid UPPER_SNAKE_CASE.
    /// UPPER_SNAKE_CASE: All uppercase letters separated by underscores.
    /// Examples: "LIST_PETS", "GET_PET_BY_ID", "CREATE_NEW_USER".
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is valid UPPER_SNAKE_CASE; otherwise, <see langword="false"/>.</returns>
    public static bool IsUpperSnakeCase(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // First character must be uppercase letter
        if (!char.IsUpper(value![0]))
        {
            return false;
        }

        var previousWasUnderscore = false;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c == '_')
            {
                // No consecutive underscores
                if (previousWasUnderscore)
                {
                    return false;
                }

                // No trailing underscore
                if (i == value.Length - 1)
                {
                    return false;
                }

                previousWasUnderscore = true;
                continue;
            }

            previousWasUnderscore = false;

            // Only uppercase letters and digits allowed (no lowercase)
            if (char.IsUpper(c) || char.IsDigit(c))
            {
                continue;
            }

            // Lowercase letters are not allowed in UPPER_SNAKE_CASE
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the specified operationId uses a valid casing style.
    /// Valid styles are: camelCase or kebab-case.
    /// </summary>
    /// <param name="operationId">The operationId to check.</param>
    /// <returns><see langword="true"/> if the operationId uses a valid casing style; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidOperationIdCasing(string? operationId)
    {
        if (string.IsNullOrEmpty(operationId))
        {
            return false;
        }

        return IsCamelCase(operationId) || IsKebabCase(operationId);
    }

    /// <summary>
    /// Gets the detected casing style for an operationId.
    /// </summary>
    /// <param name="operationId">The operationId to analyze.</param>
    /// <returns>A descriptive string of the detected casing style.</returns>
    public static string GetDetectedCasingStyle(string? operationId)
    {
        if (string.IsNullOrEmpty(operationId))
        {
            return "empty";
        }

        if (IsCamelCase(operationId))
        {
            return "camelCase";
        }

        if (IsKebabCase(operationId))
        {
            return "kebab-case";
        }

        if (IsPascalCase(operationId))
        {
            return "PascalCase";
        }

        if (IsSnakeCase(operationId))
        {
            return "snake_case";
        }

        if (IsUpperSnakeCase(operationId))
        {
            return "UPPER_SNAKE_CASE";
        }

        // Try to detect partial patterns
        if (operationId!.Contains("-"))
        {
            return "mixed (contains hyphens)";
        }

        if (operationId.Contains("_"))
        {
            return "mixed (contains underscores)";
        }

        return "mixed/unknown";
    }

    /// <summary>
    /// Gets a suggestion for converting the operationId to the expected casing style.
    /// </summary>
    /// <param name="operationId">The operationId to convert.</param>
    /// <returns>A suggested camelCase version of the operationId.</returns>
    public static string SuggestCamelCase(string operationId)
    {
        if (string.IsNullOrEmpty(operationId))
        {
            return operationId;
        }

        // If it's PascalCase, just lowercase the first character
        if (IsPascalCase(operationId))
        {
            return char.ToLowerInvariant(operationId[0]) + operationId.Substring(1);
        }

        // If it contains separators, convert to camelCase
        var result = new StringBuilder();
        var capitalizeNext = false;

        for (var i = 0; i < operationId.Length; i++)
        {
            var c = operationId[i];

            if (c is '-' or '_' or ' ')
            {
                capitalizeNext = true;
                continue;
            }

            if (i == 0)
            {
                result.Append(char.ToLowerInvariant(c));
            }
            else if (capitalizeNext)
            {
                result.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Gets a suggestion for converting a value to PascalCase.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A suggested PascalCase version of the value.</returns>
    public static string SuggestPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // If it's camelCase, just uppercase the first character
        if (IsCamelCase(value))
        {
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        // If it contains separators, convert to PascalCase
        var result = new StringBuilder();
        var capitalizeNext = true;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c is '-' or '_' or ' ')
            {
                capitalizeNext = true;
                continue;
            }

            if (capitalizeNext)
            {
                result.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                result.Append(char.ToLowerInvariant(c));
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Gets a suggestion for converting a value to kebab-case.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A suggested kebab-case version of the value.</returns>
    public static string SuggestKebabCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var result = new StringBuilder();

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            // Replace underscores and spaces with hyphens
            if (c is '_' or ' ')
            {
                if (result.Length > 0 && result[result.Length - 1] != '-')
                {
                    result.Append('-');
                }

                continue;
            }

            // Insert hyphen before uppercase letters (but not at start)
            if (char.IsUpper(c) && result.Length > 0 && result[result.Length - 1] != '-')
            {
                result.Append('-');
            }

            result.Append(char.ToLowerInvariant(c));
        }

        return result.ToString();
    }

    /// <summary>
    /// Converts a value to PascalCase format suitable for .NET identifiers.
    /// Handles various input formats: camelCase, kebab-case, snake_case, dot.separated,
    /// UPPER_SNAKE_CASE, and mixed formats.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A PascalCase version of the value (e.g., "MyPetStore").</returns>
    /// <remarks>
    /// Examples:
    /// - "myPetStore" → "MyPetStore" (camelCase)
    /// - "my-pet-store" → "MyPetStore" (kebab-case)
    /// - "my.pet-store" → "MyPetStore" (mixed separators)
    /// - "my-PET.store" → "MyPetStore" (mixed with ALL CAPS)
    /// - "XMLParser" → "XmlParser" (acronym handling)
    /// </remarks>
    public static string ToPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.ToPascalCaseForDotNet();
    }
}