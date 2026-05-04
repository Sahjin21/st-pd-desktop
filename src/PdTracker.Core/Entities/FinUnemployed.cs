namespace PdTracker.Core.Entities;

public enum IncomeSource { Employment, Unemployment, SpouseEmployment, SpouseUnemployment, Other }

public class FinUnemployed : Entity
{
    public string DefendantId { get; set; } = string.Empty;
    public int? UnemployCounter { get; set; }
    public IncomeSource? IncomeSource { get; set; }
    public string? Description { get; set; }
    public string? TimeUnemployed { get; set; }
    public string? PayPeriod { get; set; }
    public decimal? PayAmt { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
