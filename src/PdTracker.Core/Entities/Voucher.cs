namespace PdTracker.Core.Entities;

public enum VoucherOutcome { Guilty, NotGuilty, Plea, Nollepros, DeadDocket, Dismissed, Other }

public class Voucher : Entity
{
    public string VoucherNumber { get; set; } = string.Empty; // 9-digit
    public string? VoucherLetter { get; set; }
    public int? ApplicationNumber { get; set; }
    public string? AttyCode { get; set; }
    public DateTime? DateVchrPaid { get; set; }
    public DateTime? DateCaseCompleted { get; set; }
    public decimal? InCourtHours { get; set; } // was Text(4) in legacy — fixed to Decimal
    public decimal? OutCourtHours { get; set; } // was Text(4)
    public decimal? CourtOrderedReimburse { get; set; }
    public decimal? TotalVoucherAmt { get; set; }
    public decimal? TotalAmountPaid { get; set; }

    // Outcome — one outcome field (mutually exclusive)
    public VoucherOutcome? Outcome { get; set; }
    public string? OutcomeOther { get; set; } // for "Other" outcome

    // Removal reasons (booleans — multiple can be true)
    public bool? AttorneyRemovedOther { get; set; }
    public bool? AttorneyRemovedPC { get; set; }
    public bool? AttorneyRemovedConflict { get; set; }
    public bool? AttorneyRemovedWithdraw { get; set; }
    public bool? AttorneyRemovedJudge { get; set; }
    public bool? AttorneyRemovedIncome { get; set; }
    public bool? Misc { get; set; }
    public bool? HiredPC { get; set; }

    // Additional flags from legacy schema
    public bool? NOLO { get; set; }
    public bool? ContractVoucher { get; set; }
    public bool? PreTrialDiv { get; set; }
    public bool? BenchWarrant { get; set; }
    public bool? TransferredToSuperiorCourt { get; set; }
    public bool? DUISanctions { get; set; }
    public bool? OrderAttorneyRemoved { get; set; }
    public bool? CommunityService { get; set; }
    public bool? Fines { get; set; }
    public bool? JailSanction { get; set; }
    public bool? DUITerminated { get; set; }
    public bool? CHINS { get; set; }
    public bool? Termination { get; set; }
    public bool? Deprivation { get; set; }
    public bool? Appeal { get; set; }
    public DateTime? SanctionDate { get; set; }

    public JurisdictionCode? JurisdictionCode { get; set; }
    public string? VoucherYear { get; set; }

    // Navigation
    public Defendant? Defendant { get; set; }
    public AttorneyList? Attorney { get; set; }
}
