using tms_template_net8.Models.Product;

namespace tms_template_net8.Services;

public interface IProductService
{
    IReadOnlyList<ProductItem> GetAll();
    ProductItem? GetById(int id);
    ProductItem Create(ProductItem product);
    bool Update(int id, ProductItem product);
    bool Delete(int id);
}
