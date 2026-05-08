using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using tms_template_net8.Jwt;

namespace tms_template_net8.Tokens;

/// <summary>
/// Validates JWTs using the configured RSA public (or private) key and Jwt issuer/audience settings.
/// </summary>
public class TokenService : ITokenService
{
    private readonly RSA _rsa;
    private readonly JwtOptions _jwt;

    public TokenService(RSA rsa, IOptions<JwtOptions> jwtOptions)
    {
        _rsa = rsa;
        _jwt = jwtOptions.Value;
    }

    public (ClaimsPrincipal? principal, AuthTokenValidationKind kind) ValidateTokenWithKind(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, GetValidationParameters(), out var validatedToken);
            if (validatedToken is not JwtSecurityToken)
                return (null, AuthTokenValidationKind.Invalid);

            return (principal, AuthTokenValidationKind.Valid);
        }
        catch (SecurityTokenExpiredException)
        {
            return (null, AuthTokenValidationKind.Expired);
        }
        catch (SecurityTokenNotYetValidException)
        {
            return (null, AuthTokenValidationKind.Invalid);
        }
        catch (Exception)
        {
            return (null, AuthTokenValidationKind.Invalid);
        }
    }

    private TokenValidationParameters GetValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(_rsa),
            ValidIssuer = _jwt.Issuer,
            ValidAudience = _jwt.Audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
    }
}
