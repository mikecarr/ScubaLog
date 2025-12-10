using System;
using System.Collections.Generic;

namespace ScubaLog.Core.Models;

/// <summary>
/// Represents a single logged dive (header / summary info).
/// Detailed, per-sample data lives in <see cref="Samples"/>.
/// </summary>
public class Dive
{
    /// <summary>Internal id for persistence.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Dive number from your logbook.</summary>
    public double Number { get; set; }

    /// <summary>Local date/time the dive started.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Total underwater time.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Convenience property: when the dive ended.</summary>
    public DateTime EndTime => StartTime + Duration;

    /// <summary>Maximum depth reached (m).</summary>
    public double MaxDepthMeters { get; set; }

    /// <summary>Average depth over the whole dive (m).</summary>
    public double AvgDepthMeters { get; set; }

    // ---- Site ----

    public Guid?    SiteId { get; set; }
    public DiveSite? Site  { get; set; }

    // ---- Gas / tank info ----

    /// <summary>Gas label, e.g. "Air", "EAN32", "Tx18/45".</summary>
    public string Gas { get; set; } = "Air";

    /// <summary>Fraction of O₂ (0.21 for air, 0.32 for EAN32).</summary>
    public double? Fo2 { get; set; }

    /// <summary>Fraction of He for trimix (optional).</summary>
    public double? FHe { get; set; }

    /// <summary>Tank size in cu ft (or litres, depending on how you decide to store it).</summary>
    public double? TankVolumeCuFt { get; set; }

    /// <summary>Starting cylinder pressure (psi).</summary>
    public double? StartPressurePsi { get; set; }

    /// <summary>Ending cylinder pressure (psi).</summary>
    public double? EndPressurePsi { get; set; }

    // ---- Summary stats derived from samples ----

    /// <summary>Shallowest point (m) if you want it, usually 0.</summary>
    public double? MinDepthMeters { get; set; }

    /// <summary>Average surface-equivalent RMV over the dive (cf/min).</summary>
    public double? AvgRmv { get; set; }

    /// <summary>Minimum water temperature (°C).</summary>
    public double? MinTemperatureC { get; set; }

    /// <summary>Maximum water temperature (°C).</summary>
    public double? MaxTemperatureC { get; set; }

    /// <summary>Maximum PPO₂ during the dive.</summary>
    public double? MaxPpo2 { get; set; }

    /// <summary>Minimum NDL seen (i.e., the tightest no-deco limit).</summary>
    public double? MinNdlMinutes { get; set; }

    /// <summary>Maximum TTS seen during the dive.</summary>
    public double? MaxTtsMinutes { get; set; }

    // ---- Meta / notes ----

    /// <summary>Free-form notes / comments for the log.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Per-sample profile data used for graphs and detailed views.</summary>
    public List<DiveSample> Samples { get; set; } = new();
}