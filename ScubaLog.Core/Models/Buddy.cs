namespace ScubaLog.Core.Models;

public class Buddy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    // MacDive / external IDs
    public string? ExternalId { get; set; }
}