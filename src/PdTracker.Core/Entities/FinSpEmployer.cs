namespace PdTracker.Core.Entities;

public class FinSpEmployer : Entity
{
    public string DefendantId { get; set; } = string.Empty;
    public int? SpEmployerCounter { get; set; }
    public string? EmployerName { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public decimal? PayAmt { get; set; }
    public string? PayPeriod { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
