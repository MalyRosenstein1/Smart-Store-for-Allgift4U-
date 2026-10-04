using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

var groqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
if (string.IsNullOrWhiteSpace(groqApiKey))
{
    Console.WriteLine("Set the GROQ_API_KEY environment variable before running this tool.");
    return;
}
const string connectionString = "Server=Maly;Database=SmartStore;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";
const string picturesFolder = @"c:\Users\User\Downloads\SmartStore (2)\SmartStore (2)\SmartStore\OurProject\pictures";

using var httpClient = new HttpClient();

var imageFiles = Directory.GetFiles(picturesFolder, "*.jpg");
Console.WriteLine($"נמצאו {imageFiles.Length} תמונות");

foreach (var imagePath in imageFiles)
{
    var orderName = Path.GetFileNameWithoutExtension(imagePath);
    Console.WriteLine($"\nמעבד: {orderName}");

    var imageBytes = await File.ReadAllBytesAsync(imagePath);
    var base64 = Convert.ToBase64String(imageBytes);

    var prompt = $@"אתה מומחה לזיהוי מארזי מתנה.
תסתכל על התמונה של המארז בשם: ""{orderName}"".
זהה את כל הפריטים/מוצרים הבודדים שנמצאים בתוך המארז.
החזר JSON בלבד בפורמט הבא:
{{
  ""items"": [
    {{ ""name"": ""שם המוצר בעברית"", ""quantity"": 1 }}
  ]
}}
כללים:
- שמות קצרים וברורים בעברית
- אל תכלול את הקופסה/אריזה החיצונית
- רק פריטים שרואים בתמונה";

    var requestBody = new
    {
        model = "meta-llama/llama-4-scout-17b-16e-instruct",
        messages = new[]
        {
            new {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = prompt },
                    new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64}" } }
                }
            }
        },
        response_format = new { type = "json_object" },
        temperature = 0.1
    };

    var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    var req = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", groqApiKey);

    HttpResponseMessage response;
    string responseBody;
    try
    {
        response = await httpClient.SendAsync(req);
        responseBody = await response.Content.ReadAsStringAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"שגיאת רשת: {ex.Message} - מדלג על {orderName}");
        continue;
    }

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"שגיאה: {responseBody}");
        continue;
    }

    using var doc = JsonDocument.Parse(responseBody);
    var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;

    using var itemsDoc = JsonDocument.Parse(content);
    var items = itemsDoc.RootElement.GetProperty("items").EnumerateArray().ToList();

    Console.WriteLine($"זוהו {items.Count} פריטים:");
    foreach (var item in items)
        Console.WriteLine($"  - {item.GetProperty("name").GetString()} x{item.GetProperty("quantity").GetInt32()}");

    using var conn = new SqlConnection(connectionString);
    conn.Open();

    var templateCmd = new SqlCommand(
        "INSERT INTO OrderTemplates (OrderName, Description) OUTPUT INSERTED.TemplateID VALUES (@name, @desc)", conn);
    templateCmd.Parameters.AddWithValue("@name", orderName);
    templateCmd.Parameters.AddWithValue("@desc", $"מארז {orderName}");
    int templateId = (int)templateCmd.ExecuteScalar()!;

    foreach (var item in items)
    {
        var itemName = item.GetProperty("name").GetString()!;
        int quantity = item.GetProperty("quantity").GetInt32();

        var checkCmd = new SqlCommand("SELECT ProductID FROM Products WHERE ProductName = @name", conn);
        checkCmd.Parameters.AddWithValue("@name", itemName);
        var existingId = checkCmd.ExecuteScalar();

        int productId;
        if (existingId == null)
        {
            var insertProduct = new SqlCommand(
                "INSERT INTO Products (ProductName, CurrentQuantity, MinQuantity) OUTPUT INSERTED.ProductID VALUES (@name, 50, 10)", conn);
            insertProduct.Parameters.AddWithValue("@name", itemName);
            productId = (int)insertProduct.ExecuteScalar()!;
            Console.WriteLine($"  + נוסף מוצר: {itemName} (ID={productId})");
        }
        else
        {
            productId = (int)existingId;
            Console.WriteLine($"  ~ קיים: {itemName} (ID={productId})");
        }

        var compCmd = new SqlCommand(
            "IF NOT EXISTS (SELECT 1 FROM OrderTemplateComponents WHERE TemplateID=@tid AND ProductID=@pid) INSERT INTO OrderTemplateComponents (TemplateID, ProductID, QuantityRequired) VALUES (@tid, @pid, @qty)", conn);
        compCmd.Parameters.AddWithValue("@tid", templateId);
        compCmd.Parameters.AddWithValue("@pid", productId);
        compCmd.Parameters.AddWithValue("@qty", quantity);
        compCmd.ExecuteNonQuery();
    }

    Console.WriteLine($"✅ {orderName} הוכנס (TemplateID={templateId})");
    await Task.Delay(1500);
}

Console.WriteLine("\n🎉 סיום!");
