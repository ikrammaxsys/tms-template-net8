using System.Security.Cryptography;
using tms_template_net8.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace tms_template_net8.Jwt;

public static class AuthServiceExtensions
{
    /// <summary>
    /// Registers RSA key material and the local JWT validator used by the custom access-token middleware.
    /// </summary>
    public static IServiceCollection AddAuthServices(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        var rsa = RsaKeyLoader.LoadRsaKey(config, env);
        services.AddSingleton(rsa);

        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}
