namespace tms_template_net8.Models.Product;

public sealed class ProductItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = "Active";
    public string Description { get; set; } = string.Empty;
}
