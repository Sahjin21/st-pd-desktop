namespace PdTracker.Core.Entities;

public class FinHome : Entity
{
    public string DefendantId { get; set; } = string.Empty;
    public int? HomeCounter { get; set; }
    public decimal? MortgagePay { get; set; }
    public decimal? HomeValue { get; set; }
    public decimal? MortgageBalance { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
