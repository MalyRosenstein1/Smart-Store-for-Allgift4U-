namespace SmartStore.BLL.Interfaces;

public interface IInventoryService
{
    Task<OrderResult> ProcessIncomingOrder(string orderName);
    Task<ChatResponse> ProcessChatMessage(ChatRequest request);
    Task<OrderTemplateResult> CreateOrderTemplate(CreateOrderTemplateRequest request);
    Task<OrderTemplateResult> UpdateOrderTemplate(UpdateOrderTemplateRequest request);
    Task<ProductStockResult> UpdateProductStock(int productId, int newQuantity);
    Task<string> GetInventoryStatus();
    Task<ChatResponse> ProcessImageMessage(string base64Image, string mimeType, string? fileName = null);
    OrderStockResult GetOrderStock(string orderName);
}

// --- Chat DTOs ---
public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class ChatRequest
{
    public string UserMessage { get; set; } = string.Empty;
    public List<ChatMessage> ChatHistory { get; set; } = new();
}

public class ChatResponse
{
    public string BotMessage { get; set; } = string.Empty;
    public OrderResult? OrderResult { get; set; }
    public ProductStockResult? StockResult { get; set; }
    public bool IsOrderProcessing { get; set; }
}

// --- Order Processing DTOs ---
public class OrderResult
{
    public string OrderName { get; set; } = string.Empty;
    public List<OrderItemResult> Items { get; set; } = new();
    public List<string> StockDepletionWarnings { get; set; } = new();
    public List<RecommendationDto> Recommendations { get; set; } = new();
    public List<string> EmailDrafts { get; set; } = new();
    public byte[] DeliveryNotePdf { get; set; } = Array.Empty<byte>();
    public string DeliveryNoteFileName { get; set; } = string.Empty;
}

public class OrderItemResult
{
    public string ProductName { get; set; } = string.Empty;
    public int QuantityRemoved { get; set; }
    public int NewQuantity { get; set; }
    public bool AiTriggered { get; set; }
}

public class RecommendationDto
{
    public string SupplierName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? PurchaseUrl { get; set; }
}

// --- Management DTOs ---
public class TemplateComponentDto
{
    public int ProductID { get; set; }
    public int QuantityRequired { get; set; }
}

public class CreateOrderTemplateRequest
{
    public string OrderName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<TemplateComponentDto> Components { get; set; } = new();
}

public class UpdateOrderTemplateRequest
{
    public int TemplateID { get; set; }
    public string? OrderName { get; set; }
    public string? Description { get; set; }
    public List<TemplateComponentDto>? Components { get; set; }
}

public class OrderTemplateResult
{
    public int TemplateID { get; set; }
    public string OrderName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<TemplateComponentDto> Components { get; set; } = new();
}

public class ProductStockResult
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }
}

public class OrderStockResult
{
    public string OrderName { get; set; } = string.Empty;
    public List<OrderStockItem> Items { get; set; } = new();
}

public class OrderStockItem
{
    public string ProductName { get; set; } = string.Empty;
    public int CurrentQuantity { get; set; }
    public int QuantityRequired { get; set; }
}
