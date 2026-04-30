using tms_template_net8.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace tms_template_net8.AccessValidation;

/// <summary>
/// For browser (non-API) routes, requires a valid JWT in the access-token cookie (or query).
/// If the access token is expired but a refresh token cookie is present, attempts to refresh via the auth API and updates cookies before continuing.
/// </summary>
public sealed class AccessTokenValidationMiddleware
{
    private readonly RequestDelegate _next;

    public AccessTokenValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITokenService tokenService,
        IAuthTokenRefreshService refreshService,
        IConfiguration configuration)
    {
        if (ShouldSkipTokenCheck(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var tokenKey = configuration["Auth:AccessTokenStorageKey"] ?? "authacl_access_token";
        var refreshKey = configuration["Auth:RefreshTokenStorageKey"] ?? "authacl_refresh_token";
        var token = context.Request.Cookies[tokenKey] ?? context.Request.Query["access_token"].ToString();
        var appBaseUrl = configuration["App:BaseUrl"] ?? "";

        if (string.IsNullOrWhiteSpace(token))
        {
            context.Response.Redirect(appBaseUrl + "/Home/SessionExpired");
            return;
        }

        var (principal, kind) = tokenService.ValidateTokenWithKind(token);

        if (kind == AuthTokenValidationKind.Valid && principal != null)
        {
            await _next(context);
            return;
        }

        if (kind == AuthTokenValidationKind.Expired)
        {
            var refreshToken = context.Request.Cookies[refreshKey];
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                var refreshed = await refreshService.TryRefreshAsync(refreshToken.Trim(), context.RequestAborted);
                if (refreshed != null)
                {
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = false,
                        Secure = context.Request.IsHttps,
                        Path = "/",
                        SameSite = SameSiteMode.Lax,
                    };
                    context.Response.Cookies.Append(tokenKey, refreshed.AccessToken, cookieOptions);
                    if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
                        context.Response.Cookies.Append(refreshKey, refreshed.RefreshToken!, cookieOptions);

                    var (newPrincipal, newKind) = tokenService.ValidateTokenWithKind(refreshed.AccessToken);
                    if (newKind == AuthTokenValidationKind.Valid && newPrincipal != null)
                    {
                        await _next(context);
                        return;
                    }
                }
            }
        }

        context.Response.Redirect(appBaseUrl + "/Home/SessionExpired");
    }

    private static bool ShouldSkipTokenCheck(PathString path)
    {
        return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/ACLChecking", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Home/SessionExpired", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Login", StringComparison.OrdinalIgnoreCase);
    }
}
