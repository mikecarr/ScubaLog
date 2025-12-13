using System;

namespace ScubaLog.Core.Models;

/// <summary>
/// Represents a time-series sample from a dive profile.
/// 
/// Canonical units (stored):
/// - DepthMeters (m)
/// - TemperatureC (°C)
/// - TankPressureBar (bar)
/// - RmvLitersPerMinute (L/min, surface-equivalent)
/// - SacBarPerMinute (bar/min, surface-equivalent)
/// - AscentRateMps (m/s)
/// - DecoStopDepthMeters (m) / DecoStopMinutes (min)
/// 
/// Display in Metric/Imperial/Canadian should be done by converting these values in the UI layer.
/// </summary>
public class DiveSample
{
    public int Index { get; set; }

    /// <summary>Elapsed time since dive start.</summary>
    public TimeSpan Time { get; set; }

    /// <summary>Depth in meters (canonical).</summary>
    public double DepthMeters { get; set; }

    /// <summary>Temperature in °C (canonical).</summary>
    public double? TemperatureC { get; set; }

    /// <summary>Tank pressure in bar (canonical). Null if not available.</summary>
    public double? TankPressureBar { get; set; }

    /// <summary>RMV in liters/min at surface-equivalent (canonical). Null if not available.</summary>
    public double? RmvLitersPerMinute { get; set; }

    /// <summary>SAC in bar/min at surface-equivalent (canonical). Null if not available.</summary>
    public double? SacBarPerMinute { get; set; }
    
    /// <summary>SAC in psi/min at surface-equivalent (canonical). Null if not available.</summary>
    public double? SacPsiPerMinute { get; set; }
    

    /// <summary>Partial pressure of O2 in ATA (approx). Null if not available.</summary>
    public double? Ppo2 { get; set; }

    /// <summary>No-decompression limit at this point (minutes). Null if not available.</summary>
    public double? NdlMinutes { get; set; }

    public double? NdtMinutes { get; set; }
    
        
    /// <summary>Time to surface at this point (minutes). Null if not available.</summary>
    public double? TtsMinutes { get; set; }

    /// <summary>
    /// Current ascent/descent rate in meters/second (canonical).
    /// Positive = ascending, negative = descending. Null if not available.
    /// </summary>
    public double? AscentRateMps { get; set; }

    /// <summary>
    /// Current deco stop depth in meters (canonical). Null if not currently on a stop.
    /// </summary>
    public double? DecoStopDepthMeters { get; set; }

    /// <summary>
    /// Current deco stop time in minutes. Null if not currently on a stop.
    /// </summary>
    public double? DecoStopMinutes { get; set; }

    /// <summary>
    /// Active gas label at this point in the dive (e.g. "Air", "EAN32", "Tx18/45").
    /// Null if not available.
    /// </summary>
    public string? Gas { get; set; }

    // -----------------
    // Back-compat aliases (so existing code keeps compiling)
    // -----------------

    [Obsolete("Use DepthMeters (meters)")]
    public double Depth
    {
        get => DepthMeters;
        set => DepthMeters = value;
    }

    [Obsolete("Use TemperatureC (°C)")]
    public double? Temperature
    {
        get => TemperatureC;
        set => TemperatureC = value;
    }

    [Obsolete("Use TankPressureBar (bar)")]
    public double? TankPressure
    {
        get => TankPressureBar;
        set => TankPressureBar = value;
    }

    [Obsolete("Use RmvLitersPerMinute (L/min)")]
    public double? Rmv
    {
        get => RmvLitersPerMinute;
        set => RmvLitersPerMinute = value;
    }

    [Obsolete("Use SacBarPerMinute (bar/min)")]
    public double? Sac
    {
        get => SacBarPerMinute;
        set => SacBarPerMinute = value;
    }
}