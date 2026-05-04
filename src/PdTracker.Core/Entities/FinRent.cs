namespace PdTracker.Core.Entities;

public class FinRent : Entity
{
    public string DefendantId { get; set; } = string.Empty;
    public int? RentCounter { get; set; }
    public decimal? MonthlyRent { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
