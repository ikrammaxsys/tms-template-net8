using Microsoft.AspNetCore.Mvc;
using tms_template_net8.Models.Product;
using tms_template_net8.Services;

namespace tms_template_net8.Controllers.Api;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public IActionResult GetAllProducts()
    {
        var products = _productService.GetAll();
        return Ok(new { success = true, message = "Products fetched successfully.", data = products });
    }

    [HttpGet("{id:int}")]
    public IActionResult GetProductById(int id)
    {
        var product = _productService.GetById(id);
        if (product is null)
            return NotFound(new { success = false, message = "Product not found." });

        return Ok(new { success = true, message = "Product fetched successfully.", data = product });
    }

    [HttpPost]
    public IActionResult CreateProduct([FromBody] ProductUpsertRequest? body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { success = false, message = "Product name is required." });

        var created = _productService.Create(new ProductItem
        {
            Name = body.Name.Trim(),
            Sku = (body.Sku ?? string.Empty).Trim(),
            Price = body.Price,
            Status = string.IsNullOrWhiteSpace(body.Status) ? "Active" : body.Status.Trim(),
            Description = (body.Description ?? string.Empty).Trim()
        });

        return StatusCode(StatusCodes.Status201Created, new
        {
            success = true,
            message = "Product created successfully.",
            data = created
        });
    }

    [HttpPut("{id:int}")]
    public IActionResult UpdateProduct(int id, [FromBody] ProductUpsertRequest? body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { success = false, message = "Product name is required." });

        var updated = _productService.Update(id, new ProductItem
        {
            Name = body.Name.Trim(),
            Sku = (body.Sku ?? string.Empty).Trim(),
            Price = body.Price,
            Status = string.IsNullOrWhiteSpace(body.Status) ? "Active" : body.Status.Trim(),
            Description = (body.Description ?? string.Empty).Trim()
        });

        if (!updated)
            return NotFound(new { success = false, message = "Product not found." });

        var product = _productService.GetById(id);
        return Ok(new { success = true, message = "Product updated successfully.", data = product });
    }

    [HttpDelete("{id:int}")]
    public IActionResult DeleteProduct(int id)
    {
        var deleted = _productService.Delete(id);
        if (!deleted)
            return NotFound(new { success = false, message = "Product not found." });

        return Ok(new { success = true, message = "Product deleted successfully." });
    }

    public sealed class ProductUpsertRequest
    {
        public string? Name { get; set; }
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
    }
}
