namespace ScubaLog.Core.Models;

/// <summary>
/// Represents a sample of dive data, including time, depth, temperature,
/// breathing rate, gas pressure and simple deco info.
/// </summary>
public class DiveSample
{
    public int Index { get; set; }

    public TimeSpan Time { get; set; }
    public double DepthMeters { get; set; }

    // Environment
    public double? TemperatureC { get; set; }

    // Pressure (pick the one(s) you actually use)
    public double? PressureBar { get; set; }
    public double? TankPressurePsi { get; set; }

    // Breathing / gas
    public double? Rmv { get; set; }   // Respiratory minute volume (cf/min)
    public double? Ppo2 { get; set; }  // Partial pressure O₂ (ATA)

    // Toy deco info
    public double? NdlMinutes { get; set; } // No-deco time remaining
    public double? TtsMinutes { get; set; } // Time to surface
}