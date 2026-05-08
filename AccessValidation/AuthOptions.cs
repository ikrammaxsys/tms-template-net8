namespace tms_template_net8.AccessValidation;

/// <summary>
/// Bound from the <c>Auth</c> configuration section. Centralises every key used by
/// <see cref="AccessTokenValidationMiddleware"/>, <see cref="AuthTokenRefreshService"/>,
/// the ACL-gate controller, and the user-roles loader.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Base URL of the standalone auth API (no trailing slash required).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Path used by the ACL gate to swap an <c>auth-code</c> for tokens.</summary>
    public string ExchangeAuthCodeUrl { get; set; } = string.Empty;

    /// <summary>Path used by the refresh service when the access token is expired.</summary>
    public string RefreshTokenApiUrl { get; set; } = "/api/auth/refresh-token";

    /// <summary>Optional path used to call out on logout (currently informational).</summary>
    public string LogoutApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Path (or absolute URL) to load the current user's roles + access controls.
    /// May contain the <c>{idAclUser}</c> placeholder.
    /// </summary>
    public string UserRolesAndAccessUrl { get; set; } = string.Empty;

    /// <summary>Cookie name for the access token JWT.</summary>
    public string AccessTokenStorageKey { get; set; } = "authacl_access_token";

    /// <summary>Cookie name for the refresh token.</summary>
    public string RefreshTokenStorageKey { get; set; } = "authacl_refresh_token";

    /// <summary>When true the refresh request body uses <c>{ grantType, refreshToken }</c>; otherwise <c>{ refreshToken }</c>.</summary>
    public bool RefreshTokenRequestUsesGrantType { get; set; }
}
