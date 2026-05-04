namespace PdTracker.Core.Entities;

public enum AttorneyStatus { Active, Inactive, Suspended }

public class AttorneyList : Entity
{
    public string AttyCode { get; set; } = string.Empty; // e.g. "JARU"
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string? Street { get; set; }
    public string? Suite { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Email { get; set; }
    public string? OfficeNumber { get; set; }
    public string? FaxNumber { get; set; }
    public string? HomeNumber { get; set; }
    public string? PagerNumber { get; set; }
    public string? MobileNumber { get; set; }
    public string? OtherNumber { get; set; }
    public string? PhoneType { get; set; }
    public DateTime? Date { get; set; }
    public AttorneyStatus Status { get; set; } = AttorneyStatus.Active;
    public bool DeathPenalty { get; set; }
    public bool Murder { get; set; }
    public bool Felony { get; set; } = true;
    public bool Misd { get; set; } = true;
    public bool Appeal { get; set; }
    public bool Juvenile { get; set; }
    public bool GAL { get; set; }
    public string? VendorNumber { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public List<Appointment> Appointments { get; set; } = new();
    public List<Voucher> Vouchers { get; set; } = new();

    public string FullName => $"{FirstName} {(string.IsNullOrEmpty(MiddleName) ? "" : MiddleName + " ")}{LastName}";
    public string DisplayName => $"{LastName}, {FirstName} {(string.IsNullOrEmpty(MiddleName) ? "" : MiddleName)}";
}
