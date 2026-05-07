using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using tms_template_net8.Models;
using tms_template_net8.Services;

namespace tms_template_net8.AccessControl;

/// <summary>
/// Authorization filter that backs <see cref="RequirePageAccessAttribute"/>.
/// Reads the cached <see cref="UserAclData"/> from session and either lets the request
/// proceed or short-circuits with a redirect to <c>/Home/AccessDenied</c> (HTTP 403 for AJAX/API).
/// </summary>
public sealed class PageAccessAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly string _accessName;
    private readonly AccessRight _right;
    private readonly IUserAccessControlService _accessControlService;
    private readonly ILogger<PageAccessAuthorizationFilter> _logger;

    public PageAccessAuthorizationFilter(
        string accessName,
        AccessRight right,
        IUserAccessControlService accessControlService,
        ILogger<PageAccessAuthorizationFilter> logger)
    {
        _accessName = accessName;
        _right = right;
        _accessControlService = accessControlService;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        await http.Session.LoadAsync(http.RequestAborted).ConfigureAwait(false);

        if (_accessControlService.HasAccess(http, _accessName, _right))
            return;

        var snapshot = _accessControlService.GetCurrent(http);
        if (snapshot == null)
        {
            _logger.LogWarning("Access denied: no UserAclData in session for {Path} (required '{Name}'/{Right}).",
                http.Request.Path, _accessName, _right);
        }
        else
        {
            _logger.LogInformation("Access denied for user '{User}' on {Path}: missing '{Name}'/{Right}.",
                snapshot.User?.UserId, http.Request.Path, _accessName, _right);
        }

        context.Result = BuildDeniedResult(http);
    }

    private IActionResult BuildDeniedResult(HttpContext http)
    {
        if (IsAjaxOrApi(http))
        {
            return new JsonResult(new
            {
                success = false,
                message = $"You do not have '{_right.ToToken()}' access to '{_accessName}'.",
                accessName = _accessName,
                right = _right.ToToken()
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        var basePath = http.Request.PathBase.HasValue
            ? http.Request.PathBase.ToString().TrimEnd('/')
            : string.Empty;

        var url = $"{basePath}/Home/AccessDenied?name={Uri.EscapeDataString(_accessName)}&right={Uri.EscapeDataString(_right.ToToken())}";
        return new RedirectResult(url);
    }

    private static bool IsAjaxOrApi(HttpContext http)
    {
        if (http.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return true;

        var requestedWith = http.Request.Headers["X-Requested-With"].ToString();
        if (string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return true;

        var accept = http.Request.Headers.Accept.ToString();
        return !string.IsNullOrWhiteSpace(accept)
            && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
