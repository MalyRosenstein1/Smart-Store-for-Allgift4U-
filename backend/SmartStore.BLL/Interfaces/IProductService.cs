using SmartStore.DAL.Models;

namespace SmartStore.BLL.Interfaces;

public interface IProductService
{
    Task<List<Product>> GetInventoryAsync();
    Task<List<Product>> GetMissingProductsAsync();
}
