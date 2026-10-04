namespace SmartStore.DAL.Models;

public class OrderTemplate
{
    public int TemplateID { get; set; }
    public string OrderName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<OrderTemplateComponent> Components { get; set; } = new List<OrderTemplateComponent>();
}

public class OrderTemplateComponent
{
    public int TemplateID { get; set; }
    public int ProductID { get; set; }
    public int QuantityRequired { get; set; }

    public OrderTemplate Template { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
