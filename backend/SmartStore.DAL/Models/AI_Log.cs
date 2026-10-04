namespace SmartStore.DAL.Models;

public class AI_Log
{
    public int LogID { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? ImageName { get; set; }
    public string AI_Analysis { get; set; } = string.Empty;
}
