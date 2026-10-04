using Microsoft.AspNetCore.Mvc;
using SmartStore.BLL.Interfaces;
using SmartStore.DAL.Models;

namespace SmartStore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _service;

    public RecommendationsController(IRecommendationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _service.GetAllRecommendationsAsync());

    [HttpPost]
    public async Task<IActionResult> Add(PurchaseRecommendation recommendation)
    {
        await _service.AddRecommendationAsync(recommendation);
        return Ok();
    }
}
