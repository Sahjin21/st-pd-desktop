namespace PdTracker.Core.Entities;

public class FinBank : Entity
{
    public string DefendantId { get; set; } = string.Empty;
    public int? BankCounter { get; set; }
    public string? BankName { get; set; }
    public string? AccountType { get; set; }
    public decimal? Balance { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
