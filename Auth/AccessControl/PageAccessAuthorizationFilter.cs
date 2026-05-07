using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using tms_template_net8.Models;
using tms_template_net8.Services;

namespace tms_template_net8.AccessControl;

/// <summary>
/// Authorization filter that backs <see cref="RequirePageAccessAttribute"/>.
/// Reads the cached <see cref="UserAclData"/> from session and either lets the request
/// proceed or short-circuits with a redirect to <c>/Home/AccessDenied</c>.
/// </summary>
public sealed class PageAccessAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly string _accessName;
    private readonly AccessRight _right;
    private readonly IUserAccessControlService _accessControlService;

    public PageAccessAuthorizationFilter(
        string accessName,
        AccessRight right,
        IUserAccessControlService accessControlService)
    {
        _accessName = accessName;
        _right = right;
        _accessControlService = accessControlService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        await http.Session.LoadAsync(http.RequestAborted).ConfigureAwait(false);

        if (_accessControlService.HasAccess(http, _accessName, _right))
            return;

        var basePath = http.Request.PathBase.HasValue
            ? http.Request.PathBase.ToString().TrimEnd('/')
            : string.Empty;

        var url = $"{basePath}/Home/AccessDenied?name={Uri.EscapeDataString(_accessName)}&right={Uri.EscapeDataString(_right.ToToken())}";
        context.Result = new RedirectResult(url);
    }
}
