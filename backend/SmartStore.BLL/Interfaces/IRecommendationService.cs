using SmartStore.DAL.Models;

namespace SmartStore.BLL.Interfaces;

public interface IRecommendationService
{
    Task AddRecommendationAsync(PurchaseRecommendation recommendation);
    Task<List<PurchaseRecommendation>> GetAllRecommendationsAsync();
}
