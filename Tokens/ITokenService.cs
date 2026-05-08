using System.Security.Claims;

namespace tms_template_net8.Tokens;

/// <summary>
/// Validates JWT access tokens issued by the standalone auth service (same signing key / issuer / audience as configured).
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Validates the access token and distinguishes expiry from other failures (bad signature, wrong issuer, etc.).
    /// </summary>
    (ClaimsPrincipal? principal, AuthTokenValidationKind kind) ValidateTokenWithKind(string token);
}
