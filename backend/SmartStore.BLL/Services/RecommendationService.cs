using SmartStore.BLL.Interfaces;
using SmartStore.DAL.Models;
using SmartStore.DAL.Repositories;

namespace SmartStore.BLL.Services;

public class RecommendationService : IRecommendationService
{
    private readonly RecommendationRepository _repository;

    public RecommendationService(RecommendationRepository repository)
    {
        _repository = repository;
    }

    public async Task AddRecommendationAsync(PurchaseRecommendation recommendation) =>
        await _repository.AddAsync(recommendation);

    public async Task<List<PurchaseRecommendation>> GetAllRecommendationsAsync() =>
        await _repository.GetAllAsync();
}
