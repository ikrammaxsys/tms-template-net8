namespace tms_template_net8.AccessValidation;

/// <summary>
/// Calls the external auth API to obtain new tokens using a refresh token.
/// </summary>
public interface IAuthTokenRefreshService
{
    /// <summary>
    /// Returns new access (and optional refresh) tokens, or null if refresh failed or is not configured.
    /// </summary>
    Task<AuthRefreshedTokens?> TryRefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}

/// <param name="AccessToken">New JWT access token.</param>
/// <param name="RefreshToken">New refresh token if the server rotated it; otherwise null to keep the existing cookie.</param>
public sealed record AuthRefreshedTokens(string AccessToken, string? RefreshToken);
