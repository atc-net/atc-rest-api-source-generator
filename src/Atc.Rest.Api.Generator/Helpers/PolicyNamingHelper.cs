namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Shared naming logic for policy-name constants. Used by the rate-limit, resilience,
/// output-cache and hybrid-cache policy extractors, which all derive a C# constant name
/// from a policy string the same way. (Security policies use a different scheme that
/// also handles ':' and '+' scope separators, so they keep their own conversion.)
/// </summary>
public static class PolicyNamingHelper
{
    /// <summary>
    /// Converts a policy name to a valid C# constant identifier by splitting on
    /// '-', '_', ':' and ' ' separators and PascalCasing each part.
    /// </summary>
    /// <param name="policyName">The policy name (e.g., "global", "create-user").</param>
    /// <returns>A valid C# identifier (e.g., "Global", "CreateUser").</returns>
    public static string ToConstantName(string policyName)
    {
        var parts = policyName.Split(['-', '_', ':', ' '], StringSplitOptions.RemoveEmptyEntries);

        var result = new StringBuilder();

        foreach (var part in parts)
        {
            result.Append(part.ToPascalCaseForDotNet());
        }

        return result.ToString();
    }
}