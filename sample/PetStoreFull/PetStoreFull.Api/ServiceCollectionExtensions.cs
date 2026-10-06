namespace PetStoreFull.Api;

/// <summary>
/// Extension methods for configuring PetStoreFull authentication.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the two authentication schemes the specification names, configured for demo purposes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WARNING: This configuration accepts ANY bearer token and ANY api_key header and should NEVER be used in production!
    /// </para>
    /// <para>
    /// The generated endpoints name their authentication schemes after the securityScheme keys of the
    /// specification: "petstore_auth" (oauth2, scopes write:pets and read:pets) and "api_key" (an API key
    /// in the api_key header). Both must be registered, or the requests fail at runtime with
    /// "No authentication handler is registered for the scheme".
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddAuthenticationForPetStoreFullDemo(
        this IServiceCollection services)
    {
        services
            .AddAuthentication("petstore_auth")
            .AddJwtBearer("petstore_auth", ConfigureDemoJwtBearer)
            .AddScheme<AuthenticationSchemeOptions, DemoApiKeyAuthenticationHandler>("api_key", _ => { });

        // The generated authorization policies: one per scope combination the specification uses
        services.AddApiSecurityPolicies();

        return services;
    }

    private static void ConfigureDemoJwtBearer(JwtBearerOptions options)
    {
        // Disable metadata retrieval (no real authority)
        options.RequireHttpsMetadata = false;
        options.Authority = null;
        options.MetadataAddress = string.Empty;

        // Accept any bearer token for demo purposes: the token is never validated, a demo identity
        // holding every scope of the petstore_auth scheme answers for it
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, "demo-user"),
                    new Claim(ClaimTypes.NameIdentifier, "demo-user-id"),

                    // The scopes as a token delivers them: one space-separated claim
                    new Claim("scope", "write:pets read:pets"),
                };

                context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
                context.Success();
                return Task.CompletedTask;
            },
        };
    }
}