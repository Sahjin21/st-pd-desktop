namespace PdTracker.Core.Entities;

public class Qualify : Entity
{
    public int ApplicationNumber { get; set; }
    public DateTime? Date { get; set; }
    public bool NoAction { get; set; } // true = case closed
    public string? Comment { get; set; }
    public string? CourtInformation { get; set; }
    public bool? Military { get; set; }
    public DateTime? EntryDate { get; set; }
    public string? DefendantId { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
