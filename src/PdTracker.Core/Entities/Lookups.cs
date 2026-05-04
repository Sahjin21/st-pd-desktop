namespace PdTracker.Core.Entities;

public class ChargeId : Entity
{
    public string ChargeIdCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ChargeSelect { get; set; }
}

public class DenialCode : Entity
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class RemovalCode : Entity
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class Jurisdiction : Entity
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class Judge : Entity
{
    public string Code { get; set; } = string.Empty;
    public string? Name { get; set; }
}
