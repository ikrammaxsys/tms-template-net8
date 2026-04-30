using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;

namespace tms_template_net8.Tokens;

/// <summary>
/// Validates JWTs using the configured RSA public (or private) key and Jwt issuer/audience settings.
/// </summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly RSA _rsa;

    private readonly string _issuer;
    private readonly string _audience;

    public TokenService(IConfiguration config, RSA rsa)
    {
        _config = config;
        _rsa = rsa;

        _issuer = _config["Jwt:Issuer"] ?? "authapi";
        _audience = _config["Jwt:Audience"] ?? "authapi-client";
    }

    public (ClaimsPrincipal? principal, string? error) ValidateToken(string token)
    {
        var (principal, kind) = ValidateTokenWithKind(token);
        if (kind == AuthTokenValidationKind.Valid)
            return (principal, null);
        return (null, kind == AuthTokenValidationKind.Expired ? "token_expired" : "invalid_token");
    }

    public (ClaimsPrincipal? principal, AuthTokenValidationKind kind) ValidateTokenWithKind(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var validationParams = GetValidationParameters();
            var principal = handler.ValidateToken(token, validationParams, out var validatedToken);
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
        var key = new RsaSecurityKey(_rsa);
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidIssuer = _issuer,
            ValidAudience = _audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
    }
}
