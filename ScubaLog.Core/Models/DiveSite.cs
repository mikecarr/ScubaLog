namespace ScubaLog.Core.Models;

/// <summary>
/// Represents a dive site, which includes the name, country, region, latitude, longitude, and notes.
/// </summary>
public class DiveSite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string Notes { get; set; } = string.Empty;
}