using System.Data;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TMS.WebApp.Sdk.Data.Sql;
using tms_template_net8.Models;

namespace tms_template_net8.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ISqlExecutor _sql;

    public HomeController(ILogger<HomeController> logger, ISqlExecutor sql)
    {
        _logger = logger;
        _sql = sql;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // List<SqlParameter> _pMssql = new List<SqlParameter>();
        // _pMssql.Add(new SqlParameter("@testParameter", "Admin"));
        // var dt = await _sql.ExecuteAsync("PSP_SELECT_EMPLOYEES", CommandType.StoredProcedure, _pMssql, "StdTemplate_DEV", cancellationToken: ct);
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    public IActionResult SessionExpired()
    {
        return View();
    }

    /// <summary>
    /// Renders the access-denied page when a user lacks the required access for a route
    /// (see <c>[RequirePageAccess]</c>). Returns HTTP 403 so it is also discoverable for
    /// scripted clients that do follow the redirect.
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult AccessDenied(string? name = null, string? right = null)
    {
        ViewBag.AccessName = name;
        ViewBag.AccessRight = right;
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
