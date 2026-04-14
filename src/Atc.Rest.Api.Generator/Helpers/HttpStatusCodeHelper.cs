namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Provides safe conversion of HTTP status code integers to valid C# identifier names.
/// Handles non-standard status codes (e.g., 499, 529) that are not defined in
/// <see cref="System.Net.HttpStatusCode"/> and would otherwise produce invalid identifiers.
/// </summary>
public static class HttpStatusCodeHelper
{
    /// <summary>
    /// Converts an HTTP status code integer to a valid C# identifier name.
    /// For defined codes (e.g., 200 → "OK", 404 → "NotFound"), returns the enum name.
    /// For undefined codes (e.g., 499, 529), returns "Status{code}" (e.g., "Status499").
    /// </summary>
    public static string ToEnumName(int statusCode)
    {
        var httpStatusCode = (System.Net.HttpStatusCode)statusCode;

        if (Enum.IsDefined(typeof(System.Net.HttpStatusCode), statusCode))
        {
            return httpStatusCode.ToString();
        }

        return $"Status{statusCode}";
    }
}