using Microsoft.AspNetCore.Mvc;
using SmartStore.BLL.Interfaces;
using SmartStore.DAL.Models;

namespace SmartStore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AILogsController : ControllerBase
{
    private readonly IAILogService _service;

    public AILogsController(IAILogService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Add(AI_Log log)
    {
        await _service.AddLogAsync(log);
        return Ok();
    }
}
