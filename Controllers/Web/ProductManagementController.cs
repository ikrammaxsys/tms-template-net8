using Microsoft.AspNetCore.Mvc;
using tms_template_net8.AccessControl;
using tms_template_net8.Models;
using tms_template_net8.Services;

namespace tms_template_net8.Controllers.Web;

// Controller-level requirement: any action below needs at least 'view' on the access-control resource.
// The string must match a key in the ACL `accessControls` dictionary returned by the auth API.
[Route("[controller]")]
[RequirePageAccess("PAB Sites", AccessRight.View)]
public class ProductManagementController : Controller
{
    private readonly IProductService _productService;

    public ProductManagementController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("GetList")]
    public IActionResult GetList()
    {
        var rows = _productService.GetAll()
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Sku,
                x.Price,
                x.Status
            })
            .ToList();

        return Json(rows);
    }

    // Per-action override: requires 'add' on top of the controller-level 'view'.
    [HttpGet("Create")]
    [RequirePageAccess("PAB Sites", AccessRight.Add)]
    public IActionResult Create()
    {
        return View();
    }

    [HttpGet("Detail/{id:int}")]
    public IActionResult Detail(int id)
    {
        ViewBag.Id = id;
        return View();
    }

    [HttpGet("Edit/{id:int}")]
    [RequirePageAccess("PAB Sites", AccessRight.Edit)]
    public IActionResult Edit(int id)
    {
        ViewBag.Id = id;
        return View();
    }
}
