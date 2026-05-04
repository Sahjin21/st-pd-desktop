namespace PdTracker.Core.Entities;

public class Appointment : Entity
{
    public int ApplicationNumber { get; set; }
    public string AttyCode { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public string? Action { get; set; } // "A"=Appointed, "D"=Denied + many others
    public DateTime? DateSigned { get; set; }
    public string? DenyCode { get; set; } // from DENIAL_CODE lookup
    public string? RemovalCode { get; set; } // from REMOVAL_CODE lookup
    public bool? Bonded { get; set; }
    public bool? GAL { get; set; }
    public string? VoucherNumber { get; set; } // assigned when voucher is created
    public string? VoucherLetter { get; set; }
    public bool? ContractCase { get; set; }
    public bool? DUICourt { get; set; }
    public string? JuvenileSubstType { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
    public AttorneyList? Attorney { get; set; }
}
