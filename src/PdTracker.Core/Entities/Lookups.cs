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

public class Type : Entity
{
    public string TypeCode { get; set; } = string.Empty;
    public string? TypeDescription { get; set; }
}
