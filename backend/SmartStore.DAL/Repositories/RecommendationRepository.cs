using Microsoft.EntityFrameworkCore;
using SmartStore.DAL.Data;
using SmartStore.DAL.Models;

namespace SmartStore.DAL.Repositories;

public class RecommendationRepository
{
    private readonly SmartStoreContext _context;

    public RecommendationRepository(SmartStoreContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PurchaseRecommendation recommendation)
    {
        _context.PurchaseRecommendations.Add(recommendation);
        await _context.SaveChangesAsync();
    }

    public async Task<List<PurchaseRecommendation>> GetAllAsync() =>
        await _context.PurchaseRecommendations.ToListAsync();
}
