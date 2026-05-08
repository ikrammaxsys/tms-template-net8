using Microsoft.AspNetCore.Mvc;
using tms_template_net8.Models;

namespace tms_template_net8.AccessControl;

/// <summary>
/// Declarative page-level access control. Decorate a controller or action with this attribute
/// to require that the current session's <c>UserAclData.AccessControls</c> contains the named
/// resource with the specified <see cref="AccessRight"/>.
/// </summary>
/// <example>
/// <code>
/// [RequirePageAccess("PAB Sites", AccessRight.View)]
/// public class PabSitesController : Controller { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePageAccessAttribute : TypeFilterAttribute
{
    public RequirePageAccessAttribute(string accessName, AccessRight right = AccessRight.View)
        : base(typeof(PageAccessAuthorizationFilter))
    {
        if (string.IsNullOrWhiteSpace(accessName))
            throw new ArgumentException("Access name is required.", nameof(accessName));

        AccessName = accessName;
        Right = right;
        Arguments = new object[] { accessName, right };
    }

    public string AccessName { get; }
    public AccessRight Right { get; }
}
