using System.Security.Cryptography;
using tms_template_net8.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace tms_template_net8.Jwt;

public static class AuthServiceExtensions
{
    /// <summary>
    /// Registers RSA key material, JWT validation for <see cref="ITokenService"/>, and JWT Bearer authentication.
    /// </summary>
    public static IServiceCollection AddAuthServices(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        var rsa = RsaKeyLoader.LoadRsaKey(config, env);
        services.AddSingleton(rsa);

        services.AddScoped<ITokenService, TokenService>();

        var signingKey = new RsaSecurityKey(rsa);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
                };
            });

        return services;
    }
}
