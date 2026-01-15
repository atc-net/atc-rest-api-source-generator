namespace Atc.Rest.Api.Generator;

/// <summary>
/// Constants for OAuth2 code generation and runtime configuration.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Intentional grouping of related constants.")]
public static class OAuthConstants
{
    /// <summary>
    /// OAuth2 grant type constants.
    /// </summary>
    public static class GrantTypes
    {
        /// <summary>
        /// Client Credentials grant type for machine-to-machine authentication.
        /// </summary>
        public const string ClientCredentials = "client_credentials";

        /// <summary>
        /// Refresh Token grant type for obtaining new access tokens.
        /// </summary>
        public const string RefreshToken = "refresh_token";

        /// <summary>
        /// Authorization Code grant type for user authentication flows.
        /// </summary>
        public const string AuthorizationCode = "authorization_code";
    }

    /// <summary>
    /// OAuth2 JSON property names for token request/response payloads.
    /// </summary>
    public static class JsonPropertyNames
    {
        /// <summary>
        /// The access token property name.
        /// </summary>
        public const string AccessToken = "access_token";

        /// <summary>
        /// The token type property name (typically "Bearer").
        /// </summary>
        public const string TokenType = "token_type";

        /// <summary>
        /// The token expiration time in seconds property name.
        /// </summary>
        public const string ExpiresIn = "expires_in";

        /// <summary>
        /// The refresh token property name.
        /// </summary>
        public const string RefreshToken = "refresh_token";

        /// <summary>
        /// The scope property name.
        /// </summary>
        public const string Scope = "scope";

        /// <summary>
        /// The grant type property name.
        /// </summary>
        public const string GrantType = "grant_type";

        /// <summary>
        /// The client ID property name.
        /// </summary>
        public const string ClientId = "client_id";

        /// <summary>
        /// The client secret property name.
        /// </summary>
        public const string ClientSecret = "client_secret";
    }

    /// <summary>
    /// Generated OAuth type names.
    /// </summary>
    public static class TypeNames
    {
        /// <summary>
        /// The OAuth token response class name.
        /// </summary>
        public const string TokenResponse = "OAuthTokenResponse";

        /// <summary>
        /// The OAuth token provider interface name.
        /// </summary>
        public const string TokenProviderInterface = "IOAuthTokenProvider";

        /// <summary>
        /// The OAuth token provider class name.
        /// </summary>
        public const string TokenProviderClass = "OAuthTokenProvider";

        /// <summary>
        /// The OAuth client options class name.
        /// </summary>
        public const string ClientOptions = "OAuthClientOptions";

        /// <summary>
        /// The OAuth authentication handler class name.
        /// </summary>
        public const string AuthenticationHandler = "OAuthAuthenticationHandler";

        /// <summary>
        /// The OAuth service collection extensions class name.
        /// </summary>
        public const string ServiceCollectionExtensions = "OAuthServiceCollectionExtensions";
    }

    /// <summary>
    /// OAuth configuration section names for appsettings.json.
    /// </summary>
    public static class ConfigSections
    {
        /// <summary>
        /// The root OAuth configuration section name.
        /// </summary>
        public const string OAuth = "OAuth";

        /// <summary>
        /// Returns the configuration section path for a specific project.
        /// </summary>
        /// <param name="projectName">The project name.</param>
        /// <returns>Configuration section path in format "OAuth:{projectName}".</returns>
        public static string ForProject(string projectName)
            => $"{OAuth}:{projectName}";
    }

    /// <summary>
    /// Authentication scheme names.
    /// </summary>
    public static class Schemes
    {
        /// <summary>
        /// The Bearer authentication scheme.
        /// </summary>
        public const string Bearer = "Bearer";
    }

    /// <summary>
    /// Named HTTP client names used for OAuth.
    /// </summary>
    public static class HttpClientNames
    {
        /// <summary>
        /// The named HTTP client for the OAuth token provider.
        /// </summary>
        public const string TokenProvider = "OAuthTokenProvider";
    }
}