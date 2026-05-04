namespace PdTracker.Core.Entities;

public class Charge : Entity
{
    public int ApplicationNumber { get; set; }
    public int? ChargeNumber { get; set; }
    public string? ChargeType { get; set; } // felony, misd, etc.
    public string? CaseNumber { get; set; }
    public DateTime? ChargeDate { get; set; }
    public string? AddCharge { get; set; }
    public string? WarrantNumber { get; set; }
    public string? ChargeId { get; set; } // FK to CHARGE_ID lookup
    public string? Description { get; set; } // derived via join

    // Navigation
    public Defendant? Defendant { get; set; }
}
