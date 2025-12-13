using System;
using System.Collections.Generic;

namespace ScubaLog.Core.Models;

public class Dive
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // --- Basic dive info ---
    public int Number { get; set; }                     // ZDIVENUMBER
    public DateTime StartTime { get; set; }             // ZRAWDATE converted
    public TimeSpan Duration { get; set; }              // ZTOTALDURATION
    public double MaxDepthMeters { get; set; }          // ZMAXDEPTH
    public double AvgDepthMeters { get; set; }          // ZAVERAGEDEPTH
    public string? DiveComputer { get; set; }
    public string? DiveComputerSerial { get; set; }

    // --- Site (FK) ---
    public Guid? SiteId { get; set; }
    public DiveSite? Site { get; set; }

    // --- People ---
    public List<Buddy> Buddies { get; set; } = new();   // Buddy join table
    public string? Divemaster { get; set; }             // ZDIVEMASTER
    public string? DiveOperator { get; set; }           // ZDIVEOPERATOR

    // --- Tanks & gases ---
    public List<TankUsage> Tanks { get; set; } = new(); // ZTANKANDGAS + ZGAS
    public string? GasModel { get; set; }               // ZGASMODEL (EAN21, EAN32, Trimix, CCR, etc.)

    // --- Tags & types ---
    public List<string> Tags { get; set; } = new();     // Z_5RELATIONSHIPTAGS
    public List<string> DiveTypes { get; set; } = new(); // e.g. "Boat", "Rebreather", etc.

    // --- Environment ---
    public double? AirTempC { get; set; }               // ZAIRTEMP
    public double? WaterTempHighC { get; set; }         // ZTEMPHIGH
    public double? WaterTempLowC { get; set; }          // ZTEMPLOW
    public string? Weather { get; set; }                // ZWEATHER
    public string? Current { get; set; }                // ZCURRENT
    public string? SurfaceConditions { get; set; }      // ZSURFACECONDITIONS
    public string? Visibility { get; set; }             // ZVISIBILITY (string or int)
    public string? EntryType { get; set; }              // ZENTRYTYPE
    public string? WaterType => Site?.WaterType;

    // --- Repetitive dive info ---
    public int? RepetitiveDiveNumber { get; set; }      // ZREPETITIVEDIVENUMBER
    public double? SurfaceIntervalMinutes { get; set; } // ZSURFACEINTERVAL

    // --- Dive computer / series availability ---
    public bool HasDecompression { get; set; }          // ZDECOMPRESSION
    public bool HasAirSeries { get; set; }              // ZHASAIR
    public bool HasNdtSeries { get; set; }              // ZHASNDT
    public bool HasPpo2Series { get; set; }             // ZHASPPO2
    public bool HasTempSeries { get; set; }             // ZHASTEMP
    public double? CnsPercent { get; set; }             // ZCNS

    public string? DecoModel { get; set; }              // ZDECOMODEL
    public double? SampleIntervalSeconds { get; set; }  // ZSAMPLEINTERVAL

    // --- Logistics / boat ---
    public string? BoatCaptain { get; set; }            // ZBOATCAPTAIN
    public string? BoatName { get; set; }               // ZBOATNAME

    // --- Notes, rating ---
    public double? Rating { get; set; }                 // ZRATING
    public string Notes { get; set; } = "";             // ZNOTES

    // --- Graph data (from UDDF, Shearwater, or synthetic) ---
    public List<DiveSample> Samples { get; set; } = new();
}
