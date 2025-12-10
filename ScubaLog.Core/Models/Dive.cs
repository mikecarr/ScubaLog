namespace ScubaLog.Core.Models;


/// <summary>
/// Represents a dive, which includes the site the dive was performed at, the time
/// the dive started and ended, and statistics about the dive.
/// </summary>
public class Dive
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public double Number { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }

    public double MaxDepthMeters { get; set; }
    public double AvgDepthMeters { get; set; }

    public Guid? SiteId { get; set; }
    public DiveSite? Site { get; set; }

    public string Notes { get; set; } = string.Empty;
    
    public List<DiveSample> Samples { get; set; } = new();
}
