namespace PdTracker.Core.Entities;

public class EIA : Entity
{
    public int ApplicationNumber { get; set; }
    public string? DefendantId { get; set; } // FK → Defendant.DefendantId, populated via ApplicationNumber lookup
    public string? Type { get; set; }         // 'F', 'M', 'O' — stored as string, no enum
    public string? ApplicationType { get; set; } // alias for Type
    public string? Judge { get; set; }
    public string? EIAResult { get; set; }
    public string? Jail { get; set; }
    public string? Probation { get; set; }
    public decimal? Reimbursement { get; set; }
    public decimal? Bond { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
