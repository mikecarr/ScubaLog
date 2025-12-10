namespace ScubaLog.Core.Models;

public class TankUsage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Ordering within the dive
    public int SortOrder { get; set; }

    // From ZTANKANDGAS
    public bool IsDouble { get; set; }
    public double? DurationSeconds { get; set; }
    public string? SupplyType { get; set; }   // OC / CCR / Bailout

    public double? AirStartPsi { get; set; }
    public double? AirEndPsi { get; set; }

    public string? ExternalId { get; set; }   // ZUUID from ZTANKANDGAS

    // From ZTANK
    public double? TankSizeLiters { get; set; }
    public double? WorkingPressurePsi { get; set; }
    public string? TankName { get; set; }
    public string? TankType { get; set; }     // "AL80", "LP50", etc.

    // From ZGAS
    public double? O2Percent { get; set; }
    public double? HePercent { get; set; }
    public double? MinPpo2 { get; set; }
    public double? MaxPpo2 { get; set; }
}