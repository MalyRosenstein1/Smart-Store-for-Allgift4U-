using Microsoft.EntityFrameworkCore;
using SmartStore.DAL.Data;
using SmartStore.DAL.Models;

namespace SmartStore.DAL.Repositories;

public class ProductRepository
{
    private readonly SmartStoreContext _context;

    public ProductRepository(SmartStoreContext context)
    {
        _context = context;
    }

    public async Task<List<Product>> GetAllAsync() =>
        await _context.Products.ToListAsync();

    public async Task<List<Product>> GetMissingAsync() =>
        await _context.Products.Where(p => p.CurrentQuantity < p.MinQuantity).ToListAsync();
}
