using System.Collections.Concurrent;
using tms_template_net8.Models.Product;

namespace tms_template_net8.Services;

public sealed class ProductService : IProductService
{
    private readonly ConcurrentDictionary<int, ProductItem> _products = new();
    private int _lastId;

    public ProductService()
    {
        Seed();
    }

    public IReadOnlyList<ProductItem> GetAll()
    {
        return _products.Values
            .OrderBy(x => x.Id)
            .ToList();
    }
    kambing 46

    public ProductItem? GetById(int id)
    {
        return _products.TryGetValue(id, out var product) ? Clone(product) : null;
    }

    public ProductItem Create(ProductItem product)
    {
        var nextId = Interlocked.Increment(ref _lastId);
        var created = Clone(product);
        created.Id = nextId;
        _products[nextId] = created;
        return Clone(created);
    }

    public bool Update(int id, ProductItem product)
    {
        if (!_products.ContainsKey(id))
            return false;

        var updated = Clone(product);
        updated.Id = id;
        _products[id] = updated;
        return true;
    }

    public bool Delete(int id)
    {
        return _products.TryRemove(id, out _);
    }

    private void Seed()
    {
        var samples = new[]
        {
            new ProductItem
            {
                Id = 1,
                Name = "Laptop 14 Inch",
                Sku = "PRD-LTP-001",
                Price = 1200.00m,
                Status = "Active",
                Description = "Dummy item for Product module."
            },
            new ProductItem
            {
                Id = 2,
                Name = "Wireless Mouse",
                Sku = "PRD-MSE-002",
                Price = 35.50m,
                Status = "Active",
                Description = "Dummy item for Product module."
            },
            new ProductItem
            {
                Id = 3,
                Name = "Legacy Printer",
                Sku = "PRD-PRN-003",
                Price = 450.00m,
                Status = "Inactive",
                Description = "Dummy item for Product module."
            }
        };

        foreach (var item in samples)
        {
            _products[item.Id] = Clone(item);
        }

        _lastId = samples.Max(x => x.Id);
    }

    private static ProductItem Clone(ProductItem source)
    {
        return new ProductItem
        {
            Id = source.Id,
            Name = source.Name,
            Sku = source.Sku,
            Price = source.Price,
            Status = source.Status,
            Description = source.Description
        };
    }
}
