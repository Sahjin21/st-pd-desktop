namespace PdTracker.Core.Entities;

public class Defendant : Entity
{
    public string DefendantId { get; set; } = string.Empty; // 9-char SOID — maps to legacy DefendantID
    public int ApplicationNumber { get; set; } // AutoNumber — the shared FK
    public string? SOID { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public DateTime? DOB { get; set; }
    public string? Education { get; set; }
    public string? Race { get; set; }
    public string? Sex { get; set; }
    public string? Reference1 { get; set; }
    public string? Reference2 { get; set; }
    public string? DefPhoto { get; set; }
    public string? DefSignature { get; set; }
    public int? Dependants { get; set; }
    public string? DepDescription { get; set; }
    public string? DepOther { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.Now;

    // Navigation
    public Qualify? Qualify { get; set; }
    public List<DefAddress> Addresses { get; set; } = new();
    public List<DefPhone> Phones { get; set; } = new();
    public List<DefAlias> Aliases { get; set; } = new();
    public List<Dependent> Dependents { get; set; } = new();
    public DefSpouse? Spouse { get; set; }
    public List<FinEmployer> Employers { get; set; } = new();
    public List<FinAuto> FinAutos { get; set; } = new();
    public List<FinBank> FinBanks { get; set; } = new();
    public List<FinHome> FinHomes { get; set; } = new();
    public List<FinRent> FinRents { get; set; } = new();
    public List<FinOther> FinOthers { get; set; } = new();
    public List<Charge> Charges { get; set; } = new();
    public List<Warrant> Warrants { get; set; } = new();
    public List<Appointment> Appointments { get; set; } = new();
    public List<Voucher> Vouchers { get; set; } = new();
    public EIA? EIA { get; set; }

    public string FullName => $"{FirstName} {(string.IsNullOrEmpty(MiddleName) ? "" : MiddleName + " ")}{LastName}";
}
