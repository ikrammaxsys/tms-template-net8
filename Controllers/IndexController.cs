using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace tms_template_net8.Controllers.Web;

/// <summary>
/// OPTIONAL: Default route example controller.
/// This controller redirects root requests to the ACL gate with query parameters preserved.
/// 
/// Use this if you want the root path (/) to go through the ACL gate.
/// Skip this if your subsystem has an existing landing page or different routing pattern.
/// </summary>
[Route("[controller]")]
public class IndexController : Controller
{
    private readonly ILogger<IndexController> _logger;

    public IndexController(ILogger<IndexController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var qb = new QueryBuilder();
        foreach (var kv in Request.Query)
        {
            foreach (var v in kv.Value)
                qb.Add(kv.Key, v ?? string.Empty);
        }

        var target = (Url.Action("Index", "ACLChecking") ?? "/ACLChecking") + qb.ToQueryString();
        return Redirect(target);
    }
}
