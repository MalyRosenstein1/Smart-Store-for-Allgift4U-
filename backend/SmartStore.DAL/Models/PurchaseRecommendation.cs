namespace SmartStore.DAL.Models;

public class PurchaseRecommendation
{
    public int RecommendationID { get; set; }
    public int ProductID { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? PurchaseUrl { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Status { get; set; } = "Pending";

    public Product Product { get; set; } = null!;
}
