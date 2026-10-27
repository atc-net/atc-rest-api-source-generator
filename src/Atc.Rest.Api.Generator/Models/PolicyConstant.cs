namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// One constant of a generated policy-name class (<see cref="Helpers.PolicyConstantsClassBuilder"/>): its C# name, the
/// policy name it holds and the sentence that documents it.
/// </summary>
/// <param name="ConstantName">The constant's identifier.</param>
/// <param name="PolicyName">The policy name (the constant's value).</param>
/// <param name="Description">The summary line.</param>
/// <param name="Attribute">An attribute line to put above the constant (<c>[Obsolete(...)]</c>), or <see langword="null"/>.</param>
public sealed record PolicyConstant(
    string ConstantName,
    string PolicyName,
    string Description,
    string? Attribute = null);