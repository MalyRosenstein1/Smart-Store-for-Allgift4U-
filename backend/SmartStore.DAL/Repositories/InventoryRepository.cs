using System.Data;
using Microsoft.Data.SqlClient;

namespace SmartStore.DAL.Repositories;

public class InventoryRepository
{
    private readonly string _connectionString;

    public InventoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DataTable GetOrderComponentsByName(string orderName)
    {
        var table = new DataTable();
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand("sp_GetOrderComponentsByName", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@OrderName", orderName);
        connection.Open();
        using var reader = command.ExecuteReader();
        table.Load(reader);
        return table;
    }

    public List<string> GetAllOrderNames()
    {
        var orders = new List<string>();
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand("SELECT DISTINCT OrderName FROM OrderTemplates ORDER BY OrderName", connection);
        connection.Open();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            orders.Add(reader["OrderName"].ToString()!);
        return orders;
    }

    public void UpdateProductQuantity(int productId, int quantityToRemove)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "UPDATE Products SET CurrentQuantity = CurrentQuantity - @qty WHERE ProductID = @id", connection);
        command.Parameters.AddWithValue("@qty", quantityToRemove);
        command.Parameters.AddWithValue("@id", productId);
        connection.Open();
        command.ExecuteNonQuery();
    }

    public void InsertRecommendation(int productId, string supplierName, decimal price, string purchaseUrl)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand("sp_InsertRecommendation", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@ProductID", productId);
        command.Parameters.AddWithValue("@SupplierName", supplierName);
        command.Parameters.AddWithValue("@Price", price);
        command.Parameters.AddWithValue("@PurchaseUrl", purchaseUrl);
        connection.Open();
        command.ExecuteNonQuery();
    }

    // Returns existing recommendation for a product (for draft email)
    public (string SupplierName, decimal Price, string? PurchaseUrl)? GetSupplierForProduct(int productId)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "SELECT TOP 1 SupplierName, Price, PurchaseUrl FROM PurchaseRecommendations WHERE ProductID = @id ORDER BY CreatedDate DESC",
            connection);
        command.Parameters.AddWithValue("@id", productId);
        connection.Open();
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return (
            reader["SupplierName"].ToString()!,
            Convert.ToDecimal(reader["Price"]),
            reader["PurchaseUrl"] == DBNull.Value ? null : reader["PurchaseUrl"].ToString()
        );
    }

    // Returns product name + current quantity
    public (string Name, int CurrentQuantity)? GetProductById(int productId)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "SELECT ProductName, CurrentQuantity FROM Products WHERE ProductID = @id", connection);
        command.Parameters.AddWithValue("@id", productId);
        connection.Open();
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return (reader["ProductName"].ToString()!, Convert.ToInt32(reader["CurrentQuantity"]));
    }

    public void SetProductStock(int productId, int newQuantity)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "UPDATE Products SET CurrentQuantity = @qty WHERE ProductID = @id", connection);
        command.Parameters.AddWithValue("@qty", newQuantity);
        command.Parameters.AddWithValue("@id", productId);
        connection.Open();
        command.ExecuteNonQuery();
    }

    // Returns new TemplateID
    public int InsertOrderTemplate(string orderName, string? description)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "INSERT INTO OrderTemplates (OrderName, Description) OUTPUT INSERTED.TemplateID VALUES (@name, @desc)",
            connection);
        command.Parameters.AddWithValue("@name", orderName);
        command.Parameters.AddWithValue("@desc", (object?)description ?? DBNull.Value);
        connection.Open();
        return (int)command.ExecuteScalar()!;
    }

    public void InsertTemplateComponent(int templateId, int productId, int quantityRequired)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "INSERT INTO OrderTemplateComponents (TemplateID, ProductID, QuantityRequired) VALUES (@tid, @pid, @qty)",
            connection);
        command.Parameters.AddWithValue("@tid", templateId);
        command.Parameters.AddWithValue("@pid", productId);
        command.Parameters.AddWithValue("@qty", quantityRequired);
        connection.Open();
        command.ExecuteNonQuery();
    }

    // Returns null if not found
    public (string OrderName, string? Description)? GetTemplateById(int templateId)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "SELECT OrderName, Description FROM OrderTemplates WHERE TemplateID = @id", connection);
        command.Parameters.AddWithValue("@id", templateId);
        connection.Open();
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return (
            reader["OrderName"].ToString()!,
            reader["Description"] == DBNull.Value ? null : reader["Description"].ToString()
        );
    }

    public void UpdateTemplateHeader(int templateId, string orderName, string? description)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "UPDATE OrderTemplates SET OrderName = @name, Description = @desc WHERE TemplateID = @id", connection);
        command.Parameters.AddWithValue("@name", orderName);
        command.Parameters.AddWithValue("@desc", (object?)description ?? DBNull.Value);
        command.Parameters.AddWithValue("@id", templateId);
        connection.Open();
        command.ExecuteNonQuery();
    }

    public void DeleteTemplateComponents(int templateId)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "DELETE FROM OrderTemplateComponents WHERE TemplateID = @id", connection);
        command.Parameters.AddWithValue("@id", templateId);
        connection.Open();
        command.ExecuteNonQuery();
    }

    public List<(int ProductID, int QuantityRequired)> GetTemplateComponents(int templateId)
    {
        var list = new List<(int, int)>();
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "SELECT ProductID, QuantityRequired FROM OrderTemplateComponents WHERE TemplateID = @id", connection);
        command.Parameters.AddWithValue("@id", templateId);
        connection.Open();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            list.Add((Convert.ToInt32(reader["ProductID"]), Convert.ToInt32(reader["QuantityRequired"])));
        return list;
    }

    public List<(int ProductID, string Name, int CurrentQuantity, int MinQuantity)> GetAllProductsStock()
    {
        var list = new List<(int, string, int, int)>();
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(
            "SELECT ProductID, ProductName, CurrentQuantity, MinQuantity FROM Products ORDER BY ProductName", connection);
        connection.Open();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            list.Add((
                Convert.ToInt32(reader["ProductID"]),
                reader["ProductName"].ToString()!,
                Convert.ToInt32(reader["CurrentQuantity"]),
                Convert.ToInt32(reader["MinQuantity"])
            ));
        return list;
    }
}
