namespace PdTracker.Core.Entities;

public enum JurisdictionCode { STE, SUP, MAG, JUV }

public class Warrant : Entity
{
    public int ApplicationNumber { get; set; }
    public string? WarrantNumber { get; set; }
    public string? CaseNumber { get; set; }
    public DateTime? Date { get; set; }
    public DateTime? ArrestDate { get; set; }
    public JurisdictionCode? JurisdictionCode { get; set; }
    public string? BondType { get; set; }
    public decimal? BondAmt { get; set; }
    public bool? Jail { get; set; }
    public string? AddOnCase { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
