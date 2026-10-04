using Microsoft.AspNetCore.Mvc;
using SmartStore.BLL.Interfaces;

namespace SmartStore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetInventory() =>
        Ok(await _service.GetInventoryAsync());

    [HttpGet("missing")]
    public async Task<IActionResult> GetMissing() =>
        Ok(await _service.GetMissingProductsAsync());
}
