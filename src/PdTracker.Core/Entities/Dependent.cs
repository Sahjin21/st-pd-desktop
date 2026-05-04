namespace PdTracker.Core.Entities;

public class Dependent : Entity
{
    public string DefendantId { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
}
