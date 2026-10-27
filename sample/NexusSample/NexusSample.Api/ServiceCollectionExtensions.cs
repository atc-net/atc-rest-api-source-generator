namespace NexusSample.Api;

/// <summary>
/// Extension methods for configuring Nexus authentication.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds JWT Bearer authentication configured for demo purposes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WARNING: This configuration accepts ANY bearer token and should NEVER be used in production!
    /// </para>
    /// <para>
    /// The specification declares no securitySchemes, only <c>x-authorize-roles</c>, so the generated
    /// endpoints require the roles under the default authentication scheme ("Bearer"). The demo identity
    /// holds both roles the specification names: nexus.read.all and nexus.execute.all.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddAuthenticationForNexusDemo(
        this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(ConfigureDemoJwtBearer);

        services.AddAuthorization();

        return services;
    }

    private static void ConfigureDemoJwtBearer(JwtBearerOptions options)
    {
        // Disable metadata retrieval (no real authority)
        options.RequireHttpsMetadata = false;
        options.Authority = null;
        options.MetadataAddress = string.Empty;

        // Accept any bearer token for demo purposes: the token is never validated, a demo identity
        // holding every role of the specification answers for it
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
                    new Claim(ClaimTypes.Role, "nexus.read.all"),
                    new Claim(ClaimTypes.Role, "nexus.execute.all"),
                };

                context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
                context.Success();
                return Task.CompletedTask;
            },
        };
    }
}