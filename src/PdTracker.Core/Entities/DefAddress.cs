namespace PdTracker.Core.Entities;

public enum AddressFlag { Current, Previous }

public class DefAddress : Entity
{
    public string DefendantId { get; set; } = string.Empty;
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public AddressFlag AddressFlag { get; set; } = AddressFlag.Current;

    // Navigation
    public Defendant? Defendant { get; set; }
}
