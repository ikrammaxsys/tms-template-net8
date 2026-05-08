using tms_template_net8.AccessValidation;
using tms_template_net8.Services;
using tms_template_net8.Tokens;

namespace tms_template_net8.Jwt;

public static class AuthServiceExtensions
{
    /// <summary>
    /// Registers everything the ACL gate + access-token middleware needs:
    /// RSA signing key, JWT validator, refresh-token client, ACL session cache,
    /// and the named "Vasp" HttpClient used to call the auth API.
    /// </summary>
    public static IServiceCollection AddAuthAndAcl(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.SectionName));

        var rsa = RsaKeyLoader.LoadRsaKey(config, env);
        services.AddSingleton(rsa);

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthTokenRefreshService, AuthTokenRefreshService>();
        services.AddScoped<IUserAccessControlService, UserAccessControlService>();

        services.AddHttpClient("Vasp", (sp, client) =>
        {
            var baseUrl = sp.GetRequiredService<IConfiguration>()["Vasp:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("Configuration 'Vasp:BaseUrl' is required.");
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        });

        return services;
    }
}
