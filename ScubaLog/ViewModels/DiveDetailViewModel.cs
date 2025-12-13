using System;
using System.Collections.Generic;
using System.Linq;
using ScubaLog.Core.Models;
using ScubaLog.Core.Units;

namespace ScubaLog.ViewModels;

public class DiveDetailViewModel : ViewModelBase
{
    public Dive Dive { get; }
    public UnitSystem UnitSystem { get; }

    public DiveDetailViewModel(Dive dive, UnitSystem unitSystem)
    {
        Dive = dive;
        UnitSystem = unitSystem;
    }

    public string Title => $"Dive #{Dive.Number}";
    public string Location => Dive.Site?.Name ?? "—";
    public string Notes => string.IsNullOrWhiteSpace(Dive.Notes) ? "—" : Dive.Notes;

    public string MaxDepthDisplay => $"{ToDisplayDepth(Dive.MaxDepthMeters):0.0} {DepthUnit}";
    public string AvgDepthDisplay => $"{ToDisplayDepth(Dive.AvgDepthMeters):0.0} {DepthUnit}";
    public string DurationDisplay => $"{Dive.Duration:hh\\:mm\\:ss}";
    public string WaterTempDisplay
    {
        get
        {
            if (Dive.WaterTempLowC is null && Dive.WaterTempHighC is null) return "—";
            var lo = Dive.WaterTempLowC is null ? null : ToDisplayTemp(Dive.WaterTempLowC);
            var hi = Dive.WaterTempHighC is null ? null : ToDisplayTemp(Dive.WaterTempHighC);
            if (lo is not null && hi is not null)
                return $"{lo:0.#}–{hi:0.#} {TempUnit}";
            var one = lo ?? hi;
            return one is null ? "—" : $"{one:0.#} {TempUnit}";
        }
    }

    public IEnumerable<TankDisplay> Tanks => Dive.Tanks
        .OrderBy(t => t.SortOrder)
        .Select(t => new TankDisplay(t, UnitSystem, DepthUnit, TempUnit));

    private double ToDisplayDepth(double meters) => UnitSystem == UnitSystem.Imperial ? meters * 3.28084 : meters;
    private double? ToDisplayTemp(double? c) => c is null
        ? null
        : UnitSystem == UnitSystem.Imperial
            ? (c.Value * 9.0 / 5.0) + 32.0
            : c.Value;

    private double ToDisplayPressure(double bar) => UnitSystem == UnitSystem.Imperial ? bar * 14.5037738 : bar;

    public string DepthUnit => UnitSystem == UnitSystem.Imperial ? "ft" : "m";
    public string TempUnit => UnitSystem == UnitSystem.Imperial ? "°F" : "°C";
    public string PressureUnit => UnitSystem == UnitSystem.Imperial ? "psi" : "bar";

    public sealed class TankDisplay
    {
        public string Name { get; }
        public string Mix { get; }
        public string Size { get; }
        public string Start { get; }
        public string End { get; }

        public TankDisplay(TankUsage tank, UnitSystem units, string depthUnit, string tempUnit)
        {
            Name = string.IsNullOrWhiteSpace(tank.TankName) ? "Tank" : tank.TankName!;
            Mix = BuildMixLabel(tank);

            if (tank.TankSizeLiters is double liters)
            {
                if (units == UnitSystem.Imperial)
                {
                    // liters to cubic feet
                    Size = $"{liters / 28.3168466:0.#} cf";
                }
                else
                {
                    Size = $"{liters:0.#} L";
                }
            }
            else
            {
                Size = "—";
            }

            Start = FormatPressure(tank.AirStartPsi, units);
            End   = FormatPressure(tank.AirEndPsi, units);
        }

        private static string BuildMixLabel(TankUsage tank)
        {
            if (tank.O2Percent is null && tank.HePercent is null)
                return "—";

            var o2 = tank.O2Percent is null ? null : $"{tank.O2Percent:0.#}% O2";
            var he = tank.HePercent is null ? null : $"{tank.HePercent:0.#}% He";
            var parts = new[] { o2, he }.Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(" / ", parts);
        }

        private static string FormatPressure(double? psi, UnitSystem units)
        {
            if (psi is null) return "—";
            if (units == UnitSystem.Imperial)
                return $"{psi.Value:0} psi";
            return $"{psi.Value / 14.5037738:0.0} bar";
        }
    }
}
