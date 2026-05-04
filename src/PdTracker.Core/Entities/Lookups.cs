namespace PdTracker.Core.Entities;

public class ChargeId : Entity
{
    public string ChargeIdCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ChargeSelect { get; set; }
}

public class DenialCode : Entity
{
    public string DenyCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LongText { get; set; }
}

public class RemovalCode : Entity
{
    public string RemovalCodeValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Statement { get; set; }
}

public class Jurisdiction : Entity
{
    public string JurisdictionCode { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class Judge : Entity
{
    public string JudgeCode { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class IncomeSource : Entity
{
    public string IncomeSourceCode { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Generic catch-all lookup table for migrated reference data.
/// </summary>
public class AttorneyListLookups : Entity
{
    public string TableName { get; set; } = string.Empty; // e.g. "CHARGE_ID", "DENIAL_CODE"
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}
