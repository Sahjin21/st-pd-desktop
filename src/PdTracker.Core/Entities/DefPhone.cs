namespace PdTracker.Core.Entities;

public class DefPhone : Entity
{
    public string DefendantId { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PhoneType { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
