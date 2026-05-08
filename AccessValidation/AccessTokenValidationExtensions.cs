using Microsoft.AspNetCore.Builder;

namespace tms_template_net8.AccessValidation;

/// <summary>
/// Registration helpers for the ACL gate pipeline.
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

    /// <summary>
    /// Redirects bare <c>/</c> requests to <c>/ACLChecking</c> (preserving the query string and any
    /// configured base path), so external login redirects with a wrong path still hit the ACL gate.
    /// Must run after <c>UseRouting</c>/<c>UseSession</c> and before <see cref="UseAccessTokenValidation"/>.
    /// </summary>
    public static IApplicationBuilder UseRootAclRedirect(this IApplicationBuilder app)
    {
        return app.Use((ctx, next) =>
        {
            if (ctx.Request.Path != "/")
                return next();

            var basePath = ctx.Request.PathBase.HasValue
                ? ctx.Request.PathBase.ToString().TrimEnd('/')
                : string.Empty;
            ctx.Response.Redirect($"{basePath}/ACLChecking{ctx.Request.QueryString}");
            return Task.CompletedTask;
        });
    }
}
