using SmartStore.BLL.Interfaces;
using SmartStore.DAL.Models;
using SmartStore.DAL.Repositories;

namespace SmartStore.BLL.Services;

public class ProductService : IProductService
{
    private readonly ProductRepository _repository;

    public ProductService(ProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<Product>> GetInventoryAsync() =>
        await _repository.GetAllAsync();

    public async Task<List<Product>> GetMissingProductsAsync() =>
        await _repository.GetMissingAsync();
}
