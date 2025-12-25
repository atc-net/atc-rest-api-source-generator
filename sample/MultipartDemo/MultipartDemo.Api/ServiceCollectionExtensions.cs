namespace MultipartDemo.Api;

/// <summary>
/// Extension methods for configuring MultipartDemo authentication.
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
    /// The scheme names "bearer_auth" and "api_key" match the OpenAPI securitySchemes definitions.
    /// When authentication fails, a demo identity is created automatically so the request can proceed.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddAuthenticationForMultipartDemoDemo(
        this IServiceCollection services)
    {
        // The scheme names must match the OpenAPI securitySchemes names
        services
            .AddAuthentication("bearer_auth")
            .AddJwtBearer("bearer_auth", ConfigureDemoJwtBearer);

        services.AddAuthorization();

        return services;
    }

    private static void ConfigureDemoJwtBearer(
        Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions options)
    {
        // Disable metadata retrieval (no real authority)
        options.RequireHttpsMetadata = false;
        options.Authority = null;
        options.MetadataAddress = string.Empty;

#pragma warning disable CA5404 // Do not disable token validation checks - Demo only
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = false,

            // Use a demo signing key so tokens can be validated
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes("demo-signing-key-for-multipart-demo-only-not-production!")),
        };
#pragma warning restore CA5404

        // Accept any token that looks like a JWT for demo purposes
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // If there's an Authorization header with Bearer token, accept it
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = authHeader["Bearer ".Length..].Trim();
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = _ => Task.CompletedTask,
            OnAuthenticationFailed = context =>
            {
                // For demo: ignore authentication failures and create a demo identity
                var claims = new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "demo-user"),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "demo-user-id"),
                };
                var identity = new System.Security.Claims.ClaimsIdentity(claims, "bearer_auth");
                context.Principal = new System.Security.Claims.ClaimsPrincipal(identity);
                context.Success();
                return Task.CompletedTask;
            },
        };
    }
}