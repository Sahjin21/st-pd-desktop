namespace PdTracker.Core.Entities;

public abstract class Entity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
}
