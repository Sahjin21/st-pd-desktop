namespace PdTracker.Core.Entities;

public class FinOther : Entity
{
    public string DefendantId { get; set; } = string.Empty;
    public int? OtherCounter { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public decimal? MonthlyAmount { get; set; }
    public decimal? TotalAmount { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
