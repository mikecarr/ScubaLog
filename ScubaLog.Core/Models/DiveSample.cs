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

    public double? TemperatureC { get; set; }
    public double? TankPressurePsi { get; set; }

    public double? Rmv { get; set; }      // CF/min
    public double? Sac { get; set; }      // PSI/min
    public double? Ppo2 { get; set; }
    public double? NdlMinutes { get; set; }
    public double? TtsMinutes { get; set; }
}