namespace SmartStore.DAL.Models;

public class Product
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CurrentQuantity { get; set; }
    public int MinQuantity { get; set; }

    public ICollection<PurchaseRecommendation> PurchaseRecommendations { get; set; } = new List<PurchaseRecommendation>();
}
