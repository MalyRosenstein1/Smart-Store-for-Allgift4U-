using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartStore.BLL.Interfaces;
using SmartStore.DAL.Repositories;

namespace SmartStore.BLL.Services;

public class InventoryService : IInventoryService
{
    private const string DeliveryNoteStoreName = "Allgift4U";
    private static readonly string ArialFontPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
        "arial.ttf");

    private readonly InventoryRepository _repository;
    private readonly string _groqApiKey;
    private static readonly HttpClient _httpClient = new();
    private static readonly IReadOnlyDictionary<string, string> ProductNameAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["בירה"] = "בירה בוטל",
        ["בקבוק בירה"] = "בירה בוטל",
        ["כוס"] = "כוס חד פעמית",
        ["בר שוקולד"] = "חטיף שוקולד",
        ["ברים שוקולד"] = "חטיף שוקולד",
        ["חטיף שוקולד"] = "חטיף שוקולד",
        ["אריזה"] = "אריזת מתנה",
        ["קופסה"] = "קופסת מתנה",
        ["ברכה"] = "ברכת מזל טוב",
        ["צלופן"] = "שקית צלופן"
    };

    static InventoryService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        RegisterDeliveryNoteFont();
    }

    public InventoryService(InventoryRepository repository, string groqApiKey)
    {
        _repository = repository;
        _groqApiKey = groqApiKey;
    }

    private static void RegisterDeliveryNoteFont()
    {
        if (!File.Exists(ArialFontPath))
            return;

        try
        {
            FontManager.RegisterFont(File.OpenRead(ArialFontPath));
        }
        catch
        {
        }
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
                    BotMessage = AppendOrderDetails($"✅ ההזמנה \"{intentResult.ExtractedOrderName}\" עובדה בהצלחה", orderResult),
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

        if (!string.IsNullOrWhiteSpace(intentResult.StockUpdateCommand))
        {
            try
            {
                var stockResult = await ProcessStockUpdateFromCommand(intentResult.StockUpdateCommand);
                var stockUpdateOperator = string.IsNullOrWhiteSpace(intentResult.StockUpdateOperator)
                    ? ParseStockUpdateCommand(intentResult.StockUpdateCommand)?.Operator ?? ""
                    : intentResult.StockUpdateOperator;
                var stockUpdateQuantity = intentResult.StockUpdateQuantity > 0
                    ? intentResult.StockUpdateQuantity
                    : ParseStockUpdateCommand(intentResult.StockUpdateCommand)?.Quantity ?? 0;
                var action = stockUpdateOperator switch
                {
                    "+" => $"הוספו {stockUpdateQuantity}, סה\"כ",
                    "-" => $"הופחתו {stockUpdateQuantity}, סה\"כ",
                    _ => "נקבע"
                };

                return new ChatResponse
                {
                    BotMessage = $"✅ המלאי של \"{stockResult.ProductName}\" {action} {stockResult.NewQuantity} יחידות",
                    StockResult = stockResult,
                    IsOrderProcessing = false
                };
            }
            catch (KeyNotFoundException ex)
            {
                return new ChatResponse
                {
                    BotMessage = ex.Message,
                    IsOrderProcessing = false
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
        var sanitizedOrderName = SanitizeOrderName(orderName);
        var allOrders = _repository.GetAllOrderNames();
        var matchedOrder = allOrders.FirstOrDefault(o =>
            SanitizeOrderName(o).Equals(sanitizedOrderName, StringComparison.OrdinalIgnoreCase));

        if (matchedOrder == null && sanitizedOrderName.StartsWith("מארז ", StringComparison.OrdinalIgnoreCase))
        {
            var orderNameWithoutPrefix = sanitizedOrderName.Substring("מארז ".Length).Trim();
            matchedOrder = allOrders.FirstOrDefault(o =>
                SanitizeOrderName(o).Equals(orderNameWithoutPrefix, StringComparison.OrdinalIgnoreCase));
        }

        var lookupOrderName = matchedOrder ?? sanitizedOrderName;
        var displayOrderName = SanitizeOrderName(matchedOrder ?? orderName);
        var table = _repository.GetOrderComponentsByName(lookupOrderName);

        if (table.Rows.Count == 0)
        {
            var sanitizedAllOrders = allOrders
                .Select(SanitizeOrderName)
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var similar = sanitizedAllOrders
                .Where(o => o.Contains(sanitizedOrderName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var errorMsg = $"ההזמנה '{displayOrderName}' לא נמצאה.";
            if (similar.Any()) errorMsg += $" האם התכוונת ל: {string.Join(", ", similar)}?";
            else if (sanitizedAllOrders.Any()) errorMsg += $" ההזמנות הזמינות: {string.Join(", ", sanitizedAllOrders)}";
            throw new Exception(errorMsg);
        }

        // ── STRICT BLOCK: check ALL products BEFORE touching the DB ──────────────
        foreach (System.Data.DataRow row in table.Rows)
        {
            int currentQty = Convert.ToInt32(row["CurrentQuantity"]);
            int quantityRequired = Convert.ToInt32(row["QuantityRequired"]);
            string productName = row["ProductName"].ToString()!;

            if (currentQty < quantityRequired)
            {
                var recommendations = FindAvailableAlternativeBundles(displayOrderName)
                    .Take(2)
                    .ToList();

                throw new Exception(BuildFallbackRecommendationMessage(displayOrderName, productName, recommendations));
            }
        }

        // ── All stock sufficient: build delivery note, then deduct and report ─────
        var result = new OrderResult { OrderName = displayOrderName };
        var itemResults = table.Rows
            .Cast<System.Data.DataRow>()
            .Select(row => new OrderItemResult
            {
                ProductName = row["ProductName"].ToString()!,
                QuantityRemoved = Convert.ToInt32(row["QuantityRequired"]),
                NewQuantity = 0,
                AiTriggered = false
            })
            .ToList();

        result.Items.AddRange(itemResults);

        try
        {
            result.DeliveryNoteFileName = BuildDeliveryNoteFileName(result.OrderName);
            result.DeliveryNotePdf = BuildDeliveryNotePdf(result, result.DeliveryNoteFileName);
        }
        catch
        {
            result.DeliveryNotePdf = Array.Empty<byte>();
            result.DeliveryNoteFileName = string.Empty;
            throw;
        }

        for (var index = 0; index < table.Rows.Count; index++)
        {
            var row = table.Rows[index];
            int productId = Convert.ToInt32(row["ProductID"]);
            string productName = row["ProductName"].ToString()!;
            int quantityRequired = Convert.ToInt32(row["QuantityRequired"]);
            int currentQuantity = Convert.ToInt32(row["CurrentQuantity"]);
            int minQuantity = Convert.ToInt32(row["MinQuantity"]);

            _repository.UpdateProductQuantity(productId, quantityRequired);
            int newQuantity = currentQuantity - quantityRequired;
            bool aiTriggered = currentQuantity >= minQuantity && newQuantity < minQuantity;
            bool needsRestockDraft = newQuantity == 0 || aiTriggered;

            itemResults[index].NewQuantity = newQuantity;
            itemResults[index].AiTriggered = needsRestockDraft;

            if (needsRestockDraft)
            {
                var supplier = await FindOrCreateSupplier(productId, productName);
                if (supplier != null)
                {
                    var draftEmail = await BuildRestockDraftEmail(productId, productName, supplier);
                    result.EmailDrafts.Add(draftEmail);
                }
            }

            if (newQuantity == 0)
                result.StockDepletionWarnings.Add(
                    $"⚠️ '{productName}' הגיע לאפס.{BuildAlternativeSuggestion(productName, FindAlternativeProduct(productId, productName))}");
            else if (newQuantity < minQuantity)
                result.StockDepletionWarnings.Add(
                    $"⚠️ '{productName}' ירד מתחת לסף המינימום ({minQuantity}) לאחר עיבוד ההזמנה.");
        }

        return result;
    }

    private static byte[] BuildDeliveryNotePdf(OrderResult result, string deliveryNoteFileName)
    {
        var transactionDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.CreateSpecificCulture("he-IL"));
        var fileName = Path.GetFileNameWithoutExtension(deliveryNoteFileName);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style
                    .FontFamily("Arial")
                    .FontSize(10)
                    .FontColor(Colors.Black));

                page.Header()
                    .AlignCenter()
                    .Text("Allgift4U")
                    .FontSize(22)
                    .SemiBold()
                    .FontColor(Colors.Blue.Darken2);

                page.Content()
                    .PaddingVertical(20)
                    .Column(column =>
                    {
                        column.Spacing(16);

                        column.Item()
                            .AlignCenter()
                            .Text("תעודת משלוח / Delivery Note")
                            .FontSize(18)
                            .SemiBold();

                        column.Item().Table(details =>
                        {
                            details.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(140);
                                columns.RelativeColumn();
                            });

                            AddDetailRow(details, "Store Name", DeliveryNoteStoreName);
                            AddDetailRow(details, "Order Details", result.OrderName);
                            AddDetailRow(details, "Date & Time", transactionDate);
                        });

                        column.Item()
                            .PaddingTop(8)
                            .Text("Items Breakdown")
                            .FontSize(14)
                            .SemiBold();

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.ConstantColumn(80);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Product");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Quantity");
                            });

                            foreach (var item in result.Items)
                            {
                                table.Cell().Element(BodyCellStyle).Text(item.ProductName);
                                table.Cell().Element(BodyCellStyle).AlignRight().Text(item.QuantityRemoved.ToString(CultureInfo.InvariantCulture));
                            }
                        });

                        column.Item()
                            .PaddingTop(24)
                            .AlignCenter()
                            .Text("Thank you for your order")
                            .FontSize(11)
                            .FontColor(Colors.Grey.Darken2);
                    });

                page.Footer()
                    .AlignCenter()
                    .PaddingBottom(10)
                    .Text(text =>
                    {
                        text.Span($"{fileName} - ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
            });
        }).GeneratePdf();
    }

    private static void AddDetailRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(BodyCellStyle).Text(label).Bold();
        table.Cell().Element(BodyCellStyle).Text(value);
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container
            .Border(0.75f)
            .BorderColor(Colors.Grey.Lighten1)
            .Background(Colors.Grey.Lighten4)
            .Padding(8);
    }

    private static IContainer BodyCellStyle(IContainer container)
    {
        return container
            .Border(0.75f)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(8);
    }

    private static string BuildDeliveryNoteFileName(string orderName)
    {
        var safeOrderName = string.Join("_", SanitizeOrderName(orderName)
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray()).Trim('_');

        if (string.IsNullOrWhiteSpace(safeOrderName))
            safeOrderName = "order";

        return $"{safeOrderName}_delivery_note_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
    }

    private async Task<ProductStockResult> ProcessStockUpdateFromCommand(string command)
    {
        var parsed = ParseStockUpdateCommand(command);
        if (parsed == null)
            throw new Exception("פורמט לא תקין. השתמש ב: +20 שם מוצר, -20 שם מוצר או =20 שם מוצר.");

        var products = _repository.GetAllProductsStock();
        var product = FindProductByName(products, parsed.ProductName);
        if (product == null)
            throw new KeyNotFoundException($"המוצר '{parsed.ProductName}' לא נמצא במערכת.");

        int newQuantity = parsed.Operator switch
        {
            "+" => product.Value.CurrentQuantity + parsed.Quantity,
            "-" => Math.Max(0, product.Value.CurrentQuantity - parsed.Quantity),
            "=" => parsed.Quantity,
            _ => throw new Exception("פורמט לא תקין. השתמש ב: +20 שם מוצר, -20 שם מוצר או =20 שם מוצר.")
        };

        return await UpdateProductStock(product.Value.ProductID, newQuantity);
    }

    private (int ProductID, string Name, int CurrentQuantity, int MinQuantity)? FindProductByName(
        IEnumerable<(int ProductID, string Name, int CurrentQuantity, int MinQuantity)> products,
        string productName)
    {
        var normalizedProductName = NormalizeProductText(productName);
        var canonicalProductName = GetCanonicalProductName(normalizedProductName);

        var exactProduct = products.FirstOrDefault(product =>
            NormalizeProductText(product.Name).Equals(canonicalProductName, StringComparison.OrdinalIgnoreCase));

        if (!exactProduct.Equals(default))
            return exactProduct;

        if (!canonicalProductName.Equals(normalizedProductName, StringComparison.OrdinalIgnoreCase))
        {
            exactProduct = products.FirstOrDefault(product =>
                NormalizeProductText(product.Name).Equals(normalizedProductName, StringComparison.OrdinalIgnoreCase));

            if (!exactProduct.Equals(default))
                return exactProduct;
        }

        if (normalizedProductName.Length < 3)
            return null;

        var storedNameContainsInput = products
            .Where(product => NormalizeProductText(product.Name).Contains(normalizedProductName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (storedNameContainsInput.Count == 1)
            return storedNameContainsInput[0];

        var inputContainsStoredName = products
            .Where(product => normalizedProductName.Contains(NormalizeProductText(product.Name), StringComparison.OrdinalIgnoreCase))
            .ToList();

        return inputContainsStoredName.Count == 1
            ? inputContainsStoredName[0]
            : null;
    }

    private string GetCanonicalProductName(string normalizedProductName)
    {
        return ProductNameAliases.TryGetValue(normalizedProductName, out var canonicalName)
            ? canonicalName
            : normalizedProductName;
    }

    private string BuildAlternativeSuggestion(string productName, (int ProductID, string Name, int CurrentQuantity, int MinQuantity)? alternative)
    {
        if (!alternative.HasValue)
            return " אין מוצר חלופי זמין במלאי.";

        return $" אפשר להשתמש ב'{alternative.Value.Name}' שנמצא במלאי ({alternative.Value.CurrentQuantity} יחידות).";
    }

    private async Task<RestockSupplier?> FindOrCreateSupplier(int productId, string productName)
    {
        var existingSupplier = _repository.GetSupplierForProduct(productId);
        if (existingSupplier != null)
        {
            var (supplierName, price, purchaseUrl) = existingSupplier.Value;
            return new RestockSupplier
            {
                Name = supplierName,
                Price = price,
                PurchaseUrl = purchaseUrl ?? string.Empty
            };
        }

        try
        {
            var agentResponse = await RunAiPurchasingAgent(productName);
            var supplier = ParseFirstSupplierFromAgentResponse(agentResponse);
            if (supplier == null)
                return null;

            _repository.InsertRecommendation(productId, supplier.Name, supplier.Price, supplier.PurchaseUrl);
            return supplier;
        }
        catch
        {
            return null;
        }
    }

    private RestockSupplier? ParseFirstSupplierFromAgentResponse(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            if (!doc.RootElement.TryGetProperty("suppliers", out var suppliersProp) ||
                suppliersProp.ValueKind != JsonValueKind.Array ||
                suppliersProp.GetArrayLength() == 0)
                return null;

            var supplierProp = suppliersProp[0];
            var supplierName = supplierProp.TryGetProperty("supplierName", out var nameProp)
                ? nameProp.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(supplierName))
                return null;

            var price = supplierProp.TryGetProperty("pricePerUnit", out var priceProp) && priceProp.TryGetDecimal(out var parsedPrice)
                ? parsedPrice
                : 0m;

            var purchaseUrl = supplierProp.TryGetProperty("purchaseUrl", out var urlProp)
                ? urlProp.GetString() ?? string.Empty
                : string.Empty;

            return new RestockSupplier
            {
                Name = supplierName,
                Price = price,
                PurchaseUrl = purchaseUrl
            };
        }
        catch
        {
            return null;
        }
    }

    private sealed class RestockSupplier
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string PurchaseUrl { get; set; } = string.Empty;
    }

    private (int ProductID, string Name, int CurrentQuantity, int MinQuantity)? FindAlternativeProduct(int? productId, string productName)
    {
        var normalizedProductName = NormalizeProductText(productName);
        return _repository.GetAllProductsStock()
            .Where(product =>
                (!productId.HasValue || product.ProductID != productId.Value) &&
                product.CurrentQuantity > 0 &&
                !NormalizeProductText(product.Name).Equals(normalizedProductName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(product => GetAlternativeScore(normalizedProductName, NormalizeProductText(product.Name)))
            .ThenByDescending(product => product.CurrentQuantity)
            .FirstOrDefault();
    }

    private List<string> FindAvailableAlternativeBundles(string requestedOrderName)
    {
        var requested = SanitizeOrderName(requestedOrderName);
        var allOrders = _repository.GetAllOrderNames()
            .Select(SanitizeOrderName)
            .Where(order => !string.IsNullOrWhiteSpace(order))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var availableBundles = new List<string>();

        foreach (var orderName in allOrders)
        {
            if (orderName.Equals(requested, StringComparison.OrdinalIgnoreCase))
                continue;

            var table = _repository.GetOrderComponentsByName(orderName);
            if (table.Rows.Count == 0)
                continue;

            if (IsBundleFullyAvailable(table))
                availableBundles.Add(orderName);
        }

        return availableBundles;
    }

    private bool IsBundleFullyAvailable(System.Data.DataTable table)
    {
        foreach (System.Data.DataRow row in table.Rows)
        {
            var currentQuantity = Convert.ToInt32(row["CurrentQuantity"]);
            var quantityRequired = Convert.ToInt32(row["QuantityRequired"]);

            if (currentQuantity < quantityRequired)
                return false;
        }

        return true;
    }

    private string BuildFallbackRecommendationMessage(string requestedOrderName, string missingProductName, List<string> recommendations)
    {
        var firstLine = $"❌ לא ניתן להשלים את ההזמנה של '{requestedOrderName}' בשל מחסור ב-{missingProductName}.";

        if (recommendations.Count >= 2)
        {
            return $"{firstLine}\n✨ אולי יעניין אותך במקום: {recommendations[0]} או {recommendations[1]} הזמינות כעת במלאי במלואן!";
        }

        if (recommendations.Count == 1)
            return $"{firstLine}\n✨ אולי יעניין אותך במקום: {recommendations[0]} הזמינה כעת במלאי במלואה!";

        return $"{firstLine}\n✨ לא נמצאו מארזים חלופיים הזמינים במלאי כרגע.";
    }

    private int GetAlternativeScore(string normalizedProductName, string normalizedCandidateName)
    {
        if (normalizedProductName.Contains("שוקולד") && normalizedCandidateName.Contains("שוקולד"))
            return 100;

        var nutsKeywords = new[] { "פיצוח", "אגוז", "בוטן", "שקד" };
        if (nutsKeywords.Any(keyword => normalizedProductName.Contains(keyword)) &&
            nutsKeywords.Any(keyword => normalizedCandidateName.Contains(keyword)))
            return 90;

        if (normalizedProductName.Contains("ברכה") && normalizedCandidateName.Contains("ברכה"))
            return 85;

        var packagingKeywords = new[] { "אריז", "קופס", "שקית", "סרט", "נייר" };
        if (packagingKeywords.Any(keyword => normalizedProductName.Contains(keyword)) &&
            packagingKeywords.Any(keyword => normalizedCandidateName.Contains(keyword)))
            return 80;

        if (normalizedProductName.Contains("בירה") && normalizedCandidateName.Contains("בירה"))
            return 75;

        return 1;
    }

    private sealed class ParsedStockUpdateCommand
    {
        public string Operator { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string ProductName { get; set; } = string.Empty;
    }

    private ParsedStockUpdateCommand? ParseStockUpdateCommand(string command)
    {
        var cleanedCommand = (command ?? string.Empty)
            .Replace("**", string.Empty)
            .Replace("__", string.Empty)
            .Trim();
        var normalized = NormalizeProductText(cleanedCommand);

        var prefixMatch = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"(?:^|\s)([+\-=])\s*(\d+)\s*(.+)$",
            System.Text.RegularExpressions.RegexOptions.None);

        if (prefixMatch.Success)
        {
            return CreateParsedStockUpdate(prefixMatch.Groups[1].Value, prefixMatch.Groups[2].Value, prefixMatch.Groups[3].Value);
        }

        var addMatch = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"^(?:הוסף|הוספת|להוסיף|הוספה)\s+(\d+)\s+(.+)$");
        if (addMatch.Success)
        {
            return CreateParsedStockUpdate("+", addMatch.Groups[1].Value, addMatch.Groups[2].Value);
        }

        var subtractMatch = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"^(?:הפחת|הפחתי|להפחית|הפחתה|החסר|הסר)\s+(\d+)\s+(.+)$");
        if (subtractMatch.Success)
        {
            return CreateParsedStockUpdate("-", subtractMatch.Groups[1].Value, subtractMatch.Groups[2].Value);
        }

        var setMatch = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"^(?:קבע|לקבוע|שנה ל|לעדכן ל)\s+(\d+)\s+(.+)$");
        if (setMatch.Success)
        {
            return CreateParsedStockUpdate("=", setMatch.Groups[1].Value, setMatch.Groups[2].Value);
        }

        return null;
    }

    private ParsedStockUpdateCommand CreateParsedStockUpdate(string op, string quantityText, string productName)
    {
        return new ParsedStockUpdateCommand
        {
            Operator = op,
            Quantity = int.Parse(quantityText),
            ProductName = productName.Trim().Trim('.', ':', '!', '?')
        };
    }

    private string NormalizeProductText(string value)
    {
        var text = (value ?? string.Empty)
            .Normalize(System.Text.NormalizationForm.FormKD)
            .Replace("ך", "כ")
            .Replace("ם", "מ")
            .Replace("ן", "נ")
            .Replace("ף", "פ")
            .Replace("ץ", "צ")
            .Replace("\u200E", string.Empty)
            .Replace("\u200F", string.Empty);

        return System.Text.RegularExpressions.Regex.Replace(
            text,
            @"[\u0591-\u05BD\u05BF\u05C1-\u05C2\u05C4-\u05C5\u05C7]|\s+",
            match => match.Value.All(char.IsWhiteSpace) ? " " : string.Empty)
            .Trim()
            .ToLowerInvariant();
    }

    // Builds a draft restock email using an existing supplier or the AI purchasing agent.
    private async Task<string> BuildRestockDraftEmail(int productId, string productName, RestockSupplier? supplierOverride = null)
    {
        var supplier = supplierOverride ?? await FindOrCreateSupplier(productId, productName);

        if (supplier == null)
            return "לא נמצא ספק קיים במערכת ולא הצלחתי למצוא ספק חדש אוטומטית. יש להזין ספק ידנית.";

        var systemPrompt = $@"אתה עוזר פנימי לניהול מלאי של חברת 'All Gift' בישראל.
כתוב טיוטת מייל מקצועית ותמציתית בעברית בלבד לספק {supplier.Name} לבקשת אספקה מחדש של: {productName}.
כלול: שם חומר הגלם, בקשה לכמות, מחיר עדכני (מחיר נוכחי במערכת: {supplier.Price} ₪), ופרטי קשר.
החזר רק את טקסט המייל, ללא הסברים נוספים.";

        var requestMessages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = $"כתוב טיוטת מייל לספק {supplier.Name} לרכישת {productName}" }
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
            return $"לא ניתן היה לייצר טיוטת מייל. פנה ישירות ל-{supplier.Name}.";

        using var doc = JsonDocument.Parse(responseBody);
        var emailDraft = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;

        return $"📧 טיוטת מייל לספק ({supplier.Name}):\n{supplier.PurchaseUrl}\n\n{emailDraft}";
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
            .GetString()!;
    }

    private async Task<IntentResult> DetermineUserIntent(string userMessage, List<object> messages)
    {
        var allOrders = _repository.GetAllOrderNames()
            .Select(SanitizeOrderName)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var ordersJson = JsonSerializer.Serialize(allOrders);
        var sanitizedUserMessage = SanitizeOrderName(userMessage);

        var systemPrompt = $@"You are a chat intent classifier for an inventory management system called 'All Gift'.
Your only job is to classify the user's message into one of these supported intents:
1. Process a new order.
2. Update product stock from chat.
3. Ask a follow-up question.

Available exact order names: {ordersJson}

CRITICAL OUTPUT RULES:
- Return ONLY valid JSON. Do not return markdown, bullet points, quotes around the JSON, explanations, or conversational Hebrew text.
- For a new order, set isNewOrder to true, set extractedOrderName to the exact full order name from the available exact order names, and set stockUpdateCommand and stockUpdateOperator to empty strings.
- For a stock update, set isNewOrder to false, set extractedOrderName to an empty string, and set stockUpdateCommand to a strict machine-readable command only.
- For stock updates, stockUpdateCommand must be only one of these formats: '+[quantity] [product_name]', '-[quantity] [product_name]', or '=[quantity] [product_name]'. Example: '+20 שוקולד מריר'.
- Do not include words like 'עדכון מלאי', 'להוסיף כמות', 'כתוב', markdown bold markers, punctuation, or any wrapper around stockUpdateCommand.
- Set stockUpdateOperator to '+', '-', or '=' for stock updates, or empty string otherwise.
- The user message and product names may be in Hebrew. Do not translate Hebrew product names. Keep the product name exactly as the user wrote it, except for harmless whitespace cleanup.
- For Hebrew order names, extractedOrderName must be the exact full string from the available exact order names after trimming whitespace and removing carriage returns/newlines.
- Sanitize both the user's input and the available order names before comparing: `.Trim().Replace(""\r"", """").Replace(""\n"", """")`.
- Use case-insensitive matching with `StringComparison.OrdinalIgnoreCase`.
- If the message is not clearly a new order or a stock update, return a follow-up intent: isNewOrder=false, extractedOrderName='', stockUpdateCommand='', stockUpdateOperator=''.

Respond ONLY with this JSON:
{{
  ""isNewOrder"": true,
  ""extractedOrderName"": ""exact full order name or empty string"",
  ""stockUpdateCommand"": ""+[quantity] [product_name], -[quantity] [product_name], =[quantity] [product_name], or empty string"",
  ""stockUpdateOperator"": ""+ or - or = or empty string"",
  ""explanation"": ""empty string""
}}";

        var requestMessages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = sanitizedUserMessage }
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

        try
        {
            using var intentDoc = JsonDocument.Parse(content);
            var intentRoot = intentDoc.RootElement;

            var stockUpdateOperator = intentRoot.TryGetProperty("stockUpdateOperator", out var stockUpdateOperatorProp)
                ? stockUpdateOperatorProp.GetString() ?? ""
                : "";
            var stockUpdateCommand = intentRoot.TryGetProperty("stockUpdateCommand", out var stockUpdateCommandProp)
                ? stockUpdateCommandProp.GetString() ?? ""
                : "";
            var parsedStockUpdate = ParseStockUpdateCommand(stockUpdateCommand);
            var extractedOrderName = intentRoot.TryGetProperty("extractedOrderName", out var extractedOrderNameProp)
                ? SanitizeOrderName(extractedOrderNameProp.GetString() ?? "")
                : "";
            var sanitizedExtractedOrderName = SanitizeOrderName(extractedOrderName);
            var matchedAvailableOrder = allOrders.FirstOrDefault(order =>
                SanitizeOrderName(order).Equals(sanitizedExtractedOrderName, StringComparison.OrdinalIgnoreCase));

            return new IntentResult
            {
                IsNewOrder = intentRoot.TryGetProperty("isNewOrder", out var isNewOrderProp) && isNewOrderProp.GetBoolean(),
                ExtractedOrderName = matchedAvailableOrder ?? sanitizedExtractedOrderName,
                StockUpdateCommand = stockUpdateCommand,
                StockUpdateOperator = stockUpdateOperator,
                StockUpdateQuantity = parsedStockUpdate?.Quantity ?? 0
            };
        }
        catch
        {
            return ParseLooseIntentContent(content, allOrders);
        }
    }

    private static string SanitizeOrderName(string value)
    {
        return (value ?? string.Empty).Trim().Replace("\r", "").Replace("\n", "");
    }

    private IntentResult ParseLooseIntentContent(string content, List<string> allOrders)
    {
        var stockUpdate = ParseStockUpdateCommand(content);
        if (stockUpdate != null)
        {
            return new IntentResult
            {
                IsNewOrder = false,
                ExtractedOrderName = string.Empty,
                StockUpdateCommand = $"{stockUpdate.Operator}{stockUpdate.Quantity} {stockUpdate.ProductName}",
                StockUpdateOperator = stockUpdate.Operator,
                StockUpdateQuantity = stockUpdate.Quantity
            };
        }

        var normalizedUserText = SanitizeOrderName(content);
        var matchedOrder = allOrders.FirstOrDefault(order =>
            SanitizeOrderName(order).Equals(normalizedUserText, StringComparison.OrdinalIgnoreCase));

        return new IntentResult
        {
            IsNewOrder = matchedOrder != null,
            ExtractedOrderName = SanitizeOrderName(matchedOrder ?? string.Empty),
            StockUpdateCommand = string.Empty,
            StockUpdateOperator = string.Empty
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
        public string StockUpdateCommand { get; set; } = string.Empty;
        public string StockUpdateOperator { get; set; } = string.Empty;
        public int StockUpdateQuantity { get; set; }
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
        var sanitizedOrderName = SanitizeOrderName(orderName);
        var allOrders = _repository.GetAllOrderNames();
        var matchedOrder = allOrders.FirstOrDefault(o =>
            SanitizeOrderName(o).Equals(sanitizedOrderName, StringComparison.OrdinalIgnoreCase));

        if (matchedOrder == null && sanitizedOrderName.StartsWith("מארז ", StringComparison.OrdinalIgnoreCase))
        {
            var orderNameWithoutPrefix = sanitizedOrderName.Substring("מארז ".Length).Trim();
            matchedOrder = allOrders.FirstOrDefault(o =>
                SanitizeOrderName(o).Equals(orderNameWithoutPrefix, StringComparison.OrdinalIgnoreCase));
        }

        var lookupOrderName = matchedOrder ?? sanitizedOrderName;
        var displayOrderName = SanitizeOrderName(matchedOrder ?? orderName);
        var table = _repository.GetOrderComponentsByName(lookupOrderName);
        if (table.Rows.Count == 0)
        {
            var sanitizedAllOrders = allOrders
                .Select(SanitizeOrderName)
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var similar = sanitizedAllOrders
                .Where(o => o.Contains(sanitizedOrderName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var msg = $"הזמנה '{displayOrderName}' לא נמצאה.";
            if (similar.Any()) msg += $" האם התכוונת ל: {string.Join(", ", similar)}?";
            throw new Exception(msg);
        }

        var result = new OrderStockResult { OrderName = displayOrderName };
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

    private string AppendOrderDetails(string message, OrderResult orderResult)
    {
        var details = new List<string>();

        if (orderResult.StockDepletionWarnings.Any())
            details.Add(string.Join("\n", orderResult.StockDepletionWarnings));

        if (orderResult.EmailDrafts.Any())
            details.Add(string.Join("\n\n", orderResult.EmailDrafts));

        return details.Any()
            ? $"{message}\n\n{string.Join("\n\n", details)}"
            : message;
    }

    // ─── IMAGE PROCESSING ────────────────────────────────────────────────────────

    public async Task<ChatResponse> ProcessImageMessage(string base64Image, string mimeType, string? fileName = null)
    {
        var imageLabel = string.IsNullOrWhiteSpace(fileName) ? "התמונה" : $"'{Path.GetFileNameWithoutExtension(fileName)}'";

        try
        {
            var detectedComponents = await IdentifyComponentsFromImage(base64Image, mimeType);
            if (detectedComponents.Count > 0)
            {
                var lines = detectedComponents
                    .Select(c => $"• {c.ProductName} — {c.Quantity} יחידות")
                    .ToList();
                var matchedOrder = FindMatchingOrderByDetectedComponents(detectedComponents);
                if (matchedOrder != null)
                {
                    try
                    {
                        var orderResult = await ProcessIncomingOrder(matchedOrder);
                        var processedLines = orderResult.Items
                            .Select(i => $"• {i.ProductName}: הוסר {i.QuantityRemoved}, נשאר {i.NewQuantity}")
                            .ToList();

                        return new ChatResponse
                        {
                            BotMessage = AppendOrderDetails($"🖼️ זיהיתי ב{imageLabel} את הרכיבים הבאים:\n{string.Join("\n", lines)}\n\nהרכיבים תואמים להזמנה: {matchedOrder}\n✅ ההזמנה עובדה בהצלחה.\n\n{string.Join("\n", processedLines)}", orderResult),
                            OrderResult = orderResult,
                            IsOrderProcessing = true
                        };
                    }
                    catch (Exception ex)
                    {
                        return new ChatResponse
                        {
                            BotMessage = $"🖼️ זיהיתי ב{imageLabel} את הרכיבים הבאים:\n{string.Join("\n", lines)}\n\nהרכיבים תואמים להזמנה: {matchedOrder}\n❌ לא ניתן היה לעבד את ההזמנה: {ex.Message}",
                            IsOrderProcessing = false
                        };
                    }
                }

                var hashMatch = FindOrderNameByImageHash(base64Image);
                if (hashMatch != null)
                {
                    try
                    {
                        var orderResult = await ProcessIncomingOrder(hashMatch);
                        var processedLines = orderResult.Items
                            .Select(i => $"• {i.ProductName}: הוסר {i.QuantityRemoved}, נשאר {i.NewQuantity}")
                            .ToList();

                        return new ChatResponse
                        {
                            BotMessage = AppendOrderDetails($"🖼️ זיהיתי את ההזמנה לפי תוכן הקובץ: {hashMatch}\n✅ ההזמנה עובדה בהצלחה.\n\n{string.Join("\n", processedLines)}", orderResult),
                            OrderResult = orderResult,
                            IsOrderProcessing = true
                        };
                    }
                    catch (Exception ex)
                    {
                        return new ChatResponse
                        {
                            BotMessage = $"🖼️ זיהיתי את ההזמנה לפי תוכן הקובץ: {hashMatch}\n❌ לא ניתן היה לעבד את ההזמנה: {ex.Message}",
                            IsOrderProcessing = false
                        };
                    }
                }

                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    var nameWithoutExt = System.Text.RegularExpressions.Regex
                        .Replace(Path.GetFileNameWithoutExtension(fileName).Trim(), @"\s+", " ");
                    var fileMatch = FindMatchingOrderName(_repository.GetAllOrderNames()
                        .Select(SanitizeOrderName)
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(), nameWithoutExt);

                    if (fileMatch != null)
                    {
                        try
                        {
                            var orderResult = await ProcessIncomingOrder(fileMatch);
                            var processedLines = orderResult.Items
                                .Select(i => $"• {i.ProductName}: הוסר {i.QuantityRemoved}, נשאר {i.NewQuantity}")
                                .ToList();

                            return new ChatResponse
                            {
                                BotMessage = AppendOrderDetails($"🖼️ זיהיתי את ההזמנה לפי שם הקובץ '{nameWithoutExt}': {fileMatch}\n✅ ההזמנה עובדה בהצלחה.\n\n{string.Join("\n", processedLines)}", orderResult),
                                OrderResult = orderResult,
                                IsOrderProcessing = true
                            };
                        }
                        catch (Exception ex)
                        {
                            return new ChatResponse
                            {
                                BotMessage = $"🖼️ זיהיתי את ההזמנה לפי שם הקובץ '{nameWithoutExt}': {fileMatch}\n❌ לא ניתן היה לעבד את ההזמנה: {ex.Message}",
                                IsOrderProcessing = false
                            };
                        }
                    }
                }

                return new ChatResponse
                {
                    BotMessage = $"🖼️ זיהיתי ב{imageLabel} את הרכיבים הבאים:\n{string.Join("\n", lines)}.\n\nלא הצלחתי להתאים את הרכיבים להזמנה קיימת בוודאות.",
                    IsOrderProcessing = false
                };
            }
        }
        catch (Exception ex)
        {
            return new ChatResponse
            {
                BotMessage = $"🖼️ קיבלתי את {imageLabel}, אך לא הצלחתי לזהות רכיבים מהתמונה.\n❌ {ex.Message}",
                IsOrderProcessing = false
            };
        }

        var allOrders = _repository.GetAllOrderNames();
        var sanitizedAvailableOrders = allOrders
            .Select(SanitizeOrderName)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var nameWithoutExt = System.Text.RegularExpressions.Regex
                .Replace(Path.GetFileNameWithoutExtension(fileName).Trim(), @"\s+", " ");
            var fileMatch = FindMatchingOrderName(sanitizedAvailableOrders, nameWithoutExt);

            if (fileMatch != null)
            {
                try
                {
                    var orderResult = await ProcessIncomingOrder(fileMatch);
                    var processedLines = orderResult.Items
                        .Select(i => $"• {i.ProductName}: הוסר {i.QuantityRemoved}, נשאר {i.NewQuantity}")
                        .ToList();

                    return new ChatResponse
                    {
                        BotMessage = AppendOrderDetails($"🖼️ זיהיתי את ההזמנה לפי שם הקובץ '{nameWithoutExt}': {fileMatch}\n✅ ההזמנה עובדה בהצלחה.\n\n{string.Join("\n", processedLines)}", orderResult),
                        OrderResult = orderResult,
                        IsOrderProcessing = true
                    };
                }
                catch (Exception ex)
                {
                    return new ChatResponse
                    {
                        BotMessage = $"🖼️ זיהיתי את ההזמנה לפי שם הקובץ '{nameWithoutExt}': {fileMatch}\n❌ לא ניתן היה לעבד את ההזמנה: {ex.Message}",
                        IsOrderProcessing = false
                    };
                }
            }
        }

        var ordersList = string.Join("\n", sanitizedAvailableOrders.Select(o => $"• {o}"));
        return new ChatResponse
        {
            BotMessage = $"🖼️ קיבלתי את {imageLabel}, אך לא הצלחתי לזהות רכיבים.\n\nהזמנות קיימות במערכת:\n{ordersList}",
            IsOrderProcessing = false
        };
    }

    private sealed class DetectedComponent
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    private async Task<List<DetectedComponent>> IdentifyComponentsFromImage(string base64Image, string mimeType)
    {
        var safeBase64Image = base64Image.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? base64Image
            : $"data:{mimeType};base64,{base64Image}";
        var prompt = @"אתה מומחה לזיהוי רכיבים במארזי מתנה מתמונות.
זהה רק פריטים/מוצרים שנראים בתמונה. אל תכלול קופסה, אריזה חיצונית, סרט, נייר אריזה או רקע.
החזר JSON בלבד בפורמט הבא:
{
  ""items"": [
    { ""name"": ""שם המוצר בעברית"", ""quantity"": 1 }
  ]
}
כללים:
- השתמש בשמות מוצרים קצרים וברורים בעברית.
- אם אי אפשר לזהות פריט בוודאות, אל תכלול אותו.
- quantity חייב להיות מספר שלם.
- החזר items ריק אם אין רכיבים מזוהים.";

        var requestMessages = new List<object>
        {
            new { role = "system", content = "You are a strict JSON image component detector. Return only JSON." },
            new {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = prompt },
                    new { type = "image_url", image_url = new { url = safeBase64Image } }
                }
            }
        };

        var requestBody = new
        {
            model = "meta-llama/llama-4-scout-17b-16e-instruct",
            messages = requestMessages,
            response_format = new { type = "json_object" },
            temperature = 0.1
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
            throw new Exception($"שגיאת זיהוי רכיבים מתמונה: {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        using var itemsDoc = JsonDocument.Parse(content);
        var products = _repository.GetAllProductsStock();
        var detected = new List<DetectedComponent>();
        var seenProducts = new HashSet<int>();

        if (itemsDoc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsDoc.RootElement.EnumerateArray())
                AddDetectedComponent(detected, seenProducts, products, item);
        }
        else if (itemsDoc.RootElement.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsProp.EnumerateArray())
                AddDetectedComponent(detected, seenProducts, products, item);
        }

        return detected;
    }

    private void AddDetectedComponent(
        List<DetectedComponent> detected,
        HashSet<int> seenProducts,
        List<(int ProductID, string Name, int CurrentQuantity, int MinQuantity)> products,
        JsonElement item)
    {
        if (!item.TryGetProperty("name", out var nameProp) || !item.TryGetProperty("quantity", out var quantityProp))
            return;

        var productName = SanitizeOrderName(nameProp.GetString() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(productName))
            return;

        int quantity;
        if (quantityProp.ValueKind == JsonValueKind.Number)
            quantity = quantityProp.GetInt32();
        else if (!int.TryParse(quantityProp.GetString(), out quantity))
            quantity = 1;

        var product = FindProductByName(products, productName);
        if (product == null || string.IsNullOrWhiteSpace(product.Value.Name) || seenProducts.Contains(product.Value.ProductID))
            return;

        detected.Add(new DetectedComponent
        {
            ProductID = product.Value.ProductID,
            ProductName = product.Value.Name,
            Quantity = Math.Max(1, quantity)
        });
        seenProducts.Add(product.Value.ProductID);
    }

    private string? FindOrderNameByImageHash(string base64Image)
    {
        try
        {
            var commaIndex = base64Image.IndexOf(',');
            var base64 = commaIndex >= 0 ? base64Image[(commaIndex + 1)..] : base64Image;
            var imageBytes = Convert.FromBase64String(base64);
            var hash = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();

            var pictureFolders = new[]
            {
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "pictures")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "pictures"))
            };

            foreach (var folder in pictureFolders)
            {
                if (!Directory.Exists(folder))
                    continue;

                foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                             .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                         f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                         f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                {
                    var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
                    if (fileHash == hash)
                        return SanitizeOrderName(Path.GetFileNameWithoutExtension(file));
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private string? FindMatchingOrderByDetectedComponents(List<DetectedComponent> detectedComponents)
    {
        var detectedProductIds = detectedComponents.Select(c => c.ProductID).ToHashSet();
        var allOrders = _repository.GetAllOrderNames()
            .Select(SanitizeOrderName)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var bestOrder = string.Empty;
        var bestMatched = 0;
        var bestRequired = 0;

        foreach (var orderName in allOrders)
        {
            var table = _repository.GetOrderComponentsByName(orderName);
            if (table.Rows.Count == 0)
                continue;

            var matched = 0;
            foreach (System.Data.DataRow row in table.Rows)
            {
                var productId = Convert.ToInt32(row["ProductID"]);
                if (detectedProductIds.Contains(productId))
                    matched++;
            }

            if (matched > bestMatched || (matched == bestMatched && table.Rows.Count < bestRequired))
            {
                bestMatched = matched;
                bestRequired = table.Rows.Count;
                bestOrder = orderName;
            }
        }

        if (bestMatched > 0 && bestMatched == bestRequired)
            return bestOrder;

        return null;
    }

    private async Task<string?> IdentifyOrderNameFromImage(string base64Image, string mimeType, List<string> availableOrders)
    {
        var ordersJson = JsonSerializer.Serialize(availableOrders);
        var safeBase64Image = base64Image.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? base64Image
            : $"data:{mimeType};base64,{base64Image}";
        var prompt = $@"אתה מומחה לזיהוי מארזי מתנה מתמונות.
זהה את שם ההזמנה/המארז בתמונה בלבד.
השתמש אך ורק ברשימת השמות הזמינה. אם השם בתמונה לא מופיע ברשימה, החזר מחרוזת ריקה.

Available order names: {ordersJson}

All chat responses must be in Hebrew, but the JSON value orderName must contain only the exact order name from the available order names.
Return ONLY valid JSON with this exact structure:
{{
  ""orderName"": ""exact order name from available order names or empty string""
}}";

        var requestMessages = new List<object>
        {
            new { role = "system", content = "You are a strict JSON image classifier for gift bundles. Return only JSON." },
            new {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = prompt },
                    new { type = "image_url", image_url = new { url = safeBase64Image } }
                }
            }
        };

        var requestBody = new
        {
            model = "llama-3.2-11b-vision-preview",
            messages = requestMessages,
            response_format = new { type = "json_object" },
            temperature = 0.1
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
            throw new Exception($"שגיאת זיהוי תמונה: {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        using var orderDoc = JsonDocument.Parse(content);
        if (!orderDoc.RootElement.TryGetProperty("orderName", out var orderNameProp))
            return string.Empty;

        return SanitizeOrderName(orderNameProp.GetString() ?? string.Empty);
    }

    private string? FindMatchingOrderName(IEnumerable<string> availableOrders, string candidate)
    {
        var sanitizedCandidate = SanitizeOrderName(candidate);
        if (string.IsNullOrWhiteSpace(sanitizedCandidate))
            return null;

        var match = availableOrders.FirstOrDefault(order =>
            order.Equals(sanitizedCandidate, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;

        if (sanitizedCandidate.StartsWith("מארז ", StringComparison.OrdinalIgnoreCase))
        {
            var withoutPrefix = sanitizedCandidate.Substring("מארז ".Length).Trim();
            return availableOrders.FirstOrDefault(order =>
                order.Equals(withoutPrefix, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }
}
