namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// What a server's specification uses, each found by one rule: the blocks the generated <c>AddApi</c>, <c>MapApi</c>
/// and their options emit follow it.
/// </summary>
/// <param name="HasRateLimiting">An operation or the document declares a rate limit.</param>
/// <param name="HasValidation">The validation filter is not disabled.</param>
/// <param name="HasVersioning">A versioning strategy is configured.</param>
/// <param name="HasSecuritySchemes">The document declares a security scheme: the generated security policies are registered.</param>
/// <param name="RequiresAuthentication">A security scheme, or access declared with Atc's extensions alone (<c>x-authorize-roles</c>, <c>x-authentication-required</c>): the authentication and authorization middleware run.</param>
/// <param name="HasXmlBodies">A body the generated code reads or writes as XML.</param>
/// <param name="HasEnums">An enum the generated wire converters handle.</param>
/// <param name="HasOutputCaching">An operation or the document declares output caching.</param>
/// <param name="LimiterNeedsUser">A rate limiter partitioned by user, which reads <c>HttpContext.User</c>: authentication runs before it.</param>
public sealed record ServerFeatures(
    bool HasRateLimiting,
    bool HasValidation,
    bool HasVersioning,
    bool HasSecuritySchemes,
    bool RequiresAuthentication,
    bool HasXmlBodies,
    bool HasEnums,
    bool HasOutputCaching,
    bool LimiterNeedsUser)
{
    /// <summary>
    /// The features of a server's specification.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="config">The server configuration, or <see langword="null"/> for the defaults (no validation filter, no versioning).</param>
    /// <returns>The features.</returns>
    public static ServerFeatures Of(
        OpenApiDocument openApiDoc,
        ServerConfig? config)
    {
        if (openApiDoc is null)
        {
            throw new ArgumentNullException(nameof(openApiDoc));
        }

        var hasRateLimiting = openApiDoc.HasRateLimiting();
        var requiresAuthentication = openApiDoc.RequiresAuthentication();
        return new ServerFeatures(
            hasRateLimiting,
            HasValidation: config is not null && config.UseValidationFilter != MinimalApiPackageMode.Disabled,
            HasVersioning: config is not null && config.VersioningStrategy != VersioningStrategyType.None,
            HasSecuritySchemes: openApiDoc.HasSecuritySchemes(),
            requiresAuthentication,
            HasXmlBodies: XmlBodyReachability.Collect(openApiDoc).Count > 0,
            HasEnums: EnumWireConverterExtractor.HasEnums(openApiDoc),
            HasOutputCaching: openApiDoc.HasOutputCaching(),
            LimiterNeedsUser: hasRateLimiting && requiresAuthentication &&
                              RateLimitPoliciesExtractor.CollectPolicies(openApiDoc, config?.IncludeDeprecated ?? false)
                                  .Values.Any(p => p.Partition == RateLimitPartitionStrategy.User));
    }
}