namespace PdTracker.Core.Entities;

public class FinAuto : Entity
{
    public string DefendantId { get; set; } = string.Empty;
    public int? AutoCounter { get; set; }
    public string? Model { get; set; }
    public string? Year { get; set; }
    public decimal? Balance { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
