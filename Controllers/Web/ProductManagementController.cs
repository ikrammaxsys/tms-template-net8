using Microsoft.AspNetCore.Mvc;
using tms_template_net8.Services;

namespace tms_template_net8.Controllers.Web;

[Route("[controller]")]
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

    [HttpGet("Create")]
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
    public IActionResult Edit(int id)
    {
        ViewBag.Id = id;
        return View();
    }
}
