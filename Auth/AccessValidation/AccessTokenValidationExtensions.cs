using Microsoft.AspNetCore.Builder;

namespace tms_template_net8.AccessValidation;

/// <summary>
/// Registration helpers for access-token validation middleware.
/// </summary>
public static class AccessTokenValidationExtensions
{
    /// <summary>
    /// Enforces access-token validation (with optional refresh) on MVC routes; see <see cref="AccessTokenValidationMiddleware"/>.
    /// </summary>
    public static IApplicationBuilder UseAccessTokenValidation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AccessTokenValidationMiddleware>();
    }
}
