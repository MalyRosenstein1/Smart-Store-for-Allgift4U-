using Microsoft.AspNetCore.Mvc;
using SmartStore.BLL.Interfaces;

namespace SmartStore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    // ─── Existing endpoints ───────────────────────────────────────────────────────

    [HttpGet("order-stock/{orderName}")]
    public IActionResult GetOrderStock(string orderName)
    {
        try
        {
            var table = _inventoryService.GetOrderStock(orderName);
            return Ok(table);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("process-order")]
    public async Task<IActionResult> ProcessOrder([FromBody] OrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.OrderName))
            return BadRequest("Order name is required.");

        try
        {
            var result = await _inventoryService.ProcessIncomingOrder(request.OrderName);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("process-order-pdf")]
    public async Task<IActionResult> ProcessOrderPdf([FromBody] OrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.OrderName))
            return BadRequest("Order name is required.");

        try
        {
            var result = await _inventoryService.ProcessIncomingOrder(request.OrderName);
            if (result.DeliveryNotePdf.Length == 0)
                return StatusCode(500, new { error = "Delivery note PDF was not generated." });

            return File(result.DeliveryNotePdf, "application/pdf", result.DeliveryNoteFileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("process-chat")]
    public async Task<IActionResult> ProcessChat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.UserMessage))
            return BadRequest("User message is required.");

        try
        {
            var result = await _inventoryService.ProcessChatMessage(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("inventory-status")]
    public async Task<IActionResult> GetInventoryStatus()
    {
        try
        {
            var result = await _inventoryService.GetInventoryStatus();
            return Ok(new { botMessage = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("process-image")]
    public async Task<IActionResult> ProcessImage([FromBody] ImageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Base64Image))
            return BadRequest(new { error = "Image data is required." });

        try
        {
            var result = await _inventoryService.ProcessImageMessage(request.Base64Image, request.MimeType, request.FileName);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ─── Part 2: Management endpoints ────────────────────────────────────────────

    /// <summary>
    /// Creates a new order template with its product components.
    /// POST /api/inventory/templates
    /// </summary>
    [HttpPost("templates")]
    public async Task<IActionResult> CreateOrderTemplate([FromBody] CreateOrderTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.OrderName))
            return BadRequest(new { error = "OrderName is required." });

        if (request.Components == null || request.Components.Count == 0)
            return BadRequest(new { error = "At least one component is required." });

        try
        {
            var result = await _inventoryService.CreateOrderTemplate(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing order template's details and/or components.
    /// PUT /api/inventory/templates/{id}
    /// </summary>
    [HttpPut("templates/{id:int}")]
    public async Task<IActionResult> UpdateOrderTemplate(int id, [FromBody] UpdateOrderTemplateRequest request)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required." });

        request.TemplateID = id;

        try
        {
            var result = await _inventoryService.UpdateOrderTemplate(request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Manually sets the stock quantity for a product.
    /// PUT /api/inventory/products/{id}/stock
    /// </summary>
    [HttpPut("products/{id:int}/stock")]
    public async Task<IActionResult> UpdateProductStock(int id, [FromBody] UpdateStockRequest request)
    {
        if (request == null || request.NewQuantity < 0)
            return BadRequest(new { error = "NewQuantity must be 0 or greater." });

        try
        {
            var result = await _inventoryService.UpdateProductStock(id, request.NewQuantity);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class OrderRequest
{
    public string OrderName { get; set; } = string.Empty;
}

public class UpdateStockRequest
{
    public int NewQuantity { get; set; }
}

public class ImageRequest
{
    public string Base64Image { get; set; } = string.Empty;
    public string MimeType { get; set; } = "image/jpeg";
    public string? FileName { get; set; }
}
