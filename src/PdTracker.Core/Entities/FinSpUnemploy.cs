namespace PdTracker.Core.Entities;

public class FinSpUnemploy : Entity
{
    public string DefendantId { get; set; } = string.Empty;
    public int? SpUnemployCounter { get; set; }
    public IncomeSource? IncomeSource { get; set; }
    public string? Description { get; set; }
    public string? TimeUnemployed { get; set; }
    public string? PayPeriod { get; set; }
    public decimal? PayAmt { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
