namespace PdTracker.Core.Entities;

public class FinEmployer : Entity
{
    public string DefendantId { get; set; } = string.Empty;
    public int? EmployerCounter { get; set; }
    public string? EmployerName { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public decimal? PayAmt { get; set; }
    public string? PayPeriod { get; set; } // Weekly, Biweekly, Monthly
    public string? NetOrGross { get; set; } // "Net" or "Gross"
    public string? TimeEmployed { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
