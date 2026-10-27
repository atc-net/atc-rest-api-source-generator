namespace Atc.Rest.Api.Generator.Tests;

/// <summary>
/// Marks a test class or method as covering a rule (<c>ATC_API_...</c>): the rule-coverage check and the test-suite
/// report read it to show which tests stand behind each rule.
/// </summary>
/// <param name="ruleId">The rule's id.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RuleTestAttribute(string ruleId) : Attribute
{
    /// <summary>
    /// Gets the rule's id.
    /// </summary>
    public string RuleId { get; } = ruleId;
}