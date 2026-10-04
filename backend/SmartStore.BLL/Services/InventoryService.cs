using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SmartStore.BLL.Interfaces;
using SmartStore.DAL.Repositories;

namespace SmartStore.BLL.Services;

public class InventoryService : IInventoryService
{
    private readonly InventoryRepository _repository;
    private readonly string _groqApiKey;
    private static readonly HttpClient _httpClient = new();

    public InventoryService(InventoryRepository repository, string groqApiKey)
    {
        _repository = repository;
        _groqApiKey = groqApiKey;
    }

    // ─── PART 1: CHAT ────────────────────────────────────────────────────────────

    public async Task<ChatResponse> ProcessChatMessage(ChatRequest request)
    {
        var messages = new List<object>();
        foreach (var msg in request.ChatHistory)
        {
            var role = msg.Role == "bot" ? "assistant" : msg.Role;
            messages.Add(new { role, content = msg.Text });
        }
        messages.Add(new { role = "user", content = request.UserMessage });

        var intentResult = await DetermineUserIntent(request.UserMessage, messages);

        if (intentResult.IsNewOrder)
        {
            try
            {
                var orderResult = await ProcessIncomingOrder(intentResult.ExtractedOrderName);
                return new ChatResponse
                {
                    BotMessage = $"✅ ההזמנה \"{intentResult.ExtractedOrderName}\" עובדה בהצלחה",
                    OrderResult = orderResult,
                    IsOrderProcessing = true
                };
            }
            catch (Exception ex)
            {
                return new ChatResponse
                {
                    BotMessage = ex.Message,
                    IsOrderProcessing = false
                };
            }
        }

        return new ChatResponse
        {
            BotMessage = await GetAiFollowUpResponse(request.UserMessage, messages),
            IsOrderProcessing = false
        };
    }

    // ─── PART 1: ORDER PROCESSING ────────────────────────────────────────────────

    public async Task<OrderResult> ProcessIncomingOrder(string orderName)
    {
        var table = _repository.GetOrderComponentsByName(orderName);

        if (table.Rows.Count == 0)
        {
            var allOrders = _repository.GetAllOrderNames();
            var similar = allOrders.Where(o => o.Contains(orderName, StringComparison.OrdinalIgnoreCase)).ToList();
            var errorMsg = $"ההזמנה '{orderName}' לא נמצאה.";
            if (similar.Any()) errorMsg += $" האם התכוונת ל: {string.Join(", ", similar)}?";
            else if (allOrders.Any()) errorMsg += $" ההזמנות הזמינות: {string.Join(", ", allOrders)}";
            throw new Exception(errorMsg);
        }

        // ── STRICT BLOCK: check ALL products BEFORE touching the DB ──────────────
        foreach (System.Data.DataRow row in table.Rows)
        {
            int currentQty = Convert.ToInt32(row["CurrentQuantity"]);
            int quantityRequired = Convert.ToInt32(row["QuantityRequired"]);
            string productName = row["ProductName"].ToString()!;
            int productId = Convert.ToInt32(row["ProductID"]);

            if (currentQty < quantityRequired)
                throw new Exception(
                    $"שגיאה: '{productName}' אין מספיק מלאי (יש {currentQty}, נדרש {quantityRequired}). לא ניתן לעבד את ההזמנה.");
        }

        // ── All stock sufficient: deduct and report ───────────────────────────────
        var result = new OrderResult { OrderName = orderName };

        foreach (System.Data.DataRow row in table.Rows)
        {
            int productId = Convert.ToInt32(row["ProductID"]);
            string productName = row["ProductName"].ToString()!;
            int quantityRequired = Convert.ToInt32(row["QuantityRequired"]);
            int currentQuantity = Convert.ToInt32(row["CurrentQuantity"]);
            int minQuantity = Convert.ToInt32(row["MinQuantity"]);

            _repository.UpdateProductQuantity(productId, quantityRequired);
            int newQuantity = currentQuantity - quantityRequired;
            bool aiTriggered = currentQuantity >= minQuantity && newQuantity < minQuantity;

            result.Items.Add(new OrderItemResult
            {
                ProductName = productName,
                QuantityRemoved = quantityRequired,
                NewQuantity = newQuantity,
                AiTriggered = aiTriggered
            });

            if (aiTriggered)
            {
                var supplier = _repository.GetSupplierForProduct(productId);
                if (supplier != null)
                {
                    var (supplierName, price, purchaseUrl) = supplier.Value;
                    _repository.InsertRecommendation(productId, supplierName, price, purchaseUrl ?? string.Empty);
                    result.Recommendations.Add(new RecommendationDto
                    {
                        SupplierName = supplierName,
                        Price = price,
                        PurchaseUrl = purchaseUrl
                    });

                    // Trigger AI draft generation in the background
                    _ = Task.Run(async () => await BuildRestockDraftEmail(productId, productName));
                }
            }

            if (newQuantity < minQuantity)
                result.StockDepletionWarnings.Add(
                    $"⚠️ '{productName}' ירד מתחת לסף המינימום ({minQuantity}) לאחר עיבוד ההזמנה.");
        }

        return result;
    }

    // Builds a draft restock email using the real supplier from PurchaseRecommendations
    private async Task<string> BuildRestockDraftEmail(int productId, string productName)
    {
        var supplier = _repository.GetSupplierForProduct(productId);

        if (supplier == null)
            return "לא נמצא ספק קיים במערכת. יש להזין ספק ידנית.";

        var (supplierName, price, purchaseUrl) = supplier.Value;

        var systemPrompt = $@"אתה עוזר פנימי לניהול מלאי של חברת 'All Gift' בישראל.
כתוב טיוטת מייל מקצועית ותמציתית בעברית בלבד לספק {supplierName} לבקשת אספקה מחדש של: {productName}.
כלול: שם חומר הגלם, בקשה לכמות, מחיר עדכני (מחיר נוכחי במערכת: {price} ₪), ופרטי קשר.
החזר רק את טקסט המייל, ללא הסברים נוספים.";

        var requestMessages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = $"כתוב טיוטת מייל לספק {supplierName} לרכישת {productName}" }
        };

        var requestBody = new
        {
            model = "llama-3.3-70b-versatile",
            messages = requestMessages,
            temperature = 0.3
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _groqApiKey);

        var response = await _httpClient.SendAsync(httpRequest);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"לא ניתן היה לייצר טיוטת מייל. פנה ישירות ל-{supplierName}.";

        using var doc = JsonDocument.Parse(responseBody);
        var emailDraft = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;

        return $"📧 טיוטת מייל לספק ({supplierName}):\n{purchaseUrl}\n\n{emailDraft}";
    }

    // ─── PART 2: 3 MANAGEMENT METHODS ────────────────────────────────────────────

    public async Task<OrderTemplateResult> CreateOrderTemplate(CreateOrderTemplateRequest request)
    {
        int templateId = _repository.InsertOrderTemplate(request.OrderName, request.Description);

        foreach (var comp in request.Components)
            _repository.InsertTemplateComponent(templateId, comp.ProductID, comp.QuantityRequired);

        return await Task.FromResult(new OrderTemplateResult
        {
            TemplateID = templateId,
            OrderName = request.OrderName,
            Description = request.Description,
            Components = request.Components
        });
    }

    public async Task<OrderTemplateResult> UpdateOrderTemplate(UpdateOrderTemplateRequest request)
    {
        var existing = _repository.GetTemplateById(request.TemplateID)
            ?? throw new KeyNotFoundException($"תבנית {request.TemplateID} לא נמצאה.");

        var newName = request.OrderName ?? existing.OrderName;
        var newDesc = request.Description ?? existing.Description;

        _repository.UpdateTemplateHeader(request.TemplateID, newName, newDesc);

        List<(int ProductID, int QuantityRequired)> finalComponents;

        if (request.Components != null)
        {
            _repository.DeleteTemplateComponents(request.TemplateID);
            foreach (var comp in request.Components)
                _repository.InsertTemplateComponent(request.TemplateID, comp.ProductID, comp.QuantityRequired);
            finalComponents = request.Components.Select(c => (c.ProductID, c.QuantityRequired)).ToList();
        }
        else
        {
            finalComponents = _repository.GetTemplateComponents(request.TemplateID);
        }

        return await Task.FromResult(new OrderTemplateResult
        {
            TemplateID = request.TemplateID,
            OrderName = newName,
            Description = newDesc,
            Components = finalComponents.Select(c => new TemplateComponentDto
            {
                ProductID = c.ProductID,
                QuantityRequired = c.QuantityRequired
            }).ToList()
        });
    }

    public async Task<ProductStockResult> UpdateProductStock(int productId, int newQuantity)
    {
        var product = _repository.GetProductById(productId)
            ?? throw new KeyNotFoundException($"מוצר {productId} לא נמצא.");

        int previous = product.CurrentQuantity;
        _repository.SetProductStock(productId, newQuantity);

        return await Task.FromResult(new ProductStockResult
        {
            ProductID = productId,
            ProductName = product.Name,
            PreviousQuantity = previous,
            NewQuantity = newQuantity
        });
    }

    // ─── AI HELPERS ──────────────────────────────────────────────────────────────

    public async Task<string> RunAiPurchasingAgent(string productName)
    {
        var systemPrompt = $@"You are a B2B procurement assistant for 'All Gift', an Israeli gift company.
Your ONLY job is to find REAL Israeli B2B suppliers or distributors that sell the raw material/component: {productName}

CRITICAL INSTRUCTIONS:
1. You are assisting the store owner with INTERNAL inventory management, NOT selling to customers.
2. Focus ONLY on B2B suppliers and wholesale distributors in Israel that sell {productName}.
3. Search ONLY for Israeli companies.
4. Return ONLY a valid JSON object with REAL suppliers. DO NOT invent data.

REQUIRED JSON FORMAT:
{{
  ""suppliers"": [
    {{
      ""supplierName"": ""Company Name"",
      ""category"": ""Wholesale/Distributor/Manufacturer"",
      ""pricePerUnit"": 0,
      ""currency"": ""ILS"",
      ""purchaseUrl"": ""https://www.example.co.il"",
      ""phone"": ""+972-X-XXXXXXX"",
      ""notes"": ""Brief description""
    }}
  ]
}}

SUPPLY ONLY REAL, VERIFIABLE Israeli B2B suppliers. If none found, return empty suppliers array.";

        var requestMessages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = $"Find B2B suppliers in Israel that sell {productName}" }
        };

        var requestBody = new
        {
            model = "llama-3.3-70b-versatile",
            messages = requestMessages,
            response_format = new { type = "json_object" },
            temperature = 0.2
        };

        var json = JsonSerializer.Serialize(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _groqApiKey);

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Groq API error: {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;
    }

    private async Task ParseAndSaveRecommendation(int productId, string jsonResult)
    {
        using var doc = JsonDocument.Parse(jsonResult);
        var root = doc.RootElement;

        string supplierName = root.GetProperty("SupplierName").GetString() ?? "Unknown";
        decimal price = root.GetProperty("Price").GetDecimal();
        string purchaseUrl = root.GetProperty("PurchaseUrl").GetString() ?? "";

        await Task.Run(() => _repository.InsertRecommendation(productId, supplierName, price, purchaseUrl));
    }

    private async Task<IntentResult> DetermineUserIntent(string userMessage, List<object> messages)
    {
        var allOrders = _repository.GetAllOrderNames();
        var ordersJson = JsonSerializer.Serialize(allOrders);

        var systemPrompt = $@"You are a chat assistant for an inventory management system called 'All Gift'.
Determine if the user's message is trying to process a new order or asking a follow-up question.

Available orders: {ordersJson}

Respond ONLY with this JSON:
{{
  ""isNewOrder"": true/false,
  ""extractedOrderName"": ""closest match from available orders or empty string"",
  ""explanation"": ""brief explanation""
}}";

        var requestMessages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userMessage }
        };

        var requestBody = new
        {
            model = "llama-3.1-8b-instant",
            messages = requestMessages,
            response_format = new { type = "json_object" },
            temperature = 0.3
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _groqApiKey);

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Groq API error: {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;

        using var intentDoc = JsonDocument.Parse(content);
        var intentRoot = intentDoc.RootElement;

        return new IntentResult
        {
            IsNewOrder = intentRoot.GetProperty("isNewOrder").GetBoolean(),
            ExtractedOrderName = intentRoot.GetProperty("extractedOrderName").GetString() ?? ""
        };
    }

    private async Task<string> GetAiFollowUpResponse(string userMessage, List<object> messages)
    {
        var systemPrompt = @"אתה עוזר פנימי לניהול מלאי של 'All Gift' - חברת מארזי מתנה בישראל.
אתה עובד אך ורק מול צוות העובדים הפנימי.

כללים מחמירים:
1. אתה מטפל אך ורק בנושאים הקשורים ל: מלאי, הזמנות, מוצרים, ספקים וניהול החנות.
2. אם נשאלת שאלה שאינה קשורה לחנות, הזמנות או מלאי - ענה בדיוק: 'אני יכול לעזור רק בנושאים הקשורים לחנות, הזמנות ומלאי 🏪'
3. אינך צ'אטבוט שירות לקוחות. אינך מוכר ללקוחות קצה.
4. דבר בעברית בלבד.
5. תגובות קצרות, מקצועיות וממוקדות בלבד.";

        var requestMessages = new List<object> { new { role = "system", content = systemPrompt } };
        requestMessages.AddRange(messages);

        var requestBody = new
        {
            model = "llama-3.1-8b-instant",
            messages = requestMessages,
            temperature = 0.7
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _groqApiKey);

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Groq API error: {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "לא הצלחתי להבין את הבקשה.";
    }

    private class IntentResult
    {
        public bool IsNewOrder { get; set; }
        public string ExtractedOrderName { get; set; } = string.Empty;
    }

    // ─── INVENTORY STATUS ────────────────────────────────────────────────────────

    public Task<string> GetInventoryStatus()
    {
        var products = _repository.GetAllProductsStock();
        if (!products.Any())
            return Task.FromResult("אין מוצרים במלאי.");

        var lines = products.Select(p =>
        {
            var status = p.CurrentQuantity <= 0 ? "❌ אזל" :
                         p.CurrentQuantity < p.MinQuantity ? "⚠️ נמוך" : "✅ תקין";
            return $"{p.Name}: {p.CurrentQuantity} יחידות {status} (מינימום: {p.MinQuantity})";
        });

        return Task.FromResult("📦 מצב המלאי הנוכחי:\n" + string.Join("\n", lines));
    }

    public OrderStockResult GetOrderStock(string orderName)
    {
        var table = _repository.GetOrderComponentsByName(orderName);
        if (table.Rows.Count == 0)
        {
            var allOrders = _repository.GetAllOrderNames();
            var similar = allOrders.Where(o => o.Contains(orderName, StringComparison.OrdinalIgnoreCase)).ToList();
            var msg = $"הזמנה '{orderName}' לא נמצאה.";
            if (similar.Any()) msg += $" האם התכוונת ל: {string.Join(", ", similar)}?";
            throw new Exception(msg);
        }

        var result = new OrderStockResult { OrderName = orderName };
        foreach (System.Data.DataRow row in table.Rows)
        {
            result.Items.Add(new OrderStockItem
            {
                ProductName = row["ProductName"].ToString()!,
                CurrentQuantity = Convert.ToInt32(row["CurrentQuantity"]),
                QuantityRequired = Convert.ToInt32(row["QuantityRequired"])
            });
        }
        return result;
    }

    // ─── IMAGE PROCESSING ────────────────────────────────────────────────────────

    public async Task<ChatResponse> ProcessImageMessage(string base64Image, string mimeType, string? fileName = null)
    {
        var allOrders = _repository.GetAllOrderNames();

        // Step 1: try to match by file name first (fast, no AI needed)
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var nameWithoutExt = System.Text.RegularExpressions.Regex
                .Replace(Path.GetFileNameWithoutExtension(fileName).Trim(), @"\s+", " ");

            var fileMatch = allOrders.FirstOrDefault(o =>
            {
                var normalized = System.Text.RegularExpressions.Regex.Replace(o.Trim(), @"\s+", " ");
                return normalized.Equals(nameWithoutExt, StringComparison.OrdinalIgnoreCase) ||
                       normalized.Contains(nameWithoutExt, StringComparison.OrdinalIgnoreCase) ||
                       nameWithoutExt.Contains(normalized, StringComparison.OrdinalIgnoreCase);
            });

            if (fileMatch != null)
            {
                try
                {
                    var orderResult = await ProcessIncomingOrder(fileMatch);
                    return new ChatResponse
                    {
                        BotMessage = $"🖼️ זיהיתי לפי שם הקובץ: '{nameWithoutExt}'\n✅ עיבדתי את ההזמנה \"{fileMatch}\" בהתאם",
                        OrderResult = orderResult,
                        IsOrderProcessing = true
                    };
                }
                catch (Exception ex)
                {
                    return new ChatResponse { BotMessage = $"🖼️ זיהיתי: '{nameWithoutExt}'\n❌ {ex.Message}" };
                }
            }

            var ordersList = string.Join("\n", allOrders.Select(o => $"• {o.Trim()}"));
            return new ChatResponse
            {
                BotMessage = $"🖼️ קיבלתי את התמונה '{nameWithoutExt}', אך לא מצאתי הזמנה תואמת.\n\nהזמנות קיימות במערכת:\n{ordersList}\n\nכתוב את שם ההזמנה המדויק לעיבוד."
            };
        }

        var allOrdersList = string.Join("\n", allOrders.Select(o => $"• {o.Trim()}"));
        return new ChatResponse
        {
            BotMessage = $"🖼️ לא צורף שם קובץ. הזמנות קיימות:\n{allOrdersList}\n\nכתוב את שם ההזמנה לעיבוד."
        };
    }
}
