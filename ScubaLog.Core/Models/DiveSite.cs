namespace ScubaLog.Core.Models;

/// <summary>
/// Represents a dive site, which includes the name, country, region, latitude, longitude, and notes.
/// </summary>
public class DiveSite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }     // city/region
    public string? Country { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string? WaterType { get; set; }    // salt / fresh
    public string? Difficulty { get; set; }
    public double? AltitudeMeters { get; set; }

    public string? Notes { get; set; }
}